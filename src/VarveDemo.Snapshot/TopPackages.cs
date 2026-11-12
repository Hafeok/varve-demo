// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VarveDemo.Snapshot;

/// <summary>
/// The subset's id list (ADR 0008 point 1): the top N package ids by total
/// downloads, from nuget.org's search service, with the query and the time it
/// ran written beside it.
/// </summary>
public static class TopPackages
{
    /// <summary>nuget.org's search service, as its service index names it.</summary>
    public const string SearchService = "https://azuresearch-usnc.nuget.org/query";

    private const int Page = 1000;

    /// <summary>The URL of one page of the query.</summary>
    public static Uri PageUrl(int skip, int take) => new(
        SearchService + "?q=&prerelease=true&semVerLevel=2.0.0&sortBy=totalDownloads-desc&skip="
        + skip.ToString(CultureInfo.InvariantCulture) + "&take=" + take.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Runs the query for the top <paramref name="n"/> ids and writes
    /// <c>ids.txt</c> (one id per line, in rank order) and <c>query.json</c>
    /// into <paramref name="directory"/>.
    /// </summary>
    public static async Task<IReadOnlyList<string>> WriteAsync(CatalogHttp http, int n, string directory, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(n);
        DateTimeOffset ranAt = clock.GetUtcNow();
        List<string> ids = new(n);
        List<(string Id, long Downloads)> ranked = new(n);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        long totalHits = 0;

        for (int skip = 0; skip < n; skip += Page)
        {
            byte[] body = await http.GetAsync(PageUrl(skip, Math.Min(Page, n - skip)), cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(body);
            totalHits = document.RootElement.GetProperty("totalHits").GetInt64();
            foreach (JsonElement package in document.RootElement.GetProperty("data").EnumerateArray())
            {
                string id = package.GetProperty("id").GetString() ?? throw new InvalidDataException("A search result has no id.");
                if (!seen.Add(id))
                {
                    throw new InvalidDataException("The search returned " + id + " twice; the ranking moved while it was read. Run it again.");
                }

                ids.Add(id);
                ranked.Add((id, package.GetProperty("totalDownloads").GetInt64()));
            }
        }

        if (ids.Count != n)
        {
            throw new InvalidDataException("The search returned " + ids.Count.ToString(CultureInfo.InvariantCulture) + " ids, not " + n.ToString(CultureInfo.InvariantCulture) + ".");
        }

        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "ids.txt"), string.Join('\n', ids) + "\n", cancellationToken).ConfigureAwait(false);

        using MemoryStream query = new();
        using (Utf8JsonWriter json = new(query, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteString("service", SearchService);
            json.WriteString("query", PageUrl(0, Page).Query);
            json.WriteString("pagedBy", "skip in steps of " + Page.ToString(CultureInfo.InvariantCulture));
            json.WriteNumber("n", n);
            json.WriteString("ranAt", ranAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            json.WriteNumber("totalHits", totalHits);
            json.WriteString("first", ranked[0].Id);
            json.WriteNumber("firstTotalDownloads", ranked[0].Downloads);
            json.WriteString("last", ranked[^1].Id);
            json.WriteNumber("lastTotalDownloads", ranked[^1].Downloads);
            json.WriteEndObject();
        }

        await File.WriteAllTextAsync(Path.Combine(directory, "query.json"), Encoding.UTF8.GetString(query.ToArray()) + "\n", cancellationToken).ConfigureAwait(false);
        return ids;
    }
}
