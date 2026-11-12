// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Varve.Protocol.Client;
using Varve.Protocol.Client.Model;

namespace VarveDemo.Importer;

/// <summary>
/// Where the issuer is (ADR 0007). <paramref name="Issuer"/> is its identity,
/// the <c>iss</c> its tokens carry and the server's <c>Auth:Authority</c>.
/// <paramref name="Address"/>, when set, is where this process reaches it, for
/// a process outside the network the server shares with the issuer (the tool
/// on a viewer's machine): requests go to the address, and carry the issuer's
/// host, so the issuer names itself as the server expects.
/// </summary>
/// <param name="Issuer">The issuer's URL, exactly as the server is configured with it.</param>
/// <param name="Address">Where to connect to it; the issuer itself when null.</param>
public sealed record IssuerEndpoint(Uri Issuer, Uri? Address)
{
    /// <summary>The URL to send a request for <paramref name="issuerUrl"/> to.</summary>
    public Uri Route(Uri issuerUrl)
    {
        ArgumentNullException.ThrowIfNull(issuerUrl);
        if (Address is null)
        {
            return issuerUrl;
        }

        UriBuilder routed = new(issuerUrl) { Scheme = Address.Scheme, Host = Address.Host, Port = Address.Port };
        return routed.Uri;
    }

    /// <summary>A request to <paramref name="issuerUrl"/>, routed, with the issuer's host.</summary>
    public HttpRequestMessage Request(HttpMethod method, Uri issuerUrl)
    {
        HttpRequestMessage request = new(method, Route(issuerUrl));
        if (Address is not null)
        {
            request.Headers.Host = Issuer.IsDefaultPort ? Issuer.Host : Issuer.Host + ":" + Issuer.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return request;
    }
}

/// <summary>
/// Bearer tokens by the client credentials grant (ADR 0007): discovery, then
/// the token endpoint, cached until a minute before expiry. There is no other
/// way to get a token here, and no code path sends a request without one.
/// </summary>
public sealed class ClientCredentials : IDisposable
{
    private static readonly TimeSpan s_skew = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly IssuerEndpoint _issuer;
    private readonly string _clientSecret;
    private readonly string? _scope;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Uri? _tokenEndpoint;
    private BearerToken? _current;

    /// <summary>A token source for one client.</summary>
    public ClientCredentials(HttpClient http, IssuerEndpoint issuer, string clientId, string clientSecret, string? scope, TimeProvider clock)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        _scope = scope;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>The client's id, which is also its token's subject with the mock issuer.</summary>
    public string ClientId { get; }

    /// <summary>This source as the provider <c>SparqlHttpClient</c> calls before each request.</summary>
    public BearerTokenProvider Provider => GetAsync;

    /// <summary>A token valid for at least another minute.</summary>
    public async ValueTask<BearerToken?> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_current is { } held && held.ExpiresOn - s_skew > _clock.GetUtcNow())
            {
                return held;
            }

            _tokenEndpoint ??= await DiscoverAsync(cancellationToken).ConfigureAwait(false);
            List<KeyValuePair<string, string>> form =
            [
                new("grant_type", "client_credentials"),
                new("client_id", ClientId),
                new("client_secret", _clientSecret),
            ];
            if (_scope is not null)
            {
                form.Add(new("scope", _scope));
            }

            using HttpRequestMessage request = _issuer.Request(HttpMethod.Post, _tokenEndpoint);
            request.Content = new FormUrlEncodedContent(form);
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException("The issuer refused a token for " + ClientId + ": " + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + body);
            }

            using JsonDocument token = JsonDocument.Parse(body);
            string value = token.RootElement.GetProperty("access_token").GetString()
                ?? throw new HttpRequestException("The issuer's token response for " + ClientId + " has no access_token.");
            long seconds = token.RootElement.TryGetProperty("expires_in", out JsonElement expires) ? expires.GetInt64() : 300;
            _current = new BearerToken(value, _clock.GetUtcNow().AddSeconds(seconds));
            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private async Task<Uri> DiscoverAsync(CancellationToken cancellationToken)
    {
        Uri discovery = new(_issuer.Issuer.ToString().TrimEnd('/') + "/.well-known/openid-configuration");
        using HttpRequestMessage request = _issuer.Request(HttpMethod.Get, discovery);
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
        string issuer = document.RootElement.GetProperty("issuer").GetString() ?? string.Empty;
        if (!string.Equals(issuer.TrimEnd('/'), _issuer.Issuer.ToString().TrimEnd('/'), StringComparison.Ordinal))
        {
            throw new HttpRequestException("The issuer names itself " + issuer + ", not " + _issuer.Issuer + ": the server would refuse its tokens.");
        }

        return new Uri(document.RootElement.GetProperty("token_endpoint").GetString()
            ?? throw new HttpRequestException("The issuer's discovery document has no token_endpoint."));
    }
}
