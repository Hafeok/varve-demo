// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VarveDemo.Snapshot;

/// <summary>The base's counts (ADR 0008's amendment): the catalog's state of the N packages at the window's lower bound.</summary>
public sealed record BaseCounts
{
    /// <summary>Packages with at least one version present: one Varve commit each.</summary>
    public required long Packages { get; init; }

    /// <summary>Versions present, each its latest leaf before the lower bound.</summary>
    public required long Versions { get; init; }

    /// <summary>Versions whose latest leaf before the lower bound is a delete, so absent.</summary>
    public required long DeletedBefore { get; init; }

    /// <summary>Quads the importer sends for the base.</summary>
    public required long Quads { get; init; }

    /// <summary>The hash over the base's files, as <see cref="SnapshotManifest.HashList"/> takes it.</summary>
    public required string Sha256 { get; init; }

    /// <summary>What the base's files keep of each leaf (<see cref="LeafProjection.Rule"/>).</summary>
    public required string LeafRule { get; init; }

    /// <summary>For a projection only: the sample taken, as "percent% seed s"; null for the whole base.</summary>
    public string? Sample { get; init; }
}

/// <summary>The window's counts a snapshot's manifest states (ADR 0008).</summary>
public sealed record SnapshotCounts
{
    /// <summary>Catalog commits inside the window that touch the subset: one Varve commit each.</summary>
    public required long CatalogCommitsRetained { get; init; }

    /// <summary>Catalog commits inside the window that touch no package of the subset.</summary>
    public required long CatalogCommitsSkipped { get; init; }

    /// <summary>Leaves of the retained commits, details and deletes.</summary>
    public required long Leaves { get; init; }

    /// <summary><c>PackageDetails</c> leaves.</summary>
    public required long DetailsLeaves { get; init; }

    /// <summary><c>PackageDelete</c> leaves.</summary>
    public required long DeleteLeaves { get; init; }

    /// <summary>Distinct package ids the leaves touch, case-insensitively.</summary>
    public required long Packages { get; init; }

    /// <summary>Distinct package versions the leaves touch.</summary>
    public required long Versions { get; init; }

    /// <summary>Package versions whose graph exists after the last commit: touched, and not deleted last.</summary>
    public required long VersionsPresent { get; init; }

    /// <summary>Quads the importer sends, inserted, over the window's retained commits: the window's rate's numerator (S1 criterion 5).</summary>
    public required long Quads { get; init; }
}

/// <summary>An inline context, by the hash of its minified JSON, and how many leaves carry it.</summary>
/// <param name="Sha256">The hash of the context object as the snapshot writes it.</param>
/// <param name="Leaves">The leaves that carry it.</param>
public sealed record InlineContext(string Sha256, long Leaves);

/// <summary>One file of the snapshot, by path, size and hash.</summary>
/// <param name="Path">Relative to the snapshot's directory, with <c>/</c>.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="Sha256">The hash of its bytes.</param>
public sealed record SnapshotFile(string Path, long Bytes, string Sha256);

/// <summary>
/// The manifest of a snapshot (ADR 0008): its parameters, its bounds, its
/// counts, its context documents, a hash for each file, and one hash over the
/// whole. Committed at <c>snapshot/manifest.json</c>; CI rebuilds the snapshot
/// and compares.
/// </summary>
public sealed record SnapshotManifest
{
    /// <summary>The version of this manifest's form.</summary>
    public const int CurrentFormat = 2;

    /// <summary>The files that hold the base.</summary>
    public static readonly ImmutableArray<string> BaseFiles = ["base.ndjson", "base-leaves.ndjson"];

    /// <summary>The form of the manifest.</summary>
    public int Format { get; init; } = CurrentFormat;

    /// <summary>The tool and mapping version the snapshot was built with.</summary>
    public required string Tool { get; init; }

    /// <summary>The number of top package ids.</summary>
    public required int N { get; init; }

    /// <summary>The hash of the id list.</summary>
    public required string IdsSha256 { get; init; }

    /// <summary>The window's lower bound, inclusive, on catalog commit time.</summary>
    public required DateTimeOffset From { get; init; }

    /// <summary>The window's upper bound, exclusive: the snapshot date.</summary>
    public required DateTimeOffset To { get; init; }

