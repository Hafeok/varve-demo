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
        await _out.WriteLineAsync("snapshot: N = " + manifest.N.ToString(CultureInfo.InvariantCulture)
            + ", catalog time window " + SnapshotInputs.Format(manifest.From) + " to " + SnapshotInputs.Format(manifest.To)
            + ", " + manifest.Counts.CatalogCommitsRetained.ToString(CultureInfo.InvariantCulture) + " catalog commits, "
            + manifest.Counts.Quads.ToString(CultureInfo.InvariantCulture) + " quads, sha256 " + manifest.Sha256).ConfigureAwait(false);

        await _server.WaitReadyAsync(_settings.ReadyTimeout, _out, cancellationToken).ConfigureAwait(false);
        long bound = await _server.MaxAsOfDistanceAsync(_importer, cancellationToken).ConfigureAwait(false);
        if (_settings.CheckpointEveryCommits <= 0 || _settings.CheckpointEveryCommits >= bound)
        {
            throw new ImportRefusedException("VarveDemo:Checkpoints:EveryCommits is " + _settings.CheckpointEveryCommits.ToString(CultureInfo.InvariantCulture)
                + "; it must be above 0 and below the server's maxAsOfDistance, " + bound.ToString(CultureInfo.InvariantCulture) + " (ADR 0004 point 6). Nothing was imported.");
        }

        await _out.WriteLineAsync("server: " + _server.Server + ", dataset " + _server.Dataset + ", maxAsOfDistance " + bound.ToString(CultureInfo.InvariantCulture)
            + ", K = " + _settings.CheckpointEveryCommits.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await _out.WriteLineAsync("import: as " + _importer.ClientId + ", one SPARQL Update per catalog commit").ConfigureAwait(false);

        SparqlHttpClient client = _server.Client(_importer, TimeSpan.FromMinutes(2));
        CatalogUpdate mapper = new(snapshot.RemoteContexts());
        List<Acknowledged> acknowledged = new((int)Math.Min(manifest.Counts.CatalogCommitsRetained, int.MaxValue));
        long quads = 0;
        long last = 0;
        Stopwatch clock = Stopwatch.StartNew();
        TimeSpan nextProgress = _settings.ProgressEvery;

        await foreach (SnapshotCommit commit in snapshot.CommitsAsync(cancellationToken).ConfigureAwait(false))
        {
            CatalogUpdateText update;
            try
            {
                update = mapper.Map(commit);
            }
            catch (LeafRefusedException error)
            {
                throw new ImportRefusedException("Catalog commit " + commit.Commit.CommitId + " (" + commit.Sequence.ToString(CultureInfo.InvariantCulture) + " of the snapshot): " + error.Message, error);
            }

            using HttpResponseMessage response = await client.UpdateAsync(update.Text, (long?)null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string problem = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new ImportRefusedException("Varve refused the update for catalog commit " + commit.Commit.CommitId + " ("
                    + commit.Sequence.ToString(CultureInfo.InvariantCulture) + " of the snapshot, first leaf " + commit.Leaves[0].Ref.Url + "): "
                    + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + problem);
            }

            long position = SparqlHttpClient.PositionOf(response)
                ?? throw new ImportRefusedException("The acknowledgement of catalog commit " + commit.Commit.CommitId + " carries no Varve-Position.");
            if (position <= last)
            {
                throw new ImportRefusedException("The acknowledgement of catalog commit " + commit.Commit.CommitId + " is at position " + position.ToString(CultureInfo.InvariantCulture)
                    + ", not above the previous one's, " + last.ToString(CultureInfo.InvariantCulture) + ".");
            }

            last = position;
            quads += update.Quads;
            acknowledged.Add(new Acknowledged(commit.Sequence, commit.Commit.CommitId, position));

            if (clock.Elapsed >= nextProgress)
            {
                await Progress(acknowledged.Count, manifest.Counts.CatalogCommitsRetained, quads, clock.Elapsed, position).ConfigureAwait(false);
                nextProgress = clock.Elapsed + _settings.ProgressEvery;
            }
        }

        // The final line's rates are its own counts over its own printed time,
        // so a viewer (and S1's check, criterion 5) can redo the division.
        TimeSpan elapsed = TimeSpan.FromMilliseconds(Math.Round(clock.Elapsed.TotalMilliseconds));
        await Progress(acknowledged.Count, manifest.Counts.CatalogCommitsRetained, quads, elapsed, last).ConfigureAwait(false);

        if (acknowledged.Count != manifest.Counts.CatalogCommitsRetained || quads != manifest.Counts.Quads)
        {
            throw new ImportRefusedException("The import acknowledged " + acknowledged.Count.ToString(CultureInfo.InvariantCulture) + " commits and " + quads.ToString(CultureInfo.InvariantCulture)
                + " quads; the manifest states " + manifest.Counts.CatalogCommitsRetained.ToString(CultureInfo.InvariantCulture) + " and " + manifest.Counts.Quads.ToString(CultureInfo.InvariantCulture) + ".");
        }

        // The final checkpoint (ADR 0004 point 6), as the dataset's admin.
        await _out.WriteLineAsync("checkpoint: as " + _checkpointer.ClientId + " (admin on " + _server.Dataset + "), POST /manage/datasets/" + _server.Dataset + "/checkpoints").ConfigureAwait(false);
        Stopwatch checkpointClock = Stopwatch.StartNew();
        SparqlHttpClient admin = _server.Client(_checkpointer, TimeSpan.FromMinutes(10));
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

    private Task Progress(long commits, long total, long quads, TimeSpan elapsed, long position)
    {
        double seconds = Math.Max(elapsed.TotalSeconds, 1e-9);
        return _out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
            $"import: {commits}/{total} catalog commits, {quads} quads, {elapsed.TotalSeconds:0.000} s, {commits / seconds:0.0} commits/s, {quads / seconds:0.0} quads/s, position {position}"));
    }
}
