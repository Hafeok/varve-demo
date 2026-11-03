// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VarveDemo.Snapshot;

namespace VarveDemo.Importer;

/// <summary>
/// Completes a snapshot's manifest from what the builder wrote: the counts,
/// the quads the importer's mapping inserts (so the manifest predicts what the
/// import sends), the files and their hashes.
/// </summary>
public static class ManifestWriter
{
    /// <summary>The manifest of the snapshot in <paramref name="directory"/>, built from <paramref name="inputs"/> by <paramref name="tool"/>.</summary>
    public static async Task<SnapshotManifest> CompleteAsync(SnapshotDraft draft, SnapshotInputs inputs, string directory, string tool, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(inputs);
        IReadOnlyList<SnapshotFile> files = await SnapshotManifest.HashFilesAsync(directory, cancellationToken).ConfigureAwait(false);
        SnapshotManifest provisional = new()
        {
            Tool = tool,
            N = inputs.N,
            IdsSha256 = Sha256Text.Of(Encoding.UTF8.GetBytes(string.Join('\n', inputs.Ids) + "\n")),
            From = inputs.From,
            To = inputs.To,
            Pages = draft.Pages,
            Commits = (draft.Commits[0].CommitId, draft.Commits[^1].CommitId),
            Counts = new SnapshotCounts
            {
                CatalogCommitsRetained = draft.Commits.Count,
                CatalogCommitsSkipped = draft.Skipped,
                Leaves = 0,
                DetailsLeaves = 0,
                DeleteLeaves = 0,
                Packages = 0,
                Versions = 0,
                VersionsPresent = 0,
                Quads = 0,
            },
            RemoteContexts = draft.RemoteContexts,
            InlineContexts = draft.InlineContexts,
            Files = files,
        };

        SnapshotReader reader = SnapshotReader.OpenUnverified(directory, provisional);
        CatalogUpdate mapper = new(reader.RemoteContexts());
        long quads = 0;
        long details = 0;
        long deletes = 0;
        HashSet<string> packages = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> versions = new(StringComparer.Ordinal);
        HashSet<string> present = new(StringComparer.Ordinal);
        await foreach (SnapshotCommit commit in reader.CommitsAsync(cancellationToken).ConfigureAwait(false))
        {
            quads += mapper.CountQuads(commit);
            foreach (SnapshotLeaf leaf in commit.Leaves)
            {
                string graph = DemoNames.VersionGraph(leaf.Ref.Id, leaf.Ref.Version);
                packages.Add(leaf.Ref.Id);
                versions.Add(graph);
                if (leaf.Ref.IsDelete)
                {
                    deletes++;
                    present.Remove(graph);
                }
                else
                {
                    details++;
                    present.Add(graph);
                }
            }
        }

        return provisional with
        {
            Counts = provisional.Counts with
            {
                Leaves = details + deletes,
                DetailsLeaves = details,
                DeleteLeaves = deletes,
                Packages = packages.Count,
                Versions = versions.Count,
                VersionsPresent = present.Count,
                Quads = quads,
            },
        };
    }

    /// <summary>What differs between two manifests, one line each; empty when they agree.</summary>
    public static IReadOnlyList<string> Differences(SnapshotManifest expected, SnapshotManifest actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        List<string> differences = [];
        if (!string.Equals(expected.Sha256, actual.Sha256, StringComparison.Ordinal))
        {
            differences.Add("sha256: " + expected.Sha256 + " committed, " + actual.Sha256 + " rebuilt");
        }

        if (expected.Counts != actual.Counts)
        {
            differences.Add("counts: " + expected.Counts + " committed, " + actual.Counts + " rebuilt");
        }

        Dictionary<string, SnapshotFile> rebuilt = actual.Files.ToDictionary(static file => file.Path, StringComparer.Ordinal);
        foreach (SnapshotFile file in expected.Files)
        {
            if (!rebuilt.Remove(file.Path, out SnapshotFile? other))
            {
                differences.Add(file.Path + ": committed, not rebuilt");
            }
            else if (other != file)
            {
                differences.Add(file.Path + ": " + file.Sha256 + " committed, " + other.Sha256 + " rebuilt");
            }
        }

        differences.AddRange(rebuilt.Keys.Select(static path => path + ": rebuilt, not committed"));
        if (!string.Equals(expected.Tool, actual.Tool, StringComparison.Ordinal))
        {
            differences.Add("tool: " + expected.Tool + " committed, " + actual.Tool + " rebuilt");
        }

        return differences;
    }
}
