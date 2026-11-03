// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VarveDemo.Importer;
using VarveDemo.Snapshot;

namespace VarveDemo.Tool;

/// <summary>
/// <c>varve-demo import --since &lt;date&gt;</c> (ADR 0008's amendment): the
/// deep history, rebuilt from the catalog with no base, every catalog commit
/// from <c>since</c> to the pinned snapshot's upper bound. It is a snapshot of
/// its own, with its own manifest and hash, printed before the import; the
/// pinned manifest does not cover it, and the step checks never run it.
/// </summary>
internal static class DeepHistory
{
    public static async Task<SnapshotReader> BuildAsync(DateTimeOffset since, string cache, TextWriter log, CancellationToken cancellationToken)
    {
        SnapshotManifest pinned = PinnedSnapshot.Manifest();
        if (since >= pinned.To)
        {
            throw new UsageException("--since must be before the snapshot's upper bound, " + SnapshotInputs.Format(pinned.To) + ".");
        }

        SnapshotInputs inputs = new(pinned.N, PinnedIds.Ids(), since, pinned.To, Base: false);
        string directory = Path.Combine(cache, "since-" + since.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture));
        if (!File.Exists(Path.Combine(directory, "manifest.json")))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            await log.WriteLineAsync("since: building the deep history from nuget.org, " + SnapshotInputs.Format(since) + " to " + SnapshotInputs.Format(pinned.To)).ConfigureAwait(false);
            using CatalogHttp http = new();
            SnapshotDraft draft = await new SnapshotBuilder(http, log).BuildAsync(inputs, directory, cancellationToken).ConfigureAwait(false);
            SnapshotManifest built = await ManifestWriter.CompleteAsync(draft, inputs, directory, ToolVersion.Text, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(directory, "manifest.json"), built.ToJson(), cancellationToken).ConfigureAwait(false);
        }

        SnapshotManifest manifest = SnapshotManifest.Read(await File.ReadAllBytesAsync(Path.Combine(directory, "manifest.json"), cancellationToken).ConfigureAwait(false));
        await log.WriteLineAsync("since: " + directory + ", sha256 " + manifest.Sha256 + " (not the pinned snapshot; built by this run)").ConfigureAwait(false);
        return await SnapshotReader.OpenAsync(directory, manifest, cancellationToken).ConfigureAwait(false);
    }
}
