// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace VarveDemo.Snapshot;

/// <summary>SHA-256 as lower-case hex.</summary>
public static class Sha256Text
{
    /// <summary>The hash of <paramref name="bytes"/>.</summary>
    public static string Of(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The hash of a file's bytes.</summary>
    public static async Task<string> OfFileAsync(string path, CancellationToken cancellationToken)
    {
        FileStream file = File.OpenRead(path);
        await using (file.ConfigureAwait(false))
        {
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false));
        }
    }
}
