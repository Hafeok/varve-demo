// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VarveDemo.Importer;
using VarveDemo.Steps;

namespace VarveDemo.Tool;

/// <summary>The steps a viewer runs against the loaded dataset.</summary>
internal static partial class StepCommands
{
    /// <summary><c>varve-demo count</c>: S1's first answer, as the viewer.</summary>
    public static async Task<int> CountAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        using Connection connection = new(arguments);
        using ClientCredentials viewer = connection.Client("viewer");
        arguments.RefuseUnknown();
        string query = SparqlSelect.Load("catalog-commits.rq");
        await Console.Out.WriteLineAsync(query).ConfigureAwait(false);
        SelectAnswer answer = await SparqlSelect.RunAsync(connection.Server.Client(viewer, TimeSpan.FromMinutes(1)), query, cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
            $"answer: {answer.Solutions[0]["catalogCommits"]} catalog commits at position {answer.Position}, in {answer.Elapsed.TotalMilliseconds:0} ms")).ConfigureAwait(false);
        return 0;
    }

    /// <summary><c>varve-demo deps --lock path</c>: S2, as the viewer.</summary>
    public static async Task<int> DepsAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        string lockFile = arguments.Required("lock");
        string? results = arguments.Optional("results");
        string? answerFile = arguments.Optional("answer");
        using Connection connection = new(arguments);
        using ClientCredentials viewer = connection.Client("viewer");
        arguments.RefuseUnknown();

        IReadOnlyList<LockedPackage> packages = Dependencies.ReadLockFile(lockFile);
        await Console.Out.WriteLineAsync("lock file: " + lockFile + ", " + packages.Count.ToString(CultureInfo.InvariantCulture) + " package versions").ConfigureAwait(false);
        DependencyReport report = await Dependencies.AskAsync(connection.Server.Client(viewer, TimeSpan.FromMinutes(1)), packages, PinnedIds.Ids(), cancellationToken).ConfigureAwait(false);
        Dependencies.Print(report, Console.Out);

        if (answerFile is not null)
        {
            await File.WriteAllLinesAsync(answerFile, report.Answers.Select(static a => a.Line()), cancellationToken).ConfigureAwait(false);
        }

        if (results is not null)
        {
            new MeasurementReport("S2_DeprecatedAndVulnerableDependencies.query")
                .Fact("lockFile", lockFile)
                .Fact("tool", ToolVersion.Text)
                .Add(new Measurement("queryMilliseconds", report.Elapsed.TotalMilliseconds, "ms", 2000, "max"))
                .Add(new Measurement("position", report.Position, "position"))
                .Add(new Measurement("packages", report.Answers.Count, "count"))
                .Add(new Measurement("flagged", report.Answers.Count(static a => a.Group == DependencyGroup.Flagged), "count"))
                .Add(new Measurement("clean", report.Answers.Count(static a => a.Group == DependencyGroup.Clean), "count"))
                .Add(new Measurement("outside", report.Answers.Count(static a => a.Group == DependencyGroup.Outside), "count"))
                .Write(results, DateTimeOffset.UtcNow);
        }

        return 0;
    }
}

/// <summary>The subset's id list this build of the tool pins, embedded beside the manifest.</summary>
internal static class PinnedIds
{
    public static IReadOnlyList<string> Ids()
    {
        using Stream stream = typeof(PinnedIds).Assembly.GetManifestResourceStream("snapshot/ids.txt")
            ?? throw new InvalidDataException("This build of varve-demo carries no id list.");
        using StreamReader reader = new(stream);
        return [.. reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)];
    }
}
