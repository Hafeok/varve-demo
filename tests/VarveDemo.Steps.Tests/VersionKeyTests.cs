// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Versioning;
using VarveDemo.Importer;
using VarveDemo.Oracle;
using Xunit;

namespace VarveDemo.Steps.Tests;

/// <summary>The version key against NuGet.Versioning (ADR 0010 points 5 and 6).</summary>
public sealed class VersionKeyTests
{
    private static readonly string[] s_hard =
    [
        "1.0.0", "1.0", "1", "1.0.0.0", "1.0.0.1", "1.0.1", "1.1", "1.10.0", "1.9.0", "10.0.0", "2.0.0",
        "1.0.0-alpha", "1.0.0-ALPHA", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2",
        "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0-rc-1", "1.0.0-1", "1.0.0-a1", "1.0.0-a10",
        "1.0.0-a2", "1.0.0-alpha.1.1", "1.0.0-preview-01", "1.0.0-preview.15", "0.1.0-preview.4",
        "0.1.0-preview.15", "1.0.0+build.5", "1.0.0-beta+meta", "2147483647.0.0", "1.0.0-2147483648",
        "4.0.69-alpha", "4.0.85-alpha", "1.0.11-preview-01",
    ];

    [Fact]
    public void The_key_orders_hard_cases_as_nuget_does() => AssertAgrees(s_hard);

    /// <summary>
    /// Every version in the snapshot (ADR 0010 point 6). Needs the built
    /// snapshot, so it runs where VARVE_DEMO_SNAPSHOT_DIR names one, as CI's
    /// snapshot job does.
    /// </summary>
    [Fact]
    public void The_key_orders_every_version_in_the_snapshot_as_nuget_does()
    {
        string? directory = Environment.GetEnvironmentVariable("VARVE_DEMO_SNAPSHOT_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(directory), "VARVE_DEMO_SNAPSHOT_DIR names no built snapshot; CI's snapshot job sets it.");
        Assert.True(File.Exists(Path.Combine(directory!, "leaves.ndjson")), directory + " holds no snapshot.");
        IReadOnlyList<string> versions = CatalogOracle.Load(directory!).EveryVersion;
        Assert.NotEmpty(versions);
        AssertAgrees(versions);
    }

    private static void AssertAgrees(IReadOnlyList<string> versions)
    {
        // Sorted by NuGet; then each adjacent pair must compare the same way by
        // key. With both orders total, that is agreement on every pair.
        (string Text, NuGetVersion Parsed, string Key)[] sorted = [.. versions
            .Select(static v => (Text: v, Parsed: NuGetVersion.Parse(v), Key: VersionKey.Of(v)))
            .OrderBy(static v => v.Parsed, VersionComparer.Default)
            .ThenBy(static v => v.Text, StringComparer.Ordinal)];

        for (int i = 1; i < sorted.Length; i++)
        {
            int nuget = Math.Sign(VersionComparer.Default.Compare(sorted[i - 1].Parsed, sorted[i].Parsed));
            int key = Math.Sign(string.CompareOrdinal(sorted[i - 1].Key, sorted[i].Key));
            Assert.True(nuget == key, "NuGet orders " + sorted[i - 1].Text + " and " + sorted[i].Text + " as " + nuget + "; their keys " + sorted[i - 1].Key + " and " + sorted[i].Key + " as " + key + ".");
        }
    }
}
