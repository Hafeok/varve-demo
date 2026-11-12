// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VarveDemo.Snapshot;

/// <summary>
/// GETs against nuget.org with a bounded retry. A response that is not a
/// success after the last attempt fails the build: nothing fetched is ever
/// replaced by something else.
/// </summary>
public sealed class CatalogHttp : IDisposable
{
    private const int Attempts = 6;

    private readonly HttpClient _http;

    /// <summary>A client with automatic decompression and a generous timeout.</summary>
    public CatalogHttp()
    {
        // The base reads every page and about half a million leaves: many
        // connections, HTTP/2 where the server offers it.
        SocketsHttpHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = 64,
            EnableMultipleHttp2Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(100),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("varve-demo-snapshot/1");
    }

    /// <summary>The body of <paramref name="url"/>, after up to six attempts with exponential back-off.</summary>
    public async Task<byte[]> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        Exception? last = null;
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken).ConfigureAwait(false);
            }

            try
            {
                using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                }

                last = new HttpRequestException("GET " + url + " answered " + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                {
                    break;
                }
            }
            catch (HttpRequestException error)
            {
                last = error;
            }
            catch (TaskCanceledException error) when (!cancellationToken.IsCancellationRequested)
            {
                last = error;
            }
        }

        throw new HttpRequestException("GET " + url + " failed after " + Attempts.ToString(System.Globalization.CultureInfo.InvariantCulture) + " attempts.", last);
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