    /// <summary>The first and last catalog pages read.</summary>
    public required (int First, int Last) Pages { get; init; }

    /// <summary>The first and last retained catalog commits, by commit id.</summary>
    public required (string First, string Last) Commits { get; init; }

    /// <summary>The window's counts.</summary>
    public required SnapshotCounts Counts { get; init; }

    /// <summary>The base's counts, or null for a snapshot without a base (<c>import --since</c>).</summary>
    public required BaseCounts? Base { get; init; }

    /// <summary>Inline contexts the leaves carry.</summary>
    public required IReadOnlyList<InlineContext> InlineContexts { get; init; }

    /// <summary>Every file of the snapshot but the manifest.</summary>
    public required IReadOnlyList<SnapshotFile> Files { get; init; }

    /// <summary>One hash over the whole snapshot: of <see cref="HashList"/>.</summary>
    public string Sha256 => Sha256Text.Of(Encoding.UTF8.GetBytes(HashList(Files)));

    /// <summary>The list the whole-snapshot hash is taken over: <c>sha256sum</c>'s lines, sorted by path.</summary>
    public static string HashList(IEnumerable<SnapshotFile> files)
    {
        StringBuilder list = new();
        foreach (SnapshotFile file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            list.Append(file.Sha256).Append("  ").Append(file.Path).Append('\n');
        }

        return list.ToString();
    }

    /// <summary>The files of a snapshot directory, hashed, the manifest excluded.</summary>
    public static async Task<IReadOnlyList<SnapshotFile>> HashFilesAsync(string directory, CancellationToken cancellationToken)
    {
        List<SnapshotFile> files = [];
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string relative = System.IO.Path.GetRelativePath(directory, path).Replace('\\', '/');
            if (string.Equals(relative, "manifest.json", StringComparison.Ordinal))
            {
                continue;
            }

            files.Add(new SnapshotFile(relative, new FileInfo(path).Length, await Sha256Text.OfFileAsync(path, cancellationToken).ConfigureAwait(false)));
        }

