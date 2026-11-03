// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

/// <summary>What <see cref="SnapshotBuilder"/> found, before the quads are counted.</summary>
/// <param name="Commits">The retained catalog commits, in catalog order.</param>
/// <param name="Skipped">Catalog commits inside the window that touch no package of the subset.</param>
/// <param name="Pages">The first and last catalog pages read.</param>
/// <param name="RemoteContexts">The remote context documents the leaves name, as recorded.</param>
/// <param name="InlineContexts">The inline contexts the leaves carry.</param>
public sealed record SnapshotDraft(
    IReadOnlyList<CatalogCommit> Commits,
    long Skipped,
    (int First, int Last) Pages,
    IReadOnlyList<RemoteContext> RemoteContexts,
    IReadOnlyList<InlineContext> InlineContexts);

/// <summary>
/// Builds a snapshot (ADR 0008) as a pure function of the id list, the window
/// and the tool version. The catalog is append-only and its leaves immutable,
/// so the same inputs read the same bytes. The snapshot's files:
/// <list type="bullet">
/// <item><c>ids.txt</c>: the id list;</item>
/// <item><c>commits.ndjson</c>: one line per retained catalog commit, in catalog order;</item>
/// <item><c>leaves.ndjson</c>: one line per leaf, in the same order, each the leaf's JSON as nuget.org serves it, minified;</item>
/// <item><c>contexts/</c>: remote context documents the leaves name, if any.</item>
/// </list>
/// </summary>
public sealed class SnapshotBuilder
{
    private const int Parallelism = 32;

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

        // The pages that can hold a commit inside the window. A page's
        // commitTimeStamp is its newest item's, so page i holds items after
        // page i-1's timestamp, up to and including its own.
        using JsonDocument index = JsonDocument.Parse(await _http.GetAsync(CatalogSource.Index, cancellationToken).ConfigureAwait(false));
        List<(int Number, Uri Url, DateTimeOffset Newest)> pages = [.. index.RootElement.GetProperty("items").EnumerateArray()
            .Select(static page => (Number: PageNumber(page.GetProperty("@id").GetString()!), Url: new Uri(page.GetProperty("@id").GetString()!), Newest: SnapshotInputs.Timestamp(page.GetProperty("commitTimeStamp").GetString())))
            .OrderBy(static page => page.Number)];
        List<(int Number, Uri Url, DateTimeOffset Newest)> selected = [];
        for (int i = 0; i < pages.Count; i++)
        {
            bool reachesFrom = pages[i].Newest >= inputs.From;
            bool startsBeforeTo = i == 0 || pages[i - 1].Newest < inputs.To;
            if (reachesFrom && startsBeforeTo)
            {
                selected.Add(pages[i]);
            }
        }

        if (selected.Count == 0 || pages[^1].Newest < inputs.To)
        {
            throw new InvalidDataException("The catalog does not yet reach the window's upper bound " + SnapshotInputs.Format(inputs.To) + "; a snapshot's window must be closed.");
        }

        _log.WriteLine("catalog: " + selected.Count.ToString(CultureInfo.InvariantCulture) + " pages, " + selected[0].Number.ToString(CultureInfo.InvariantCulture) + " to " + selected[^1].Number.ToString(CultureInfo.InvariantCulture));

