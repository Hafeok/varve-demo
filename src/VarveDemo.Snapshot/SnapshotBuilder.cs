// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VarveDemo.Snapshot;

/// <summary>Where the catalog is read from.</summary>
public static class CatalogSource
{
    /// <summary>nuget.org's catalog index (its service index's <c>Catalog/3.0.0</c>).</summary>
    public static readonly Uri Index = new("https://api.nuget.org/v3/catalog0/index.json");
}

/// <summary>One package's base: its versions' latest leaves before the window (ADR 0008's amendment).</summary>
/// <param name="Id">The package id, lower case.</param>
/// <param name="Leaves">One leaf per version present at the window's lower bound, by version, lower case and ordinal.</param>
public sealed record BasePackage(string Id, IReadOnlyList<CatalogLeafRef> Leaves);

/// <summary>What <see cref="SnapshotBuilder"/> found, before the quads are counted.</summary>
/// <param name="Base">The base, one entry per package with a version present at the lower bound; empty without a base.</param>
/// <param name="DeletedBeforeFrom">Versions whose latest leaf before the lower bound is a delete, so absent from the base.</param>
/// <param name="Commits">The retained catalog commits, in catalog order.</param>
/// <param name="Skipped">Catalog commits inside the window that touch no package of the subset.</param>
/// <param name="Pages">The first and last catalog pages read.</param>
/// <param name="InlineContexts">The inline contexts the leaves carry.</param>
public sealed record SnapshotDraft(
    IReadOnlyList<BasePackage> Base,
    long DeletedBeforeFrom,
    IReadOnlyList<CatalogCommit> Commits,
    long Skipped,
    (int First, int Last) Pages,
    IReadOnlyList<InlineContext> InlineContexts);

/// <summary>
/// Builds a snapshot (ADR 0008 and its amendment) as a pure function of the id
/// list, the window and the tool version. The catalog is append-only and its
/// leaves immutable, so the same inputs read the same bytes. The files:
/// <list type="bullet">
/// <item><c>ids.txt</c>: the id list;</item>
/// <item><c>base.ndjson</c> and <c>base-leaves.ndjson</c>: per package, the latest leaf of each version before the lower bound;</item>
/// <item><c>commits.ndjson</c> and <c>leaves.ndjson</c>: the retained catalog commits inside the window, in catalog order;</item>
/// </list>
/// one leaf per line: a base leaf as <see cref="LeafProjection"/> keeps it, a window leaf whole.
/// </summary>
public sealed class SnapshotBuilder
{
    private const int Parallelism = 64;

    private readonly CatalogHttp _http;
    private readonly TextWriter _log;

    /// <summary>A builder reading through <paramref name="http"/>, reporting progress to <paramref name="log"/>.</summary>
    public SnapshotBuilder(CatalogHttp http, TextWriter log)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Reads the catalog and writes the snapshot's files into <paramref name="output"/>, which must be empty or absent.</summary>
    public async Task<SnapshotDraft> BuildAsync(SnapshotInputs inputs, string output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            throw new IOException(output + " is not empty; a snapshot is built into an empty directory.");
        }

        Directory.CreateDirectory(output);
        HashSet<string> subset = new(inputs.Ids, StringComparer.OrdinalIgnoreCase);

        // A page's commitTimeStamp is its newest item's, so page i holds items
        // after page i-1's timestamp, up to and including its own. The window
        // needs the pages that can reach it; the base needs every page before.
        using JsonDocument index = JsonDocument.Parse(await _http.GetAsync(CatalogSource.Index, cancellationToken).ConfigureAwait(false));
        List<(int Number, Uri Url, DateTimeOffset Newest)> pages = [.. index.RootElement.GetProperty("items").EnumerateArray()
            .Select(static page => (Number: PageNumber(page.GetProperty("@id").GetString()!), Url: new Uri(page.GetProperty("@id").GetString()!), Newest: SnapshotInputs.Timestamp(page.GetProperty("commitTimeStamp").GetString())))
            .OrderBy(static page => page.Number)];
        if (pages.Count == 0 || pages[^1].Newest < inputs.To)
        {
            throw new InvalidDataException("The catalog does not yet reach the window's upper bound " + SnapshotInputs.Format(inputs.To) + "; a snapshot's window must be closed.");
        }

