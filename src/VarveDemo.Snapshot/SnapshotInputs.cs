// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VarveDemo.Snapshot;

/// <summary>
/// What a snapshot is a function of (ADR 0008): the id list, the catalog time
/// window, and the tool's version, read from <c>snapshot/inputs.json</c> and
/// the id list beside it.
/// </summary>
/// <param name="N">The number of top package ids.</param>
/// <param name="Ids">The ids, in rank order.</param>
/// <param name="From">The window's lower bound, inclusive, on catalog commit time.</param>
/// <param name="To">The window's upper bound, exclusive.</param>
/// <param name="Base">
/// Whether the snapshot carries a base: for every version of the N packages,
/// its latest leaf before <paramref name="From"/> (ADR 0008's amendment). True
/// for the pinned snapshot; false for <c>import --since</c>'s deep replay.
/// </param>
/// <param name="BaseSample">A seeded sample of the base, for a projection only; null for the whole base.</param>
public sealed record SnapshotInputs(int N, IReadOnlyList<string> Ids, DateTimeOffset From, DateTimeOffset To, bool Base = true, BaseSample? BaseSample = null)
{
    /// <summary>Reads <c>inputs.json</c> and the id list it names, from <paramref name="directory"/>.</summary>
    public static SnapshotInputs Read(string directory)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "inputs.json")));
        JsonElement root = document.RootElement;
        int n = root.GetProperty("n").GetInt32();
        string idsFile = root.GetProperty("ids").GetString() ?? "ids.txt";
        JsonElement window = root.GetProperty("window");
        DateTimeOffset from = Timestamp(window.GetProperty("from").GetString());
        DateTimeOffset to = Timestamp(window.GetProperty("to").GetString());
        string[] ids = [.. File.ReadAllLines(Path.Combine(directory, idsFile)).Where(static line => line.Length > 0)];

        if (ids.Length != n)
        {
            throw new InvalidDataException(idsFile + " holds " + ids.Length.ToString(CultureInfo.InvariantCulture) + " ids, and inputs.json says n is " + n.ToString(CultureInfo.InvariantCulture) + ".");
        }

        if (from >= to)
        {
            throw new InvalidDataException("The window's lower bound is not below its upper bound.");
        }

        bool withBase = !root.TryGetProperty("base", out JsonElement baseFlag) || baseFlag.GetBoolean();
        BaseSample? sample = root.TryGetProperty("baseSample", out JsonElement s)
            ? new BaseSample(s.GetProperty("percent").GetInt32(), s.GetProperty("seed").GetString() ?? string.Empty, s.TryGetProperty("by", out JsonElement by) && by.GetString() == "package")
            : null;
        return new SnapshotInputs(n, ids, from, to, withBase, sample);
    }

    /// <summary>Parses an RFC 3339 timestamp in UTC.</summary>
    public static DateTimeOffset Timestamp(string? text) =>
        DateTimeOffset.Parse(text ?? throw new InvalidDataException("A timestamp is missing."), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    /// <summary>A timestamp as the manifest writes it.</summary>
    public static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
}

/// <summary>
/// A seeded sample of the base's versions, for projecting the base's cost
/// without loading it: a version is taken when the SHA-256 of the seed, a
/// newline and its leaf's URL, read as a big-endian 32-bit number, is below
/// <paramref name="Percent"/> hundredths of its range. The same seed takes the
/// same versions. By package, the package's lower-case id is hashed instead,
/// and a package taken brings every version.
/// </summary>
/// <param name="Percent">The share of versions taken, 1 to 100.</param>
/// <param name="Seed">The seed, recorded in the inputs and the manifest.</param>
/// <param name="ByPackage">Sample packages, not versions.</param>
public sealed record BaseSample(int Percent, string Seed, bool ByPackage = false)
{
    /// <summary>Whether the version whose leaf is at <paramref name="url"/>, of package <paramref name="id"/>, is taken.</summary>
    public bool Takes(string url, string id)
    {
        string key = ByPackage ? id.ToLowerInvariant() : url;
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Seed + "\n" + key));
        uint value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash);
        return value < (ulong)uint.MaxValue * (ulong)Percent / 100UL;
    }
}
