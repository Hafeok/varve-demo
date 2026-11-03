// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using VarveDemo.Importer;
using VarveDemo.Snapshot;
using VarveDemo.Steps;

namespace VarveDemo.Tool;

/// <summary><c>varve-demo import</c>: S1's load, then its first answer.</summary>
internal static class ImportCommand
{
    /// <summary>A snapshot verified against a manifest other than the pinned one, for a measurement; said so on screen.</summary>
    private static async Task<SnapshotReader> UnpinnedAsync(string directory, string manifestPath, CancellationToken cancellationToken)
    {
        SnapshotManifest manifest = SnapshotManifest.Read(await System.IO.File.ReadAllBytesAsync(manifestPath, cancellationToken).ConfigureAwait(false));
        await Console.Out.WriteLineAsync("snapshot: " + directory + ", sha256 " + manifest.Sha256 + " (NOT the pinned snapshot: verified against " + manifestPath + ")").ConfigureAwait(false);
        return await SnapshotReader.OpenAsync(directory, manifest, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<int> RunAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        string? since = arguments.Optional("since");
        string? unpinned = arguments.Optional("manifest");
        string? source = since is null ? arguments.Required("snapshot") : null;
        string cache = arguments.OrDefault("cache", ".data/snapshots");
        string? results = arguments.Optional("results");
        long k = arguments.Long("checkpoints-every-commits", Environment.GetEnvironmentVariable("VarveDemo__Checkpoints__EveryCommits") is { Length: > 0 } fromConfig
            ? long.Parse(fromConfig, CultureInfo.InvariantCulture)
            : 5000);
        using Connection connection = new(arguments);
        using ClientCredentials importer = connection.Client("importer");
        using ClientCredentials checkpointer = connection.Client("checkpointer");
        long kMemory = arguments.Long("checkpoints-for-memory-every-commits", Environment.GetEnvironmentVariable("VarveDemo__Checkpoints__ForMemoryEveryCommits") is { Length: > 0 } memoryConfig
            ? long.Parse(memoryConfig, CultureInfo.InvariantCulture)
            : 0);
        arguments.RefuseUnknown();

        SnapshotReader snapshot = since is not null
            ? await DeepHistory.BuildAsync(SnapshotInputs.Timestamp(since), cache, Console.Out, cancellationToken).ConfigureAwait(false)
            : unpinned is not null
                ? await UnpinnedAsync(source!, unpinned, cancellationToken).ConfigureAwait(false)
                : await PinnedSnapshot.OpenAsync(source!, cache, Console.Out, cancellationToken).ConfigureAwait(false);
        ImportRun run = new(connection.Server, importer, checkpointer, new ImportSettings { CheckpointEveryCommits = k, MemoryCheckpointEveryCommits = kMemory }, Console.Out);
        ImportResult result = await run.RunAsync(snapshot, cancellationToken).ConfigureAwait(false);

        // S1's first answer, as the importer, which may read what it wrote.
        string query = SparqlSelect.Load("catalog-commits.rq");
        SelectAnswer answer = await SparqlSelect.RunAsync(connection.Server.Client(importer, TimeSpan.FromMinutes(1)), query, cancellationToken).ConfigureAwait(false);
        long counted = long.Parse(answer.Solutions[0]["catalogCommits"] ?? "-1", CultureInfo.InvariantCulture);
        long bases = long.Parse(answer.Solutions[0]["baseStates"] ?? "-1", CultureInfo.InvariantCulture);
        long expectedBases = snapshot.Manifest.Base?.Packages ?? 0;
        await Console.Out.WriteLineAsync(query).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
            $"answer: {bases} base states and {counted} catalog commits at position {answer.Position}, in {answer.Elapsed.TotalMilliseconds:0} ms; the manifest predicts {expectedBases} and {snapshot.Manifest.Counts.CatalogCommitsRetained}")).ConfigureAwait(false);

        if (results is not null)
        {
            new MeasurementReport("S1_ZeroToQuery.import")
                .Fact("snapshotSha256", snapshot.Manifest.Sha256)
                .Fact("tool", ToolVersion.Text)
                .Fact("server", connection.Server.Server.ToString())
                .Add(new Measurement("baseCommits", result.BaseCommits, "count", expectedBases, "equal"))
                .Add(new Measurement("baseQuads", result.BaseQuads, "count", snapshot.Manifest.Base?.Quads ?? 0, "equal"))
                .Add(new Measurement("baseSeconds", result.BaseElapsed.TotalSeconds, "s"))
                .Add(new Measurement("catalogCommits", result.Commits, "count", snapshot.Manifest.Counts.CatalogCommitsRetained, "equal"))
                .Add(new Measurement("quads", result.Quads, "count", snapshot.Manifest.Counts.Quads, "equal"))
                .Add(new Measurement("importSeconds", result.Elapsed.TotalSeconds, "s"))
                .Add(new Measurement("commitsPerSecond", result.CommitsPerSecond, "per-s"))
                .Add(new Measurement("quadsPerSecond", result.QuadsPerSecond, "per-s"))
                .Add(new Measurement("firstPosition", result.Positions.First, "position"))
                .Add(new Measurement("lastPosition", result.Positions.Last, "position"))
                .Add(new Measurement("checkpointPosition", result.CheckpointPosition, "position"))
                .Add(new Measurement("checkpointSeconds", result.CheckpointElapsed.TotalSeconds, "s"))
                .Add(new Measurement("maxAsOfDistance", result.MaxAsOfDistance, "count"))
                .Add(new Measurement("answerBaseStates", bases, "count", expectedBases, "equal"))
                .Add(new Measurement("answerCatalogCommits", counted, "count", snapshot.Manifest.Counts.CatalogCommitsRetained, "equal"))
                .Add(new Measurement("answerPosition", answer.Position, "position"))
                .Add(new Measurement("answerMilliseconds", answer.Elapsed.TotalMilliseconds, "ms"))
                .Write(results, DateTimeOffset.UtcNow);
            await PositionsFile.WriteAsync(results + ".positions.tsv", result, cancellationToken).ConfigureAwait(false);
        }

        return counted == snapshot.Manifest.Counts.CatalogCommitsRetained && bases == expectedBases ? 0 : 1;
    }
}
