// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace VarveDemo.Spec.Tests;

/// <summary>
/// The decision-set gate's allowance for a superseded ADR, tested against a
/// fixture ledger in a temporary directory rather than reasoned about: the
/// repository has no superseded ADR yet, so the real ledger cannot show it.
/// </summary>
public sealed class AdrLedgerFixtureTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("varvedemo-adr-ledger-");

    private string AdrDirectory => Path.Combine(_root.FullName, "adr");

    private string DecisionsDirectory => Path.Combine(_root.FullName, "decisions");

    public AdrLedgerFixtureTests()
    {
        Directory.CreateDirectory(AdrDirectory);
        Directory.CreateDirectory(DecisionsDirectory);
    }

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void A_superseded_adr_needs_no_decision_set()
    {
        WriteAdr("0001-the-old-way.md", "Superseded by [0002](0002-the-new-way.md), 2026-10-23.");
        WriteAdr("0002-the-new-way.md", "Proposed.");
        WriteSet("the-new-way", "0002");

        Assert.Empty(Check());
    }

    [Fact]
    public void An_adr_in_force_without_a_decision_set_is_reported()
    {
        WriteAdr("0001-the-old-way.md", "Proposed.");
        WriteAdr("0002-the-new-way.md", "Proposed.");
        WriteSet("the-new-way", "0002");

        Assert.Equal(["0001-the-old-way.md"], Check());
    }

    [Fact]
    public void Superseded_by_outside_the_status_section_does_not_exempt_an_adr()
    {
        WriteAdr("0001-the-old-way.md", "Proposed.", context: "If this is ever Superseded by a later ADR, its set moves.");

        Assert.Equal(["0001-the-old-way.md"], Check());
    }

    [Fact]
    public void A_set_for_a_superseded_adr_is_still_allowed()
    {
        WriteAdr("0001-the-old-way.md", "Superseded by 0002, in part.");
        WriteSet("the-old-way", "0001");

        Assert.Empty(Check());
    }

    private IReadOnlyList<string> Check() =>
        AdrFiles.WithoutASet(AdrFiles.All(AdrDirectory), AdrFiles.Sets(DecisionsDirectory));

    private void WriteAdr(string fileName, string status, string context = "Why.") =>
        File.WriteAllText(Path.Combine(AdrDirectory, fileName),
            "# " + fileName[..4] + "\n\n## Status\n\n" + status + "\n\n## Context\n\n" + context + "\n\n## Decision\n\nWhat.\n");

    private void WriteSet(string slug, string adr) =>
        File.WriteAllText(Path.Combine(DecisionsDirectory, slug + ".md"),
            "---\nset: " + slug + "\nnamespace: varvedemo\nadr: " + adr + "\ndecisions:\n  - key: Fixture" + adr + "\n    statement: \"A fixture ruling\"\n---\n");
}
