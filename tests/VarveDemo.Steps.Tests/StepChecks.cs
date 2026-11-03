// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VarveDemo.Importer;
using VarveDemo.Oracle;
using VarveDemo.Snapshot;
using Xunit;

namespace VarveDemo.Steps.Tests;

/// <summary>
/// The step checks of slice 1a, each named after its row in the
/// specification's table, each running its step end to end (AGENTS.md).
/// </summary>
[Collection(nameof(StepChecks))]
public sealed partial class StepChecks
{
    private static readonly TimeSpan s_s1Target = TimeSpan.FromMinutes(3);
    private const double S2TargetMilliseconds = 2000;

    /// <summary>S1, zero to query (docs/spec/script.md, S1).</summary>
    [Fact]
    public async Task S1_ZeroToQuery()
    {
        Stack.RequireSteps();
        SnapshotManifest manifest = Stack.Manifest();
        MeasurementReport report = new MeasurementReport("S1_ZeroToQuery").Fact("snapshotSha256", manifest.Sha256);

        // A clean start: no container, no volume.
        await Stack.ComposeAsync("down --volumes --remove-orphans", TimeSpan.FromMinutes(2));

        // Pulls, measured apart (criterion 8 excludes them).
        ProcessResult pull = await Stack.ComposeAsync("pull --quiet", TimeSpan.FromMinutes(10));
        Assert.True(pull.ExitCode == 0, "docker compose pull failed:\n" + pull.Error);
        report.Add(new Measurement("imagePullSeconds", pull.Elapsed.TotalSeconds, "s"));

        // Criterion 1: `docker compose up`, to the importer's printed answer and exit 0.
        Stopwatch clock = Stopwatch.StartNew();
        ProcessResult up = await Stack.ComposeAsync("up --detach", TimeSpan.FromMinutes(2));
        Assert.True(up.ExitCode == 0, "docker compose up failed:\n" + up.Error);
        long rssStart = await Stack.ServerResidentKiBAsync();

        // Resident memory at the end of the import, before the final
        // checkpoint: sampled when the importer says it is taking it.
        long rssBeforeCheckpoint = -1;
        Task<ProcessResult> waiting = Stack.RunAsync("docker", "wait varve-demo-importer-1", s_s1Target + TimeSpan.FromMinutes(20));
        while (!waiting.IsCompleted)
        {
            if (rssBeforeCheckpoint < 0)
            {
                ProcessResult tail = await Stack.RunAsync("docker", "logs --tail 3 varve-demo-importer-1", TimeSpan.FromSeconds(30));
                if ((tail.Output + tail.Error).Contains("checkpoint: as", StringComparison.Ordinal))
                {
                    rssBeforeCheckpoint = await Stack.ServerResidentKiBAsync();
                }
            }

            await Task.WhenAny(waiting, Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken));
        }

        ProcessResult wait = await waiting;
        TimeSpan toAnswer = clock.Elapsed;
        long rssAfterCheckpoint = await Stack.ServerResidentKiBAsync();
        ProcessResult logs = await Stack.RunAsync("docker", "logs varve-demo-importer-1", TimeSpan.FromMinutes(1));
        string printed = logs.Output + logs.Error;
        Assert.True(wait.Output.Trim() == "0", "The importer exited with " + wait.Output.Trim() + ":\n" + Stack.Indent(printed));

        report.Add(new Measurement("composeUpToAnswerSeconds", toAnswer.TotalSeconds, "s", s_s1Target.TotalSeconds, "max"))
            .Add(new Measurement("serverResidentKiBAtStart", rssStart, "KiB"))
            .Add(new Measurement("serverResidentKiBBeforeFinalCheckpoint", rssBeforeCheckpoint, "KiB"))
            .Add(new Measurement("serverResidentKiBAfterFinalCheckpoint", rssAfterCheckpoint, "KiB"));

