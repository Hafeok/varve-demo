// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Net.Http;
using VarveDemo.Importer;

namespace VarveDemo.Tool;

/// <summary>The server, the issuer and the clients a command uses, from its options (ADR 0007).</summary>
internal sealed class Connection : IDisposable
{
    /// <summary>The mock issuer's clients have no secret; a real issuer's are given by option or environment.</summary>
    private const string MockSecret = "not-a-secret-the-issuer-is-a-mock";

    private readonly HttpClient _http = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    public Connection(Arguments arguments)
    {
        Server = new DemoServer(_http, arguments.Uri("server"), arguments.OrDefault("dataset", "nuget"));
        Issuer = new IssuerEndpoint(arguments.Uri("issuer"), arguments.Optional("issuer-address") is { } address ? new Uri(address) : null);
        Arguments = arguments;
    }

    public DemoServer Server { get; }

    public IssuerEndpoint Issuer { get; }

    private Arguments Arguments { get; }

    /// <summary>The client named by <c>--{role}-client</c>, <c>varve-demo-{role}</c> by default.</summary>
    public ClientCredentials Client(string role) => new(
        _http,
        Issuer,
        Arguments.OrDefault(role + "-client", "varve-demo-" + role),
        Arguments.OrDefault(role + "-secret", MockSecret),
        Arguments.Optional(role + "-scope"),
        TimeProvider.System);

    public void Dispose() => _http.Dispose();
}
