// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace VarveDemo.Spec.Tests;

/// <summary>MPL-2.0 (ADR 0003).</summary>
public sealed class LicenceGates
{
    /// <summary>Exhibit A, as every <c>.cs</c> file opens.</summary>
    private const string ExhibitA =
        "// This Source Code Form is subject to the terms of the Mozilla Public\n" +
        "// License, v. 2.0. If a copy of the MPL was not distributed with this\n" +
        "// file, You can obtain one at https://mozilla.org/MPL/2.0/.\n";

    /// <summary>
    /// SHA-256 of https://www.mozilla.org/media/MPL/2.0/index.txt with LF line
    /// endings, checked against that URL on 2026-10-23. Varve's LICENSE has the
    /// same bytes.
    /// </summary>
    private const string MplSha256 = "3f3d9e0024b1921b067d6f7f88deb4a60cbe7a78e76c64e3f1d7fc3b779b9d04";

    [Fact]
    public void Every_cs_file_starts_with_the_exhibit_a_notice()
    {
        string[] missing = [.. RepositoryTree.Files()
            .Where(static file => file.EndsWith(".cs", StringComparison.Ordinal))
            .Where(static file => !File.ReadAllText(file).ReplaceLineEndings("\n").TrimStart('﻿').StartsWith(ExhibitA, StringComparison.Ordinal))
            .Select(RepositoryTree.Relative)];

        Assert.True(missing.Length == 0, "Without the Exhibit A notice: " + string.Join(", ", missing));
    }

    [Fact]
    public void Licence_is_the_mpl_2_0_text()
    {
        string licence = RepositoryTree.Read("LICENSE");
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(licence)));

        Assert.StartsWith("Mozilla Public License Version 2.0\n", licence, StringComparison.Ordinal);
        Assert.Equal(MplSha256, hash);
    }
}