        // The base (ADR 0008's amendment): one commit per package, its rate from its own clock.
        Match baseLine = BaseProgress(printed);
        long baseCommits = long.Parse(baseLine.Groups["done"].Value, CultureInfo.InvariantCulture);
        long baseQuads = long.Parse(baseLine.Groups["quads"].Value, CultureInfo.InvariantCulture);
        double baseSeconds = double.Parse(baseLine.Groups["seconds"].Value, CultureInfo.InvariantCulture);
        Assert.Equal(manifest.Base!.Packages, baseCommits);
        Assert.Equal(manifest.Base.Quads, baseQuads);
        AssertRate(baseCommits, baseSeconds, double.Parse(baseLine.Groups["cps"].Value, CultureInfo.InvariantCulture));
        AssertRate(baseQuads, baseSeconds, double.Parse(baseLine.Groups["qps"].Value, CultureInfo.InvariantCulture));
        Assert.Contains("History before " + SnapshotInputs.Format(manifest.From) + " is not replayed", printed, StringComparison.Ordinal);
        report.Add(new Measurement("baseSeconds", baseSeconds, "s"))
            .Add(new Measurement("baseCommits", baseCommits, "count", manifest.Base.Packages, "equal"))
            .Add(new Measurement("baseVersions", manifest.Base.Versions, "count"))
            .Add(new Measurement("baseQuads", baseQuads, "count", manifest.Base.Quads, "equal"));

        // Criteria 4 and 5: the full replay of the window, its rate from its own clock and the manifest's counts.
        (long commits, long quads, double seconds, double commitsPerSecond, double quadsPerSecond) = FinalProgress(printed);
        Assert.Equal(manifest.Counts.CatalogCommitsRetained, commits);
        Assert.Equal(manifest.Counts.Quads, quads);
        AssertRate(commits, seconds, commitsPerSecond);
        AssertRate(quads, seconds, quadsPerSecond);
        report.Add(new Measurement("importSeconds", seconds, "s"))
            .Add(new Measurement("commitsPerSecond", commitsPerSecond, "per-s"))
            .Add(new Measurement("quadsPerSecond", quadsPerSecond, "per-s"));

        // Criterion 7: N and the window are on screen.
        Assert.Contains("N = " + manifest.N.ToString(CultureInfo.InvariantCulture), printed, StringComparison.Ordinal);
        Assert.Contains("catalog time window " + SnapshotInputs.Format(manifest.From) + " to " + SnapshotInputs.Format(manifest.To), printed, StringComparison.Ordinal);

        // The first answer: the count the manifest predicts.
        Assert.Contains("answer: " + manifest.Base.Packages.ToString(CultureInfo.InvariantCulture) + " base states and " + manifest.Counts.CatalogCommitsRetained.ToString(CultureInfo.InvariantCulture) + " catalog commits", printed, StringComparison.Ordinal);

        using HttpClient http = new();
        using ClientCredentials viewer = Stack.Client(http, "viewer");

