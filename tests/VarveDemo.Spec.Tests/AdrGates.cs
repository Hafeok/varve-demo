// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace VarveDemo.Spec.Tests;

/// <summary>ADRs, their index and their decision sets (ADR 0001).</summary>
public sealed class AdrGates
{
    [Fact]
    public void Every_file_in_docs_adr_is_an_adr_or_the_index()
    {
        string[] strays = [.. Directory.EnumerateFiles(RepositoryTree.PathOf("docs/adr"))
            .Select(Path.GetFileName)
            .Where(static name => !string.Equals(name, "README.md", StringComparison.Ordinal))
            .Where(static name => !AdrFiles.All().Any(adr => string.Equals(adr.FileName, name, StringComparison.Ordinal)))
            .Select(static name => name!)];

        Assert.True(strays.Length == 0, "Not named NNNN-slug.md: " + string.Join(", ", strays));
    }

    [Fact]
    public void Adr_numbers_run_from_one_without_gaps()
    {
        int[] numbers = [.. AdrFiles.Numbers()];

        Assert.NotEmpty(numbers);
        Assert.Equal(Enumerable.Range(1, numbers.Length), numbers);
    }

    [Fact]
    public void Every_adr_not_superseded_has_a_decision_set_whose_adr_matches()
    {
        IReadOnlyList<string> missing = AdrFiles.WithoutASet(AdrFiles.All(), AdrFiles.Sets());

        Assert.True(missing.Count == 0, "No set in docs/decisions/ whose adr: matches: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_decision_set_is_named_after_its_file_and_in_the_varvedemo_namespace()
    {
        IReadOnlyList<DecisionSet> sets = AdrFiles.Sets();

        Assert.NotEmpty(sets);
        Assert.All(sets, static set =>
        {
            Assert.True(string.Equals(set.Set + ".md", set.FileName, StringComparison.Ordinal), set.FileName + " declares set: " + set.Set + ".");
            Assert.True(string.Equals(set.Namespace, "varvedemo", StringComparison.Ordinal), set.FileName + " declares namespace: " + set.Namespace + ".");
        });
    }

    [Fact]
    public void Every_decision_set_cites_an_existing_adr()
    {
        HashSet<string> adrs = [.. AdrFiles.All().Select(static adr => adr.FileName[..4])];

        Assert.All(AdrFiles.Sets().Where(static set => set.Adr is not null), set =>
            Assert.True(adrs.Contains(set.Adr!), set.FileName + " cites adr: " + set.Adr + ", which does not exist."));
    }

    [Fact]
    public void The_index_links_every_adr()
    {
        string index = RepositoryTree.Read("docs/adr/README.md");

        Assert.All(AdrFiles.All(), adr =>
            Assert.True(index.Contains("](" + adr.FileName + ")", StringComparison.Ordinal), "docs/adr/README.md does not link " + adr.FileName + "."));
    }
}