        List<(int Number, Uri Url, DateTimeOffset Newest)> selected = [];
        for (int i = 0; i < pages.Count; i++)
        {
            bool reachesFrom = inputs.Base || pages[i].Newest >= inputs.From;
            bool startsBeforeTo = i == 0 || pages[i - 1].Newest < inputs.To;
            if (reachesFrom && startsBeforeTo)
            {
                selected.Add(pages[i]);
            }
        }

        _log.WriteLine("catalog: " + selected.Count.ToString(CultureInfo.InvariantCulture) + " pages, " + selected[0].Number.ToString(CultureInfo.InvariantCulture) + " to " + selected[^1].Number.ToString(CultureInfo.InvariantCulture));

        ConcurrentBag<(string CommitId, string CommitTimeStamp, CatalogLeafRef Leaf)> items = [];
        ConcurrentDictionary<string, byte> windowCommits = new(StringComparer.Ordinal);
        ConcurrentDictionary<(string Id, string Version), (DateTimeOffset At, CatalogLeafRef Leaf)> latest = new();
        int read = 0;
        await Parallel.ForEachAsync(selected, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken }, async (page, token) =>
        {
            using JsonDocument document = JsonDocument.Parse(await _http.GetAsync(page.Url, token).ConfigureAwait(false));
            foreach (JsonElement item in document.RootElement.GetProperty("items").EnumerateArray())
            {
                string stamp = item.GetProperty("commitTimeStamp").GetString()!;
                DateTimeOffset at = SnapshotInputs.Timestamp(stamp);
                if (at >= inputs.To || (!inputs.Base && at < inputs.From))
                {
                    continue;
                }

                string commitId = item.GetProperty("commitId").GetString()!;
                if (at >= inputs.From)
                {
                    windowCommits.TryAdd(commitId, 0);
                }

                string id = item.GetProperty("nuget:id").GetString()!;
                if (!subset.Contains(id))
                {
                    continue;
                }

                string type = item.GetProperty("@type").GetString()!;
                bool isDelete = type switch
                {
                    "nuget:PackageDetails" => false,
                    "nuget:PackageDelete" => true,
                    _ => throw new InvalidDataException("Catalog item of an unknown type " + type + " in " + page.Url),
                };
                CatalogLeafRef leaf = new(item.GetProperty("@id").GetString()!, isDelete, id, item.GetProperty("nuget:version").GetString()!);
                if (at >= inputs.From)
                {
                    items.Add((commitId, stamp, leaf));
                }
                else
                {
                    // The latest leaf before the lower bound; ties by URL, so the choice is a function of the catalog.
                    latest.AddOrUpdate(
                        (id.ToLowerInvariant(), leaf.Version.ToLowerInvariant()),
                        (at, leaf),
                        (_, held) => at > held.At || (at == held.At && string.CompareOrdinal(leaf.Url, held.Leaf.Url) > 0) ? (at, leaf) : held);
                }
            }

            int done = Interlocked.Increment(ref read);
            if (done % 1000 == 0)
            {
                _log.WriteLine("catalog: " + done.ToString(CultureInfo.InvariantCulture) + " pages read");
            }
        }).ConfigureAwait(false);

        List<BasePackage> basePackages = [.. latest
            .Where(static pair => !pair.Value.Leaf.IsDelete)
            .Where(pair => inputs.BaseSample is not { } sample || sample.Takes(pair.Value.Leaf.Url, pair.Key.Id))
            .GroupBy(static pair => pair.Key.Id, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new BasePackage(group.Key, [.. group.OrderBy(static pair => pair.Key.Version, StringComparer.Ordinal).Select(static pair => pair.Value.Leaf)]))];
        long deletedBeforeFrom = latest.Count(static pair => pair.Value.Leaf.IsDelete);

        List<CatalogCommit> commits = [.. items
            .GroupBy(static item => item.CommitId, StringComparer.Ordinal)
            .Select(static group =>
            {
                string[] stamps = [.. group.Select(static item => item.CommitTimeStamp).Distinct(StringComparer.Ordinal)];
                if (stamps.Length != 1)
                {
                    throw new InvalidDataException("Catalog commit " + group.Key + " has " + stamps.Length.ToString(CultureInfo.InvariantCulture) + " timestamps.");
                }

                List<CatalogLeafRef> leaves = [.. group.Select(static item => item.Leaf)];
                leaves.Sort(CatalogLeafRef.Compare);
                return new CatalogCommit(group.Key, stamps[0], leaves);
            })
            .OrderBy(static commit => SnapshotInputs.Timestamp(commit.CommitTimeStamp))
            .ThenBy(static commit => commit.CommitId, StringComparer.Ordinal)];

        if (commits.Count == 0)
        {
            throw new InvalidDataException("No catalog commit inside the window touches the subset.");
        }

        CatalogLeafRef[] baseLeaves = [.. basePackages.SelectMany(static package => package.Leaves)];
        CatalogLeafRef[] windowLeaves = [.. commits.SelectMany(static commit => commit.Leaves)];
        _log.WriteLine("catalog: base " + basePackages.Count.ToString(CultureInfo.InvariantCulture) + " packages, " + baseLeaves.Length.ToString(CultureInfo.InvariantCulture)
            + " versions; window " + commits.Count.ToString(CultureInfo.InvariantCulture) + " commits, " + windowLeaves.Length.ToString(CultureInfo.InvariantCulture) + " leaves");

        // Every leaf, fetched once: a base leaf kept as its projection, a window leaf whole.
        HashSet<string> baseUrls = new(baseLeaves.Select(static leaf => leaf.Url), StringComparer.Ordinal);
        HashSet<string> windowUrls = new(windowLeaves.Select(static leaf => leaf.Url), StringComparer.Ordinal);
        ConcurrentDictionary<string, byte[]> bodies = new(StringComparer.Ordinal);
        ConcurrentDictionary<string, long> inline = new(StringComparer.Ordinal);
        int fetched = 0;
        IEnumerable<CatalogLeafRef> toFetch = baseLeaves.Concat(windowLeaves).Where(static leaf => !leaf.IsDelete).DistinctBy(static leaf => leaf.Url, StringComparer.Ordinal);
        await Parallel.ForEachAsync(toFetch, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken }, async (leaf, token) =>
        {
            byte[] body = await _http.GetAsync(new Uri(leaf.Url), token).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(body);
            string at = document.RootElement.TryGetProperty("@id", out JsonElement self) ? self.GetString() ?? string.Empty : string.Empty;
            if (!string.Equals(at, leaf.Url, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The leaf at " + leaf.Url + " names itself " + at + ".");
            }

            JsonElement context = LeafProjection.InlineContext(document.RootElement, leaf.Url);
            inline.AddOrUpdate(Sha256Text.Of(Minify(context)), 1, static (_, count) => count + 1);
            bodies[leaf.Url] = baseUrls.Contains(leaf.Url) && !windowUrls.Contains(leaf.Url)
                ? LeafProjection.Project(document.RootElement, leaf.Url)
                : Minify(document.RootElement);
            int done = Interlocked.Increment(ref fetched);
            if (done % 20000 == 0)
            {
                _log.WriteLine("leaves: " + done.ToString(CultureInfo.InvariantCulture) + " fetched");
            }
        }).ConfigureAwait(false);

        // A delete leaf carries nothing the import reads beyond what its page
        // states; it is kept as a one-member object naming it.
        byte[] Body(CatalogLeafRef leaf) => leaf.IsDelete ? DeleteLine(leaf.Url) : bodies[leaf.Url];

        await File.WriteAllTextAsync(Path.Combine(output, "ids.txt"), string.Join('\n', inputs.Ids) + "\n", cancellationToken).ConfigureAwait(false);
        if (inputs.Base)
        {
            await WriteLinesAsync(Path.Combine(output, "base.ndjson"), basePackages.Select(BaseLine), cancellationToken).ConfigureAwait(false);
            await WriteLinesAsync(Path.Combine(output, "base-leaves.ndjson"), baseLeaves.Select(Body), cancellationToken).ConfigureAwait(false);
        }

        await WriteLinesAsync(Path.Combine(output, "commits.ndjson"), commits.Select(CommitLine), cancellationToken).ConfigureAwait(false);
        await WriteLinesAsync(Path.Combine(output, "leaves.ndjson"), windowLeaves.Select(Body), cancellationToken).ConfigureAwait(false);

        return new SnapshotDraft(
            basePackages,
            deletedBeforeFrom,
            commits,
            windowCommits.Count - commits.Count,
            (selected[0].Number, selected[^1].Number),
            [.. inline.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair => new InlineContext(pair.Key, pair.Value))]);
    }

    /// <summary>A JSON value written without whitespace; the same value always gives the same bytes.</summary>
    public static byte[] Minify(JsonElement element)
    {
        using MemoryStream output = new();
        using (Utf8JsonWriter json = new(output, MinifiedOptions()))
        {
            element.WriteTo(json);
        }

        return output.ToArray();
    }

    /// <summary>One line of <c>commits.ndjson</c>.</summary>
    public static byte[] CommitLine(CatalogCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        using MemoryStream output = new();
        using (Utf8JsonWriter json = new(output, MinifiedOptions()))
        {
            json.WriteStartObject();
            json.WriteString("commitId", commit.CommitId);
            json.WriteString("commitTimeStamp", commit.CommitTimeStamp);
            WriteLeaves(json, commit.Leaves);
            json.WriteEndObject();
        }

        return output.ToArray();
    }

    /// <summary>One line of <c>base.ndjson</c>.</summary>
    public static byte[] BaseLine(BasePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        using MemoryStream output = new();
        using (Utf8JsonWriter json = new(output, MinifiedOptions()))
        {
            json.WriteStartObject();
            json.WriteString("id", package.Id);
            WriteLeaves(json, package.Leaves);
            json.WriteEndObject();
        }

        return output.ToArray();
    }

    private static void WriteLeaves(Utf8JsonWriter json, IReadOnlyList<CatalogLeafRef> leaves)
    {
        json.WriteStartArray("leaves");
        foreach (CatalogLeafRef leaf in leaves)
        {
            json.WriteStartObject();
            json.WriteString("type", leaf.IsDelete ? "PackageDelete" : "PackageDetails");
            json.WriteString("id", leaf.Id);
            json.WriteString("version", leaf.Version);
            json.WriteString("url", leaf.Url);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    private static byte[] DeleteLine(string url)
    {
        using MemoryStream output = new();
        using (Utf8JsonWriter json = new(output, MinifiedOptions()))
        {
            json.WriteStartObject();
            json.WriteString("@id", url);
            json.WriteEndObject();
        }

        return output.ToArray();
    }

    private static JsonWriterOptions MinifiedOptions() => new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static int PageNumber(string url)
    {
        string name = url[(url.LastIndexOf("page", StringComparison.Ordinal) + 4)..];
        return int.Parse(name.AsSpan(0, name.IndexOf('.', StringComparison.Ordinal)), NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static async Task WriteLinesAsync(string path, IEnumerable<byte[]> lines, CancellationToken cancellationToken)
    {
        FileStream file = File.Create(path);
        await using (file.ConfigureAwait(false))
        {
            foreach (byte[] line in lines)
            {
                await file.WriteAsync(line, cancellationToken).ConfigureAwait(false);
                file.WriteByte((byte)'\n');
            }
        }
    }
}