        return [.. files.OrderBy(static file => file.Path, StringComparer.Ordinal)];
    }

    /// <summary>The manifest as indented JSON, with a final newline.</summary>
    public byte[] ToJson()
    {
        using MemoryStream output = new();
        using (Utf8JsonWriter json = new(output, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteNumber("format", Format);
            json.WriteString("tool", Tool);
            json.WriteString("sha256", Sha256);
            json.WriteNumber("n", N);
            json.WriteString("idsSha256", IdsSha256);
            json.WriteStartObject("window");
            json.WriteString("from", SnapshotInputs.Format(From));
            json.WriteString("to", SnapshotInputs.Format(To));
            json.WriteEndObject();
            json.WriteStartObject("catalog");
            json.WriteString("index", CatalogSource.Index.ToString());
            json.WriteNumber("firstPage", Pages.First);
            json.WriteNumber("lastPage", Pages.Last);
            json.WriteString("firstCommit", Commits.First);
            json.WriteString("lastCommit", Commits.Last);
            json.WriteEndObject();
            json.WriteStartObject("counts");
            json.WriteNumber("catalogCommitsRetained", Counts.CatalogCommitsRetained);
            json.WriteNumber("catalogCommitsSkipped", Counts.CatalogCommitsSkipped);
            json.WriteNumber("leaves", Counts.Leaves);
            json.WriteNumber("detailsLeaves", Counts.DetailsLeaves);
            json.WriteNumber("deleteLeaves", Counts.DeleteLeaves);
            json.WriteNumber("packages", Counts.Packages);
            json.WriteNumber("versions", Counts.Versions);
            json.WriteNumber("versionsPresent", Counts.VersionsPresent);
            json.WriteNumber("quads", Counts.Quads);
            json.WriteEndObject();
            if (Base is { } baseCounts)
            {
                json.WriteStartObject("base");
                json.WriteString("asOf", SnapshotInputs.Format(From));
                json.WriteNumber("packages", baseCounts.Packages);
                json.WriteNumber("versions", baseCounts.Versions);
                json.WriteNumber("deletedBefore", baseCounts.DeletedBefore);
                json.WriteNumber("quads", baseCounts.Quads);
                json.WriteString("sha256", baseCounts.Sha256);
                json.WriteString("leafRule", baseCounts.LeafRule);
                if (baseCounts.Sample is { } sample)
                {
                    json.WriteString("sample", sample);
                }

                json.WriteEndObject();
            }

            // Every context is inline; a leaf with a remote one fails the build (ADR 0006).
            json.WriteStartObject("contexts");
            json.WriteStartArray("inline");
            foreach (InlineContext context in InlineContexts)
            {
                json.WriteStartObject();
                json.WriteString("sha256", context.Sha256);
                json.WriteNumber("leaves", context.Leaves);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteStartArray("files");
            foreach (SnapshotFile file in Files)
            {
                json.WriteStartObject();
                json.WriteString("path", file.Path);
                json.WriteNumber("bytes", file.Bytes);
                json.WriteString("sha256", file.Sha256);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        output.WriteByte((byte)'\n');
        return output.ToArray();
    }

    /// <summary>Reads a manifest written by <see cref="ToJson"/>, and checks its stated hash against its files.</summary>
    public static SnapshotManifest Read(ReadOnlySpan<byte> utf8)
    {
        Utf8JsonReader reader = new(utf8);
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        int format = root.GetProperty("format").GetInt32();
        if (format != CurrentFormat)
        {
            throw new InvalidDataException("The manifest's format is " + format.ToString(System.Globalization.CultureInfo.InvariantCulture) + "; this tool reads " + CurrentFormat.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        JsonElement catalog = root.GetProperty("catalog");
        JsonElement counts = root.GetProperty("counts");
        JsonElement contexts = root.GetProperty("contexts");
        SnapshotManifest manifest = new()
        {
            Format = format,
            Tool = Text(root, "tool"),
            N = root.GetProperty("n").GetInt32(),
            IdsSha256 = Text(root, "idsSha256"),
            From = SnapshotInputs.Timestamp(Text(root.GetProperty("window"), "from")),
            To = SnapshotInputs.Timestamp(Text(root.GetProperty("window"), "to")),
            Pages = (catalog.GetProperty("firstPage").GetInt32(), catalog.GetProperty("lastPage").GetInt32()),
            Commits = (Text(catalog, "firstCommit"), Text(catalog, "lastCommit")),
            Counts = new SnapshotCounts
            {
                CatalogCommitsRetained = counts.GetProperty("catalogCommitsRetained").GetInt64(),
                CatalogCommitsSkipped = counts.GetProperty("catalogCommitsSkipped").GetInt64(),
                Leaves = counts.GetProperty("leaves").GetInt64(),
                DetailsLeaves = counts.GetProperty("detailsLeaves").GetInt64(),
                DeleteLeaves = counts.GetProperty("deleteLeaves").GetInt64(),
                Packages = counts.GetProperty("packages").GetInt64(),
                Versions = counts.GetProperty("versions").GetInt64(),
                VersionsPresent = counts.GetProperty("versionsPresent").GetInt64(),
                Quads = counts.GetProperty("quads").GetInt64(),
            },
            Base = root.TryGetProperty("base", out JsonElement b)
                ? new BaseCounts
                {
                    Packages = b.GetProperty("packages").GetInt64(),
                    Versions = b.GetProperty("versions").GetInt64(),
                    DeletedBefore = b.GetProperty("deletedBefore").GetInt64(),
                    Quads = b.GetProperty("quads").GetInt64(),
                    Sha256 = Text(b, "sha256"),
                    LeafRule = Text(b, "leafRule"),
                    Sample = b.TryGetProperty("sample", out JsonElement sample) ? sample.GetString() : null,
                }
                : null,
            InlineContexts = [.. contexts.GetProperty("inline").EnumerateArray().Select(static c => new InlineContext(Text(c, "sha256"), c.GetProperty("leaves").GetInt64()))],
            Files = [.. root.GetProperty("files").EnumerateArray().Select(static f => new SnapshotFile(Text(f, "path"), f.GetProperty("bytes").GetInt64(), Text(f, "sha256")))],
        };

        string stated = Text(root, "sha256");
        if (!string.Equals(stated, manifest.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The manifest states the snapshot's hash as " + stated + ", but its file list hashes to " + manifest.Sha256 + ".");
        }

        return manifest;
    }

    private static string Text(JsonElement element, string name) =>
        element.GetProperty(name).GetString() ?? throw new InvalidDataException("The manifest's " + name + " is null.");
}
