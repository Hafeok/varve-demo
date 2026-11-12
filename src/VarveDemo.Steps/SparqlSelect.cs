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

namespace VarveDemo.Steps;

/// <summary>One solution: each bound variable's value, as SPARQL results JSON gives it.</summary>
public sealed class Solution
{
    private readonly Dictionary<string, string> _values;

    internal Solution(Dictionary<string, string> values) => _values = values;

    /// <summary>The value bound to <paramref name="variable"/>, or null.</summary>
    public string? this[string variable] => _values.TryGetValue(variable, out string? value) ? value : null;
}

/// <summary>A SELECT's answer, its position and how long it took.</summary>
/// <param name="Solutions">The solutions, in the order the server gave them.</param>
/// <param name="Position">The response's <c>Varve-Position</c>.</param>
/// <param name="Elapsed">From the request to the last byte of the response.</param>
public sealed record SelectAnswer(IReadOnlyList<Solution> Solutions, long Position, TimeSpan Elapsed);

/// <summary>SELECT queries over the protocol, with the query text kept so a step can show it (S2 criterion 1).</summary>
public static class SparqlSelect
{
    /// <summary>A query this assembly carries in <c>Queries/</c>.</summary>
    public static string Load(string name)
    {
        using Stream stream = typeof(SparqlSelect).Assembly.GetManifestResourceStream("queries/" + name)
            ?? throw new FileNotFoundException("No embedded query " + name + ".");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Runs <paramref name="query"/> and reads the whole answer.</summary>
    public static async Task<SelectAnswer> RunAsync(SparqlHttpClient client, string query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        Stopwatch clock = Stopwatch.StartNew();
        using HttpResponseMessage response = await client.QueryAsync(query, "application/sparql-results+json", null, cancellationToken).ConfigureAwait(false);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        TimeSpan elapsed = clock.Elapsed;
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("The query was refused: " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + System.Text.Encoding.UTF8.GetString(body));
        }

        long position = SparqlHttpClient.PositionOf(response) ?? throw new InvalidDataException("The answer carries no Varve-Position.");
        using JsonDocument document = JsonDocument.Parse(body);
        List<Solution> solutions = [];
        foreach (JsonElement binding in document.RootElement.GetProperty("results").GetProperty("bindings").EnumerateArray())
        {
            Dictionary<string, string> values = new(StringComparer.Ordinal);
            foreach (JsonProperty variable in binding.EnumerateObject())
            {
                values[variable.Name] = variable.Value.GetProperty("value").GetString() ?? string.Empty;
            }

            solutions.Add(new Solution(values));
        }

        return new SelectAnswer(solutions, position, elapsed);
    }
}
