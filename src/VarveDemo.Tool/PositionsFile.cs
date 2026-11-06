// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VarveDemo.Importer;

namespace VarveDemo.Tool;

/// <summary>Every acknowledgement of an import, one line each: sequence, catalog commit, position (S1 criterion 6).</summary>
internal static class PositionsFile
{
    public static Task WriteAsync(string path, ImportResult result, CancellationToken cancellationToken)
    {
        StringBuilder text = new();
        text.Append("sequence\tcommitId\tposition\n");
        foreach (Acknowledged ack in result.Acknowledgements)
        {
            text.Append(ack.Sequence.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(ack.CommitId).Append('\t').Append(ack.Position.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
    }
}