        ConcurrentBag<(string CommitId, string CommitTimeStamp, CatalogLeafRef Leaf)> items = [];
        ConcurrentDictionary<string, byte> windowCommits = new(StringComparer.Ordinal);
        int read = 0;
        await Parallel.ForEachAsync(selected, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken }, async (page, token) =>
        {
            using JsonDocument document = JsonDocument.Parse(await _http.GetAsync(page.Url, token).ConfigureAwait(false));
            foreach (JsonElement item in document.RootElement.GetProperty("items").EnumerateArray())
            {
                string stamp = item.GetProperty("commitTimeStamp").GetString()!;
                DateTimeOffset at = SnapshotInputs.Timestamp(stamp);
                if (at < inputs.From || at >= inputs.To)
                {
                    continue;
                }

                string commitId = item.GetProperty("commitId").GetString()!;
                windowCommits.TryAdd(commitId, 0);
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
                items.Add((commitId, stamp, new CatalogLeafRef(item.GetProperty("@id").GetString()!, isDelete, id, item.GetProperty("nuget:version").GetString()!)));
            }

            int done = Interlocked.Increment(ref read);
            if (done % 200 == 0)
            {
                _log.WriteLine("catalog: " + done.ToString(CultureInfo.InvariantCulture) + " pages read");
            }
        }).ConfigureAwait(false);

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

        _log.WriteLine("catalog: " + commits.Count.ToString(CultureInfo.InvariantCulture) + " commits retained, " + commits.Sum(static c => c.Leaves.Count).ToString(CultureInfo.InvariantCulture) + " leaves to fetch");

        // Every leaf, fetched once; minified to one line, the JSON unchanged.
        CatalogLeafRef[] allLeaves = [.. commits.SelectMany(static commit => commit.Leaves)];
        ConcurrentDictionary<string, byte[]> bodies = new(StringComparer.Ordinal);
        ConcurrentDictionary<string, string> remote = new(StringComparer.Ordinal);
        ConcurrentDictionary<string, long> inline = new(StringComparer.Ordinal);
        int fetched = 0;
        await Parallel.ForEachAsync(allLeaves.DistinctBy(static leaf => leaf.Url, StringComparer.Ordinal), new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken }, async (leaf, token) =>
        {
            byte[] body = await _http.GetAsync(new Uri(leaf.Url), token).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(body);
            string at = document.RootElement.TryGetProperty("@id", out JsonElement self) ? self.GetString() ?? string.Empty : string.Empty;
            if (!string.Equals(at, leaf.Url, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The leaf at " + leaf.Url + " names itself " + at + ".");
            }

            if (document.RootElement.TryGetProperty("@context", out JsonElement context))
            {
                foreach (JsonElement part in context.ValueKind == JsonValueKind.Array ? [.. context.EnumerateArray()] : new[] { context })
                {
                    if (part.ValueKind == JsonValueKind.String)
                    {
                        remote.TryAdd(part.GetString()!, string.Empty);
                    }
                    else
                    {
                        inline.AddOrUpdate(Sha256Text.Of(Minify(part)), 1, static (_, count) => count + 1);
                    }
                }
            }

            bodies[leaf.Url] = Minify(document.RootElement);
            int done = Interlocked.Increment(ref fetched);
            if (done % 2000 == 0)
            {
                _log.WriteLine("leaves: " + done.ToString(CultureInfo.InvariantCulture) + " fetched");
            }
        }).ConfigureAwait(false);

        // Remote contexts, recorded so the importer never fetches one (ADR 0006).
        List<RemoteContext> remoteContexts = [];
        foreach (string iri in remote.Keys.Order(StringComparer.Ordinal))
        {
            byte[] document = await _http.GetAsync(new Uri(iri), cancellationToken).ConfigureAwait(false);
            string hash = Sha256Text.Of(document);
            string path = "contexts/" + hash + ".jsonld";
            Directory.CreateDirectory(Path.Combine(output, "contexts"));
            await File.WriteAllBytesAsync(Path.Combine(output, path), document, cancellationToken).ConfigureAwait(false);
            remoteContexts.Add(new RemoteContext(iri, path, hash));
        }

        await File.WriteAllTextAsync(Path.Combine(output, "ids.txt"), string.Join('\n', inputs.Ids) + "\n", cancellationToken).ConfigureAwait(false);
        await WriteLinesAsync(Path.Combine(output, "commits.ndjson"), commits.Select(CommitLine), cancellationToken).ConfigureAwait(false);
        await WriteLinesAsync(Path.Combine(output, "leaves.ndjson"), allLeaves.Select(leaf => bodies[leaf.Url]), cancellationToken).ConfigureAwait(false);

        return new SnapshotDraft(
            commits,
            windowCommits.Count - commits.Count,
            (selected[0].Number, selected[^1].Number),
            remoteContexts,
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
            json.WriteStartArray("leaves");
            foreach (CatalogLeafRef leaf in commit.Leaves)
            {
                json.WriteStartObject();
                json.WriteString("type", leaf.IsDelete ? "PackageDelete" : "PackageDetails");
                json.WriteString("id", leaf.Id);
                json.WriteString("version", leaf.Version);
                json.WriteString("url", leaf.Url);
                json.WriteEndObject();
            }

            json.WriteEndArray();
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

    /// <summary>The UTF-8 text of a line, for messages.</summary>
    internal static string Text(byte[] line) => Encoding.UTF8.GetString(line);
}