        // Criterion 3: no token, no answer.
        using (HttpResponseMessage anonymous = await http.PostAsync(new Uri(Stack.Server, "datasets/nuget/sparql"), new StringContent("ASK {}", System.Text.Encoding.UTF8, "application/sparql-query"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        // Criterion 6: one commit per retained catalog commit, each the importer's.
        DemoServer server = new(http, Stack.Server, "nuget");
        SelectAnswer count = await SparqlSelect.RunAsync(server.Client(viewer, TimeSpan.FromMinutes(1)), SparqlSelect.Load("catalog-commits.rq"), TestContext.Current.CancellationToken);
        Assert.Equal(manifest.Counts.CatalogCommitsRetained.ToString(CultureInfo.InvariantCulture), count.Solutions[0]["catalogCommits"]);
        Assert.Equal(manifest.Base.Packages.ToString(CultureInfo.InvariantCulture), count.Solutions[0]["baseStates"]);
        IReadOnlyList<(long Position, string Agent)> feed = await Stack.FeedAgentsAsync(http, viewer, count.Position);
        Assert.Equal(manifest.Base.Packages + manifest.Counts.CatalogCommitsRetained, feed.Count);
        Assert.All(feed, static commit => Assert.Equal("<" + Stack.Issuer + "#varve-demo-importer>", commit.Agent));
        Assert.True(feed.Zip(feed.Skip(1)).All(static pair => pair.First.Position < pair.Second.Position), "The feed's positions are not strictly increasing.");
        string positions = Path.Combine(Stack.ResultsDirectory, "s1-compose-import.json.positions.tsv");
        if (File.Exists(positions))
        {
            long[] acknowledged = [.. File.ReadLines(positions).Skip(1).Select(static line => long.Parse(line.Split('\t')[2], CultureInfo.InvariantCulture))];
            Assert.Equal(manifest.Counts.CatalogCommitsRetained, acknowledged.Length);
            Assert.Equal(feed.Skip((int)manifest.Base.Packages).Select(static c => c.Position), acknowledged);
        }

        report.Add(new Measurement("head", count.Position, "position"))
            .Add(new Measurement("feedCommitsWithImporterAgent", feed.Count, "count", manifest.Base.Packages + manifest.Counts.CatalogCommitsRetained, "equal"));

        // Resident memory after a restart of the server, on the same volume (ruling 3 of the review).
        ProcessResult restart = await Stack.ComposeAsync("restart varve", TimeSpan.FromMinutes(2));
        Assert.True(restart.ExitCode == 0, restart.Error);
        await server.WaitReadyAsync(TimeSpan.FromMinutes(2), TextWriter.Null, TestContext.Current.CancellationToken);
        SelectAnswer afterRestart = await SparqlSelect.RunAsync(server.Client(viewer, TimeSpan.FromMinutes(1)), SparqlSelect.Load("catalog-commits.rq"), TestContext.Current.CancellationToken);
        Assert.Equal(count.Position, afterRestart.Position);
        report.Add(new Measurement("serverResidentKiBAfterRestart", await Stack.ServerResidentKiBAsync(), "KiB"));

        // The same flow through the installed tool (ADR 0012 point 4): a fresh
        // stack without the importer, then `varve-demo import` from this machine.
        await Stack.ComposeAsync("down --volumes", TimeSpan.FromMinutes(2));
        ProcessResult upServer = await Stack.ComposeAsync("up --detach mock-oauth2 varve setup", TimeSpan.FromMinutes(2));
        Assert.True(upServer.ExitCode == 0, "docker compose up failed:\n" + upServer.Error);
        string toolResults = Path.Combine(Stack.ResultsDirectory, "s1-tool-import.json");
        ProcessResult tool = await Stack.ToolAsync("import --snapshot " + Stack.SnapshotDirectory + " --results " + toolResults, TimeSpan.FromMinutes(15));
        string toolPrinted = tool.Output + tool.Error;
        Assert.True(tool.ExitCode == 0, "varve-demo import exited with " + tool.ExitCode + ":\n" + Stack.Indent(toolPrinted));
        (long toolCommits, long toolQuads, double toolSeconds, double toolCommitsPerSecond, double toolQuadsPerSecond) = FinalProgress(toolPrinted);
        Assert.Equal(manifest.Counts.CatalogCommitsRetained, toolCommits);
        Match toolBase = BaseProgress(toolPrinted);
        report.Add(new Measurement("toolBaseSeconds", double.Parse(toolBase.Groups["seconds"].Value, CultureInfo.InvariantCulture), "s"));
        Assert.Equal(manifest.Counts.Quads, toolQuads);
        AssertRate(toolCommits, toolSeconds, toolCommitsPerSecond);
        AssertRate(toolQuads, toolSeconds, toolQuadsPerSecond);
        report.Add(new Measurement("toolImportSeconds", toolSeconds, "s"))
            .Add(new Measurement("toolCommitsPerSecond", toolCommitsPerSecond, "per-s"))
            .Add(new Measurement("toolQuadsPerSecond", toolQuadsPerSecond, "per-s"))
            .Add(new Measurement("toolImportProcessSeconds", tool.Elapsed.TotalSeconds, "s"));

        report.Write(Path.Combine(Stack.ResultsDirectory, "S1_ZeroToQuery.json"), DateTimeOffset.UtcNow);

        // Criterion 8, last, so every number above is recorded first.
        Assert.True(toAnswer <= s_s1Target, "From docker compose up to the answer took " + toAnswer.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s; the target is " + s_s1Target.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " s.");
    }

    /// <summary>S2, deprecated and vulnerable dependencies (docs/spec/script.md, S2).</summary>
    [Fact]
    public async Task S2_DeprecatedAndVulnerableDependencies()
    {
        Stack.RequireSteps();
        SnapshotManifest manifest = Stack.Manifest();
        CatalogOracle oracle = CatalogOracle.Load(Stack.SnapshotDirectory);
        string[] topIds = [.. File.ReadAllLines(Path.Combine(Stack.Root, "snapshot", "ids.txt")).Where(static l => l.Length > 0)];
        await EnsureLoadedAsync(manifest);

        // The head S1 printed: the count's position (criterion 5).
        ProcessResult count = await Stack.ToolAsync("count", TimeSpan.FromMinutes(1));
        Assert.True(count.ExitCode == 0, count.Output + count.Error);
        long head = long.Parse(CountPosition().Match(count.Output).Groups["p"].Value, CultureInfo.InvariantCulture);

        // Criterion 4: the committed lock file, and one generated from the
        // snapshot with a known deprecated and a known vulnerable version.
        OracleVersion deprecated = oracle.Present.Where(static v => v.Deprecated).OrderBy(static v => v.Id, StringComparer.Ordinal).ThenBy(static v => v.Version, StringComparer.Ordinal).First();
        OracleVersion vulnerable = oracle.Present.Where(static v => v.Advisories.Count > 0 && !v.Deprecated).OrderBy(static v => v.Id, StringComparer.Ordinal).ThenBy(static v => v.Version, StringComparer.Ordinal).First();
        string generated = Path.Combine(Stack.ResultsDirectory, "generated.packages.lock.json");
        Directory.CreateDirectory(Stack.ResultsDirectory);
        await File.WriteAllTextAsync(generated, LockFile((deprecated.Id, deprecated.LeafVersion), (vulnerable.Id, vulnerable.LeafVersion)), TestContext.Current.CancellationToken);

        MeasurementReport report = new MeasurementReport("S2_DeprecatedAndVulnerableDependencies").Fact("snapshotSha256", manifest.Sha256);
        int file = 0;
        foreach (string lockFile in new[] { Path.Combine(Stack.Root, "samples", "packages.lock.json"), generated })
        {
            file++;
            string answer = Path.Combine(Stack.ResultsDirectory, "s2-answer-" + file.ToString(CultureInfo.InvariantCulture) + ".txt");
            string results = Path.Combine(Stack.ResultsDirectory, "s2-query-" + file.ToString(CultureInfo.InvariantCulture) + ".json");
            ProcessResult deps = await Stack.ToolAsync("deps --lock " + lockFile + " --answer " + answer + " --results " + results, TimeSpan.FromMinutes(2));
            string printed = deps.Output + deps.Error;
            Assert.True(deps.ExitCode == 0, "varve-demo deps exited with " + deps.ExitCode + ":\n" + Stack.Indent(printed));

            // Criterion 1: the query text is shown.
            Assert.Contains("PREFIX nuget: <http://schema.nuget.org/schema#>", printed, StringComparison.Ordinal);

            // Criteria 2 and 3: every locked package once, and field by field as the oracle says.
            IReadOnlyList<LockedPackage> locked = Dependencies.ReadLockFile(lockFile);
            string[] lines = await File.ReadAllLinesAsync(answer, TestContext.Current.CancellationToken);
            Assert.Equal(locked.Count, lines.Length);
            foreach (string line in lines)
            {
                string[] fields = line.Split(" | ");
                string expected = oracle.Answer(fields[0], fields[1], topIds);
                string[] expectedFields = expected.Split(" | ");
                string[] names = ["id", "version", "group", "outside", "deprecation", "advisories", "suggested"];
                for (int i = 0; i < names.Length; i++)
                {
                    Assert.True(expectedFields[i] == fields[i], fields[0] + " " + fields[1] + ": " + names[i] + " is " + fields[i] + " from Varve, " + expectedFields[i] + " from the oracle.");
                }
            }

            Assert.Equal(
                [.. locked.Select(static p => p.Id.ToLowerInvariant() + " " + p.Version.ToLowerInvariant()).Order(StringComparer.Ordinal)],
                lines.Select(static l => string.Join(' ', l.Split(" | ").Take(2))).Order(StringComparer.Ordinal));

            // Criterion 5: within the target, at S1's head.
            using JsonDocument measured = JsonDocument.Parse(await File.ReadAllBytesAsync(results, TestContext.Current.CancellationToken));
            double milliseconds = Value(measured, "queryMilliseconds");
            long position = (long)Value(measured, "position");
            Assert.Equal(head, position);
            report.Add(new Measurement("queryMilliseconds" + file.ToString(CultureInfo.InvariantCulture), milliseconds, "ms", S2TargetMilliseconds, "max"))
                .Add(new Measurement("packages" + file.ToString(CultureInfo.InvariantCulture), lines.Length, "count"))
                .Add(new Measurement("flagged" + file.ToString(CultureInfo.InvariantCulture), lines.Count(static l => l.Split(" | ")[2] == "flagged"), "count"));
        }

        report.Add(new Measurement("head", head, "position"));
        report.Write(Path.Combine(Stack.ResultsDirectory, "S2_DeprecatedAndVulnerableDependencies.json"), DateTimeOffset.UtcNow);
        Assert.All(report.Measurements, static m => Assert.True(m.Met, m.Name + " is " + m.Value.ToString(CultureInfo.InvariantCulture) + " " + m.Unit + "; the target is " + m.Target?.ToString(CultureInfo.InvariantCulture) + "."));
    }

    /// <summary>Loads the snapshot through the tool when the stack is not already at S1's end.</summary>
    private static async Task EnsureLoadedAsync(SnapshotManifest manifest)
    {
        ProcessResult count = await Stack.ToolAsync("count", TimeSpan.FromMinutes(1));
        if (count.ExitCode == 0 && count.Output.Contains("answer: " + manifest.Base?.Packages.ToString(CultureInfo.InvariantCulture) + " base states and " + manifest.Counts.CatalogCommitsRetained.ToString(CultureInfo.InvariantCulture) + " catalog commits", StringComparison.Ordinal))
        {
            return;
        }

        await Stack.ComposeAsync("down --volumes", TimeSpan.FromMinutes(2));
        ProcessResult up = await Stack.ComposeAsync("up --detach mock-oauth2 varve setup", TimeSpan.FromMinutes(5));
        Assert.True(up.ExitCode == 0, up.Error);
        ProcessResult import = await Stack.ToolAsync("import --snapshot " + Stack.SnapshotDirectory, TimeSpan.FromMinutes(15));
        Assert.True(import.ExitCode == 0, import.Output + import.Error);
    }

    private static string LockFile(params (string Id, string Version)[] packages) =>
        "{\n  \"version\": 1,\n  \"dependencies\": {\n    \"net10.0\": {\n"
        + string.Join(",\n", packages.Select(static p => "      \"" + p.Id + "\": { \"type\": \"Direct\", \"requested\": \"[" + p.Version + ", )\", \"resolved\": \"" + p.Version + "\" }"))
        + "\n    }\n  }\n}\n";

    private static double Value(JsonDocument report, string name) =>
        report.RootElement.GetProperty("measurements").EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("value").GetDouble();

    private static (long Commits, long Quads, double Seconds, double CommitsPerSecond, double QuadsPerSecond) FinalProgress(string printed)
    {
        Match[] matches = [.. ProgressLine().Matches(printed).Cast<Match>()];
        Assert.True(matches.Length > 0, "No progress line in:\n" + Stack.Indent(printed));
        Match last = matches[^1];
        Assert.Equal(last.Groups["done"].Value, last.Groups["total"].Value);
        return (
            long.Parse(last.Groups["done"].Value, CultureInfo.InvariantCulture),
            long.Parse(last.Groups["quads"].Value, CultureInfo.InvariantCulture),
            double.Parse(last.Groups["seconds"].Value, CultureInfo.InvariantCulture),
            double.Parse(last.Groups["cps"].Value, CultureInfo.InvariantCulture),
            double.Parse(last.Groups["qps"].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Criterion 5: the printed rate is the count over the printed time, within the printed rounding.</summary>
    private static void AssertRate(long count, double seconds, double printedRate)
    {
        double exact = count / seconds;

        // Seconds are printed to the millisecond and rates to a tenth.
        double tolerance = 0.05 + (count / (seconds * seconds) * 0.0005);
        Assert.True(Math.Abs(exact - printedRate) <= tolerance, "Printed " + printedRate.ToString(CultureInfo.InvariantCulture) + "/s for " + count + " in " + seconds.ToString(CultureInfo.InvariantCulture) + " s, which is " + exact.ToString(CultureInfo.InvariantCulture) + "/s.");
    }

    private static Match BaseProgress(string printed)
    {
        Match[] matches = [.. BaseLine().Matches(printed).Cast<Match>()];
        Assert.True(matches.Length > 0, "No base progress line in:\n" + Stack.Indent(printed));
        Assert.Equal(matches[^1].Groups["done"].Value, matches[^1].Groups["total"].Value);
        return matches[^1];
    }

    [GeneratedRegex(@"base: (?<done>\d+)/(?<total>\d+) packages, (?<quads>\d+) quads, (?<seconds>[\d.]+) s, (?<cps>[\d.]+) commits/s, (?<qps>[\d.]+) quads/s", RegexOptions.CultureInvariant)]
    private static partial Regex BaseLine();

    [GeneratedRegex(@"import: (?<done>\d+)/(?<total>\d+) catalog commits, (?<quads>\d+) quads, (?<seconds>[\d.]+) s, (?<cps>[\d.]+) commits/s, (?<qps>[\d.]+) quads/s", RegexOptions.CultureInvariant)]
    private static partial Regex ProgressLine();

    [GeneratedRegex(@"at position (?<p>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex CountPosition();
}
