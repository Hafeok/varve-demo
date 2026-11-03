// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Varve.Protocol.Client;
using VarveDemo.Snapshot;

namespace VarveDemo.Importer;

/// <summary>The importer's settings.</summary>
public sealed record ImportSettings
{
    /// <summary><c>VarveDemo:Checkpoints:EveryCommits</c>, K of ADR 0004 point 6.</summary>
    public required long CheckpointEveryCommits { get; init; }

    /// <summary>How long to wait for the dataset to be ready.</summary>
    public TimeSpan ReadyTimeout { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>How often progress is printed.</summary>
    public TimeSpan ProgressEvery { get; init; } = TimeSpan.FromSeconds(2);
}

/// <summary>One acknowledged commit: the catalog commit and the position Varve gave it.</summary>
/// <param name="Sequence">The commit's place in the snapshot.</param>
/// <param name="CommitId">The catalog commit.</param>
/// <param name="Position">The <c>Varve-Position</c> of the acknowledgement.</param>
public sealed record Acknowledged(long Sequence, string CommitId, long Position);

/// <summary>What an import measured.</summary>
public sealed record ImportResult
{
    /// <summary>Commits sent and acknowledged.</summary>
    public required long Commits { get; init; }

    /// <summary>Base commits acknowledged, one per package.</summary>
    public required long BaseCommits { get; init; }

    /// <summary>Quads the base inserted.</summary>
    public required long BaseQuads { get; init; }

    /// <summary>How long the base took.</summary>
    public required TimeSpan BaseElapsed { get; init; }

    /// <summary>Quads the acknowledged updates inserted.</summary>
    public required long Quads { get; init; }

    /// <summary>From the first update sent to the last acknowledged.</summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>The first and last acknowledged positions.</summary>
    public required (long First, long Last) Positions { get; init; }

    /// <summary>The position of the checkpoint taken after the import.</summary>
    public required long CheckpointPosition { get; init; }

    /// <summary>How long the final checkpoint took.</summary>
    public required TimeSpan CheckpointElapsed { get; init; }

    /// <summary>The server's advertised maxAsOfDistance.</summary>
    public required long MaxAsOfDistance { get; init; }

    /// <summary>Every acknowledgement, in order.</summary>
    public required IReadOnlyList<Acknowledged> Acknowledgements { get; init; }

    /// <summary>Commits per second, from the run's own clock.</summary>
    public double CommitsPerSecond => Commits / Elapsed.TotalSeconds;

    /// <summary>Quads per second, from the run's own clock.</summary>
    public double QuadsPerSecond => Quads / Elapsed.TotalSeconds;
}

/// <summary>An update or a leaf refused: the import stops and names it (ADR 0006 point 4).</summary>
public sealed class ImportRefusedException : Exception
{
    /// <summary>For serialisers.</summary>
    public ImportRefusedException()
    {
    }

    /// <summary>With a message.</summary>
    public ImportRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>With a message and a cause.</summary>
    public ImportRefusedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// S1's import (ADRs 0004, 0006, 0008): checks K against the server's bound,
/// replays every retained catalog commit as one SPARQL Update, in order,
/// checking each acknowledgement's position, then takes the final checkpoint
/// as the dataset's admin (ADR 0007's amendment). It prints progress as it
/// goes, every figure from its own clock and the server's acknowledgements.
/// </summary>
public sealed class ImportRun
{
    private readonly DemoServer _server;
    private readonly ClientCredentials _importer;
    private readonly ClientCredentials _checkpointer;
    private readonly ImportSettings _settings;
    private readonly TextWriter _out;

