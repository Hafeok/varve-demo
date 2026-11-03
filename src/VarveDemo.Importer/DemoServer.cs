// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Varve.Protocol.Client;

namespace VarveDemo.Importer;

/// <summary>
/// The Varve server the demo talks to, through Varve's public HTTP API and
/// <c>Varve.Protocol.Client</c> only (C3): readiness, the service
/// description's <c>maxAsOfDistance</c>, and a <see cref="SparqlHttpClient"/>
/// per client.
/// </summary>
public sealed partial class DemoServer
{
    private readonly HttpClient _http;

    /// <summary>A server at <paramref name="server"/> serving <paramref name="dataset"/>.</summary>
    public DemoServer(HttpClient http, Uri server, string dataset)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrEmpty(dataset);
        Server = new Uri(server.ToString().TrimEnd('/') + "/");
        Dataset = dataset;
        DatasetUri = new Uri(Server, "datasets/" + Uri.EscapeDataString(dataset) + "/");
    }

    /// <summary>The server's base address.</summary>
    public Uri Server { get; }

    /// <summary>The dataset's name.</summary>
    public string Dataset { get; }

    /// <summary>The dataset's address, as <see cref="SparqlHttpClient"/> takes it.</summary>
    public Uri DatasetUri { get; }

    /// <summary>A protocol client for the dataset, authenticated by <paramref name="tokens"/>.</summary>
    public SparqlHttpClient Client(ClientCredentials tokens, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return new SparqlHttpClient(_http, DatasetUri, new ClientLimits(timeout, 1L << 30), tokens.Provider, TimeProvider.System);
    }

    /// <summary>
    /// Waits until <c>GET /health/ready</c> reports the dataset open and
    /// ready, which is after the setup service applied its declaration and
    /// grants. Throws when <paramref name="timeout"/> passes first.
    /// </summary>
    public async Task WaitReadyAsync(TimeSpan timeout, TextWriter log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        DateTimeOffset until = DateTimeOffset.UtcNow + timeout;
        string last = "no answer";
        while (DateTimeOffset.UtcNow < until)
        {
            try
            {
                using HttpResponseMessage response = await _http.GetAsync(new Uri(Server, "health/ready"), cancellationToken).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("datasets", out JsonElement datasets)
                    && datasets.TryGetProperty(Dataset, out JsonElement state)
                    && string.Equals(state.GetProperty("state").GetString(), "ready", StringComparison.Ordinal))
                {
                    return;
                }

                last = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + body;
            }
            catch (HttpRequestException error)
            {
                last = error.Message;
            }
            catch (JsonException error)
            {
                last = error.Message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }

        await log.WriteLineAsync("server: not ready after " + timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " s; last answer: " + last).ConfigureAwait(false);
        throw new TimeoutException("The dataset " + Dataset + " was not ready at " + Server + " within " + timeout + ".");
    }

    /// <summary>
    /// The <c>varve:maxAsOfDistance</c> the dataset's service description
    /// advertises (Varve's <c>ServiceDescriptionEndpoint</c>; ADR 0004 point 6),
    /// read with a token from <paramref name="tokens"/>.
    /// </summary>
    public async Task<long> MaxAsOfDistanceAsync(ClientCredentials tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        Varve.Protocol.Client.Model.BearerToken token = await tokens.GetAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("No token for " + tokens.ClientId + ".");
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(DatasetUri, "sparql"));
        request.Headers.TryAddWithoutValidation("Accept", "application/n-triples");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Value);
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Match match = MaxAsOfDistance().Match(body);
        return match.Success
            ? long.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture)
            : throw new InvalidDataException("The service description at " + request.RequestUri + " (" + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + ") states no varve:maxAsOfDistance.");
    }

    [GeneratedRegex(@"<https://w3id\.org/varve/ns#maxAsOfDistance>\s+""(?<n>\d+)""", RegexOptions.CultureInvariant)]
    private static partial Regex MaxAsOfDistance();
}
