// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VarveDemo.Snapshot;

/// <summary>One leaf of a retained commit, with its JSON as the snapshot holds it.</summary>
/// <param name="Ref">The leaf as its catalog page lists it.</param>
/// <param name="Json">The leaf's JSON, one line, UTF-8.</param>
public sealed record SnapshotLeaf(CatalogLeafRef Ref, ReadOnlyMemory<byte> Json);

/// <summary>One package of the base, with its versions' leaves, as a reader replays it.</summary>
/// <param name="Sequence">Its place in the base, from 1.</param>
/// <param name="Id">The package id, lower case.</param>
/// <param name="Leaves">One leaf per version present at the lower bound.</param>
public sealed record SnapshotBasePackage(long Sequence, string Id, IReadOnlyList<SnapshotLeaf> Leaves);

/// <summary>One retained catalog commit, with its leaves' JSON, as a reader replays it.</summary>
/// <param name="Sequence">Its place in the snapshot, from 1.</param>
/// <param name="Commit">The commit as <c>commits.ndjson</c> states it.</param>
/// <param name="Leaves">Its leaves, in order, with their JSON.</param>
public sealed record SnapshotCommit(long Sequence, CatalogCommit Commit, IReadOnlyList<SnapshotLeaf> Leaves);

/// <summary>
/// Reads a snapshot directory in catalog order. <see cref="OpenAsync"/>
/// verifies every file against a manifest before anything is read: a
/// snapshot whose hash differs is refused, never used.
/// </summary>
public sealed class SnapshotReader
{
    private SnapshotReader(string directory, SnapshotManifest manifest)
    {
        Directory = directory;
        Manifest = manifest;
    }

    /// <summary>The snapshot's directory.</summary>
    public string Directory { get; }

    /// <summary>The manifest the snapshot was verified against.</summary>
    public SnapshotManifest Manifest { get; }

    /// <summary>
    /// Verifies <paramref name="directory"/> against <paramref name="manifest"/>:
    /// the same files, each with its stated size and hash. Throws on the first
    /// difference.
    /// </summary>
    public static async Task<SnapshotReader> OpenAsync(string directory, SnapshotManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        IReadOnlyList<SnapshotFile> actual = await SnapshotManifest.HashFilesAsync(directory, cancellationToken).ConfigureAwait(false);
        Verify(manifest.Files, actual);
        return new SnapshotReader(directory, manifest);
    }

    /// <summary>
    /// A reader over a directory just built, before its manifest exists: used
    /// only by the builder to count the quads the manifest will state.
    /// </summary>
    public static SnapshotReader OpenUnverified(string directory, SnapshotManifest draft) => new(directory, draft);

    /// <summary>Throws unless <paramref name="actual"/> is exactly <paramref name="expected"/>.</summary>
    public static void Verify(IReadOnlyList<SnapshotFile> expected, IReadOnlyList<SnapshotFile> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        Dictionary<string, SnapshotFile> found = actual.ToDictionary(static file => file.Path, StringComparer.Ordinal);
        foreach (SnapshotFile file in expected)
        {
            if (!found.Remove(file.Path, out SnapshotFile? present))
            {
                throw new InvalidDataException("The snapshot lacks " + file.Path + ".");
            }

            if (present.Bytes != file.Bytes || !string.Equals(present.Sha256, file.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The snapshot's " + file.Path + " hashes to " + present.Sha256 + " (" + present.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes); the manifest says " + file.Sha256 + " (" + file.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes).");
            }
        }

        if (found.Count > 0)
        {
            throw new InvalidDataException("The snapshot has files the manifest does not list: " + string.Join(", ", found.Keys.Order(StringComparer.Ordinal)) + ".");
        }
    }

    /// <summary>The base, one package at a time, each with its versions' leaves; nothing for a snapshot without one.</summary>
    public async IAsyncEnumerable<SnapshotBasePackage> BaseAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string path = Path.Combine(Directory, "base.ndjson");
        if (Manifest.Base is null || !File.Exists(path))
        {
            yield break;
        }

        using StreamReader packages = new(path);
        using StreamReader leaves = new(Path.Combine(Directory, "base-leaves.ndjson"));
        long sequence = 0;
        while (await packages.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            using JsonDocument document = JsonDocument.Parse(line);
            string id = document.RootElement.GetProperty("id").GetString()!;
            List<SnapshotLeaf> withJson = [];
            foreach (CatalogLeafRef leaf in Leaves(document.RootElement))
            {
                string json = await leaves.ReadLineAsync(cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("base-leaves.ndjson ends before " + id + "'s leaf " + leaf.Url + ".");
                withJson.Add(new SnapshotLeaf(leaf, System.Text.Encoding.UTF8.GetBytes(json)));
            }

            yield return new SnapshotBasePackage(++sequence, id, withJson);
        }

        if (await leaves.ReadLineAsync(cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidDataException("base-leaves.ndjson holds more leaves than base.ndjson lists.");
        }
    }

    /// <summary>The retained commits, in catalog order, each with its leaves' JSON.</summary>
    public async IAsyncEnumerable<SnapshotCommit> CommitsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using StreamReader commits = new(Path.Combine(Directory, "commits.ndjson"));
        using StreamReader leaves = new(Path.Combine(Directory, "leaves.ndjson"));
        long sequence = 0;
        while (await commits.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            CatalogCommit commit = ParseCommit(line);
            List<SnapshotLeaf> withJson = new(commit.Leaves.Count);
            foreach (CatalogLeafRef leaf in commit.Leaves)
            {
                string json = await leaves.ReadLineAsync(cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("leaves.ndjson ends before commit " + commit.CommitId + "'s leaf " + leaf.Url + ".");
                withJson.Add(new SnapshotLeaf(leaf, System.Text.Encoding.UTF8.GetBytes(json)));
            }

            yield return new SnapshotCommit(++sequence, commit, withJson);
        }

        if (await leaves.ReadLineAsync(cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidDataException("leaves.ndjson holds more leaves than commits.ndjson lists.");
        }
    }

    /// <summary>Parses one line of <c>commits.ndjson</c>.</summary>
    public static CatalogCommit ParseCommit(string line)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;
        return new CatalogCommit(root.GetProperty("commitId").GetString()!, root.GetProperty("commitTimeStamp").GetString()!, Leaves(root));
    }

    private static List<CatalogLeafRef> Leaves(JsonElement owner) =>
        [.. owner.GetProperty("leaves").EnumerateArray().Select(static leaf => new CatalogLeafRef(
            leaf.GetProperty("url").GetString()!,
            string.Equals(leaf.GetProperty("type").GetString(), "PackageDelete", StringComparison.Ordinal),
            leaf.GetProperty("id").GetString()!,
            leaf.GetProperty("version").GetString()!))];
}
