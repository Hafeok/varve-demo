// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using VarveDemo.Importer;
using VarveDemo.Steps;

namespace VarveDemo.Tool;

/// <summary>The steps a viewer runs against the loaded dataset.</summary>
internal static partial class StepCommands
{
    /// <summary><c>varve-demo count</c>: S1's first answer, as the viewer.</summary>
    public static async Task<int> CountAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        using Connection connection = new(arguments);
        using ClientCredentials viewer = connection.Client("viewer");
        arguments.RefuseUnknown();
        string query = SparqlSelect.Load("catalog-commits.rq");
        await Console.Out.WriteLineAsync(query).ConfigureAwait(false);
        SelectAnswer answer = await SparqlSelect.RunAsync(connection.Server.Client(viewer, TimeSpan.FromMinutes(1)), query, cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
            $"answer: {answer.Solutions[0]["catalogCommits"]} catalog commits at position {answer.Position}, in {answer.Elapsed.TotalMilliseconds:0} ms")).ConfigureAwait(false);
        return 0;
    }
}