    /// <summary>An import against <paramref name="server"/>, writing as <paramref name="importer"/>, checkpointing as <paramref name="checkpointer"/>.</summary>
    public ImportRun(DemoServer server, ClientCredentials importer, ClientCredentials checkpointer, ImportSettings settings, TextWriter output)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        _checkpointer = checkpointer ?? throw new ArgumentNullException(nameof(checkpointer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _out = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>Runs the import of <paramref name="snapshot"/>.</summary>
    public async Task<ImportResult> RunAsync(SnapshotReader snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        SnapshotManifest manifest = snapshot.Manifest;
        BaseCounts? baseCounts = manifest.Base;
        await _out.WriteLineAsync("snapshot: N = " + manifest.N.ToString(CultureInfo.InvariantCulture)
            + ", catalog time window " + SnapshotInputs.Format(manifest.From) + " to " + SnapshotInputs.Format(manifest.To)
            + ", " + manifest.Counts.CatalogCommitsRetained.ToString(CultureInfo.InvariantCulture) + " catalog commits, "
            + manifest.Counts.Quads.ToString(CultureInfo.InvariantCulture) + " quads, sha256 " + manifest.Sha256).ConfigureAwait(false);
        await _out.WriteLineAsync(baseCounts is { } described
            ? "base: the catalog's state of the " + manifest.N.ToString(CultureInfo.InvariantCulture) + " packages as of " + SnapshotInputs.Format(manifest.From)
                + ": " + described.Versions.ToString(CultureInfo.InvariantCulture) + " versions, " + described.Packages.ToString(CultureInfo.InvariantCulture) + " packages, "
                + described.Quads.ToString(CultureInfo.InvariantCulture) + " quads. History before " + SnapshotInputs.Format(manifest.From) + " is not replayed; `varve-demo import --since <date>` replays it."
            : "base: none; every catalog commit from " + SnapshotInputs.Format(manifest.From) + " is replayed").ConfigureAwait(false);

        await _server.WaitReadyAsync(_settings.ReadyTimeout, _out, cancellationToken).ConfigureAwait(false);
        long bound = await _server.MaxAsOfDistanceAsync(_importer, cancellationToken).ConfigureAwait(false);
        if (_settings.CheckpointEveryCommits <= 0 || _settings.CheckpointEveryCommits >= bound)
        {
            throw new ImportRefusedException("VarveDemo:Checkpoints:EveryCommits is " + _settings.CheckpointEveryCommits.ToString(CultureInfo.InvariantCulture)
                + "; it must be above 0 and below the server's maxAsOfDistance, " + bound.ToString(CultureInfo.InvariantCulture) + " (ADR 0004 point 6). Nothing was imported.");
        }

        SparqlHttpClient admin = _server.Client(_checkpointer, TimeSpan.FromMinutes(10));
        long head = await HeadAsync(admin, cancellationToken).ConfigureAwait(false);
        if (head != 0)
        {
            throw new ImportRefusedException("The dataset " + _server.Dataset + " is at position " + head.ToString(CultureInfo.InvariantCulture)
                + "; the import replays onto an empty dataset only. Start from an empty volume (docker compose down --volumes). Nothing was imported.");
        }

        await _out.WriteLineAsync("server: " + _server.Server + ", dataset " + _server.Dataset + ", maxAsOfDistance " + bound.ToString(CultureInfo.InvariantCulture)
            + ", K = " + _settings.CheckpointEveryCommits.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);

        SparqlHttpClient client = _server.Client(_importer, TimeSpan.FromMinutes(5));
        long last = 0;

        // The base: one Varve commit per package, as the importer.
        long baseCommits = 0;
        long baseQuads = 0;
        Stopwatch baseClock = Stopwatch.StartNew();
        if (baseCounts is not null)
        {
            await _out.WriteLineAsync("base: as " + _importer.ClientId + ", one SPARQL Update per package").ConfigureAwait(false);
            TimeSpan nextBaseProgress = _settings.ProgressEvery;
            await foreach (SnapshotBasePackage package in snapshot.BaseAsync(cancellationToken).ConfigureAwait(false))
            {
                CatalogUpdateText update = Mapped(() => CatalogUpdate.MapBase(package, manifest.From), "base of " + package.Id);
                last = await SendAsync(client, update, "the base of " + package.Id + " (" + package.Sequence.ToString(CultureInfo.InvariantCulture) + " of the base)", last, cancellationToken).ConfigureAwait(false);
                baseCommits++;
                baseQuads += update.Quads;
                if (baseClock.Elapsed >= nextBaseProgress)
                {
                    await Progress("base", "packages", baseCommits, baseCounts.Packages, baseQuads, baseClock.Elapsed, last).ConfigureAwait(false);
                    nextBaseProgress = baseClock.Elapsed + _settings.ProgressEvery;
                }
            }

            TimeSpan baseElapsed = TimeSpan.FromMilliseconds(Math.Round(baseClock.Elapsed.TotalMilliseconds));
            await Progress("base", "packages", baseCommits, baseCounts.Packages, baseQuads, baseElapsed, last).ConfigureAwait(false);
            if (baseCommits != baseCounts.Packages || baseQuads != baseCounts.Quads)
            {
                throw new ImportRefusedException("The base acknowledged " + baseCommits.ToString(CultureInfo.InvariantCulture) + " commits and " + baseQuads.ToString(CultureInfo.InvariantCulture)
                    + " quads; the manifest states " + baseCounts.Packages.ToString(CultureInfo.InvariantCulture) + " and " + baseCounts.Quads.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        TimeSpan baseTook = TimeSpan.FromMilliseconds(Math.Round(baseClock.Elapsed.TotalMilliseconds));

        // The window: one Varve commit per catalog commit, in catalog order.
        await _out.WriteLineAsync("import: as " + _importer.ClientId + ", one SPARQL Update per catalog commit").ConfigureAwait(false);
        List<Acknowledged> acknowledged = new((int)Math.Min(manifest.Counts.CatalogCommitsRetained, int.MaxValue));
        long quads = 0;
        Stopwatch clock = Stopwatch.StartNew();
        TimeSpan nextProgress = _settings.ProgressEvery;

        await foreach (SnapshotCommit commit in snapshot.CommitsAsync(cancellationToken).ConfigureAwait(false))
        {
            CatalogUpdateText update = Mapped(() => CatalogUpdate.Map(commit), "catalog commit " + commit.Commit.CommitId + " (" + commit.Sequence.ToString(CultureInfo.InvariantCulture) + " of the snapshot)");
            last = await SendAsync(client, update, "catalog commit " + commit.Commit.CommitId + " (" + commit.Sequence.ToString(CultureInfo.InvariantCulture)
                + " of the snapshot, first leaf " + commit.Leaves[0].Ref.Url + ")", last, cancellationToken).ConfigureAwait(false);
            quads += update.Quads;
            acknowledged.Add(new Acknowledged(commit.Sequence, commit.Commit.CommitId, last));

            if (clock.Elapsed >= nextProgress)
            {
                await Progress("import", "catalog commits", acknowledged.Count, manifest.Counts.CatalogCommitsRetained, quads, clock.Elapsed, last).ConfigureAwait(false);
                nextProgress = clock.Elapsed + _settings.ProgressEvery;
            }
        }

        // The final line's rates are its own counts over its own printed time,
        // so a viewer (and S1's check, criterion 5) can redo the division.
        TimeSpan elapsed = TimeSpan.FromMilliseconds(Math.Round(clock.Elapsed.TotalMilliseconds));
        await Progress("import", "catalog commits", acknowledged.Count, manifest.Counts.CatalogCommitsRetained, quads, elapsed, last).ConfigureAwait(false);

        if (acknowledged.Count != manifest.Counts.CatalogCommitsRetained || quads != manifest.Counts.Quads)
        {
            throw new ImportRefusedException("The import acknowledged " + acknowledged.Count.ToString(CultureInfo.InvariantCulture) + " commits and " + quads.ToString(CultureInfo.InvariantCulture)
                + " quads; the manifest states " + manifest.Counts.CatalogCommitsRetained.ToString(CultureInfo.InvariantCulture) + " and " + manifest.Counts.Quads.ToString(CultureInfo.InvariantCulture) + ".");
        }

        // The final checkpoint (ADR 0004 point 6), as the dataset's admin.
        await _out.WriteLineAsync("checkpoint: as " + _checkpointer.ClientId + " (admin on " + _server.Dataset + "), POST /manage/datasets/" + _server.Dataset + "/checkpoints").ConfigureAwait(false);
        Stopwatch checkpointClock = Stopwatch.StartNew();
        using (HttpResponseMessage response = await admin.CheckpointAsync(null, cancellationToken).ConfigureAwait(false))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ImportRefusedException("Varve refused the final checkpoint: " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " "
                    + await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
        }

        long checkpoint = await NewestCheckpointAsync(admin, cancellationToken).ConfigureAwait(false);
        TimeSpan checkpointElapsed = checkpointClock.Elapsed;
        if (checkpoint < last)
        {
            throw new ImportRefusedException("The newest checkpoint is at " + checkpoint.ToString(CultureInfo.InvariantCulture) + ", below the import's last position " + last.ToString(CultureInfo.InvariantCulture) + ".");
        }

        await _out.WriteLineAsync("checkpoint: at position " + checkpoint.ToString(CultureInfo.InvariantCulture) + ", in "
            + checkpointElapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s; every imported position is now at as-of distance 0").ConfigureAwait(false);

        return new ImportResult
        {
            Commits = acknowledged.Count,
            Quads = quads,
            Elapsed = elapsed,
            BaseCommits = baseCommits,
            BaseQuads = baseQuads,
            BaseElapsed = baseTook,
            Positions = (acknowledged[0].Position, last),
            CheckpointPosition = checkpoint,
            CheckpointElapsed = checkpointElapsed,
            MaxAsOfDistance = bound,
            Acknowledgements = acknowledged,
        };
    }

    /// <summary>The newest checkpoint the dataset's status lists.</summary>
    public static async Task<long> NewestCheckpointAsync(SparqlHttpClient admin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admin);
        using HttpResponseMessage response = await admin.StatusAsync(cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ImportRefusedException("The dataset's status was refused: " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + body);
        }

        using JsonDocument status = JsonDocument.Parse(body);
        long newest = 0;
        foreach (JsonElement checkpoint in status.RootElement.GetProperty("checkpoints").EnumerateArray())
        {
            newest = Math.Max(newest, checkpoint.GetInt64());
        }

        return newest;
    }

    private Task Progress(string phase, string unit, long commits, long total, long quads, TimeSpan elapsed, long position)
    {
        double seconds = Math.Max(elapsed.TotalSeconds, 1e-9);
        return _out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
            $"{phase}: {commits}/{total} {unit}, {quads} quads, {elapsed.TotalSeconds:0.000} s, {commits / seconds:0.0} commits/s, {quads / seconds:0.0} quads/s, position {position}"));
    }

    private static CatalogUpdateText Mapped(Func<CatalogUpdateText> map, string what)
    {
        try
        {
            return map();
        }
        catch (LeafRefusedException error)
        {
            throw new ImportRefusedException(what + ": " + error.Message, error);
        }
    }

    /// <summary>Sends one update; returns its acknowledged position, which must be above <paramref name="last"/>.</summary>
    private static async Task<long> SendAsync(SparqlHttpClient client, CatalogUpdateText update, string what, long last, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.UpdateAsync(update.Text, (long?)null, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string problem = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new ImportRefusedException("Varve refused the update for " + what + ": " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + problem);
        }

        long position = SparqlHttpClient.PositionOf(response)
            ?? throw new ImportRefusedException("The acknowledgement of " + what + " carries no Varve-Position.");
        return position > last
            ? position
            : throw new ImportRefusedException("The acknowledgement of " + what + " is at position " + position.ToString(CultureInfo.InvariantCulture)
                + ", not above the previous one's, " + last.ToString(CultureInfo.InvariantCulture) + ".");
    }

    /// <summary>The dataset's head, from its status.</summary>
    private static async Task<long> HeadAsync(SparqlHttpClient admin, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await admin.StatusAsync(cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ImportRefusedException("The dataset's status was refused: " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + body);
        }

        using JsonDocument status = JsonDocument.Parse(body);
        return status.RootElement.GetProperty("head").GetInt64();
    }
}
