// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VarveDemo.Importer;
using VarveDemo.Snapshot;
using Xunit;

namespace VarveDemo.Steps.Tests;

/// <summary>A finished process: its exit code and what it printed.</summary>
internal sealed record ProcessResult(int ExitCode, string Output, string Error, TimeSpan Elapsed);

/// <summary>
/// The compose stack and the installed tool, as the steps job sets them up:
/// <list type="bullet">
/// <item><c>VARVE_DEMO_STEPS=1</c> turns the step checks on;</item>
/// <item><c>VARVE_DEMO_COMPOSE_FILES</c>: <c>compose.yaml</c> and the job's override, <c>;</c>-separated;</item>
/// <item><c>VARVE_DEMO_SNAPSHOT_DIR</c>: the snapshot the job rebuilt from nuget.org;</item>
/// <item><c>VARVE_DEMO_TOOL</c>: the <c>varve-demo</c> installed from the packed tool;</item>
/// <item><c>VARVE_DEMO_RESULTS_DIR</c>: where measurement reports go, for the artifact.</item>
/// </list>
/// </summary>
internal static partial class Stack
{
    public const string Issuer = "http://mock-oauth2:8080/varve-demo";
    public static readonly Uri IssuerAddress = new("http://localhost:8090");
    public static readonly Uri Server = new("http://localhost:8080");

    public static string ResultsDirectory => Environment.GetEnvironmentVariable("VARVE_DEMO_RESULTS_DIR") is { Length: > 0 } d ? d : Path.Combine(Root, ".data", "results");

    public static string SnapshotDirectory => Required("VARVE_DEMO_SNAPSHOT_DIR");

    public static string Tool => Required("VARVE_DEMO_TOOL");

    public static string Root { get; } = FindRoot();

    /// <summary>Skips the calling check unless the steps job turned the checks on.</summary>
    public static void RequireSteps() =>
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("VARVE_DEMO_STEPS") == "1",
            "Step checks drive docker compose and the installed tool; CI's steps job runs them with VARVE_DEMO_STEPS=1.");

    public static SnapshotManifest Manifest() => SnapshotManifest.Read(File.ReadAllBytes(Path.Combine(Root, "snapshot", "manifest.json")));

    public static Task<ProcessResult> ComposeAsync(string arguments, TimeSpan timeout) =>
        RunAsync("docker", "compose " + string.Join(' ', ComposeFiles().Select(static f => "-f " + f)) + " " + arguments, timeout);

    public static Task<ProcessResult> ToolAsync(string arguments, TimeSpan timeout) => RunAsync(Tool, arguments, timeout);

    public static async Task<ProcessResult> RunAsync(string file, string arguments, TimeSpan timeout)
    {
        ProcessStartInfo start = new(file, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Root,
        };
        Stopwatch clock = Stopwatch.StartNew();
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start " + file);
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource cancel = new(timeout);
        try
        {
            await process.WaitForExitAsync(cancel.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(file + " " + arguments + " ran past " + timeout + ".");
        }

        return new ProcessResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false), clock.Elapsed);
    }

    /// <summary>The Varve server process's resident memory, in KiB, by <c>docker top</c> (the image has no shell).</summary>
    public static async Task<long> ServerResidentKiBAsync()
    {
        ProcessResult top = await RunAsync("docker", "top varve-demo-varve-1 -o pid,rss", TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        string[] lines = top.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(top.ExitCode == 0 && lines.Length >= 2, "docker top failed: " + top.Error);
        return lines.Skip(1).Sum(static line => long.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture));
    }

    public static ClientCredentials Client(HttpClient http, string role) =>
        new(http, new IssuerEndpoint(new Uri(Issuer), IssuerAddress), "varve-demo-" + role, "not-a-secret-the-issuer-is-a-mock", null, TimeProvider.System);

    /// <summary>
    /// Every commit's position and agent, from the feed restricted to the
    /// catalog log (which every import commit touches), following
    /// <c>rel="next"</c> pages (Varve's change-feed spec §4).
    /// </summary>
    public static async Task<IReadOnlyList<(long Position, string Agent)>> FeedAgentsAsync(HttpClient http, ClientCredentials viewer, long head)
    {
        List<(long, string)> commits = [];
        Uri? next = new(Server, "datasets/nuget/commits?from=0&to=" + head.ToString(CultureInfo.InvariantCulture) + "&graph=" + Uri.EscapeDataString(DemoNames.CatalogLog));
        while (next is not null)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, next);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", (await viewer.GetAsync(CancellationToken.None).ConfigureAwait(false))!.Value.Value);
            using HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.True(response.IsSuccessStatusCode, "The feed was refused: " + (int)response.StatusCode + " " + body);
            long position = -1;
            foreach (string line in body.Split('\n'))
            {
                if (line.StartsWith("commit ", StringComparison.Ordinal))
                {
                    position = long.Parse(line.Split(' ')[1], CultureInfo.InvariantCulture);
                    commits.Add((position, string.Empty));
                }
                else if (line.StartsWith("agent ", StringComparison.Ordinal) && position >= 0)
                {
                    commits[^1] = (position, line["agent ".Length..].Trim());
                }
            }

            next = response.Headers.TryGetValues("Link", out IEnumerable<string>? links)
                ? links.SelectMany(static l => l.Split(',')).Select(static l => NextLink().Match(l)).Where(static m => m.Success).Select(m => new Uri(Server, m.Groups["url"].Value)).FirstOrDefault()
                : null;
        }

        return commits;
    }

    private static string[] ComposeFiles() =>
        (Environment.GetEnvironmentVariable("VARVE_DEMO_COMPOSE_FILES") is { Length: > 0 } files ? files : "compose.yaml")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException(name + " is not set; the steps job sets it.");

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VarveDemo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No VarveDemo.slnx above " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"<(?<url>[^>]+)>\s*;\s*rel=""?next""?", RegexOptions.CultureInvariant)]
    private static partial Regex NextLink();

    /// <summary>The text with every line prefixed, for a failure message.</summary>
    public static string Indent(string text) => new StringBuilder().AppendJoin('\n', text.Split('\n').Select(static l => "  " + l)).ToString();
}
