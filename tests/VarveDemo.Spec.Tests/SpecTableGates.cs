// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VarveDemo.Spec.Tests;

/// <summary>The step table in <c>docs/spec/script.md</c> and its copy in the README.</summary>
public sealed partial class SpecTableGates
{
    [Fact]
    public void Readme_step_table_equals_the_spec_table()
    {
        string spec = SpecTable.Block(RepositoryTree.Read("docs/spec/script.md"), "docs/spec/script.md");
        string readme = SpecTable.Block(RepositoryTree.Read("README.md"), "README.md");

        Assert.Equal(spec, readme);
    }

    [Fact]
    public void Steps_are_unique_and_each_check_is_a_unique_identifier()
    {
        IReadOnlyList<SpecRow> rows = SpecTable.FromSpec();

        Assert.NotEmpty(rows);
        Assert.Empty(Duplicates(rows.Select(static row => row.Step)));
        Assert.Empty(Duplicates(rows.Select(static row => row.Check)));
        Assert.All(rows, static row => Assert.Matches(Identifier(), row.Check));
    }

    [Fact]
    public void Every_step_has_a_target_and_a_known_state()
    {
        Assert.All(SpecTable.FromSpec(), static row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Title), row.Step + " has no title.");
            Assert.False(string.IsNullOrWhiteSpace(row.Target), row.Step + " has no target.");
            Assert.Contains(row.State, SpecTable.States);
        });
    }

    [Fact]
    public void A_passing_step_has_a_test_named_after_its_check()
    {
        string[] sources = [.. RepositoryTree.Files("tests")
            .Where(static file => file.EndsWith(".cs", StringComparison.Ordinal))
            .Select(File.ReadAllText)];

        Assert.All(SpecTable.FromSpec().Where(static row => row.State == "passing"), row =>
        {
            Regex method = new(@"\b(?:void|Task|ValueTask)\s+" + Regex.Escape(row.Check) + @"\s*\(", RegexOptions.CultureInvariant);
            Assert.True(sources.Any(method.IsMatch), row.Step + " is passing, but no test method under tests/ is named " + row.Check + ".");
        });
    }

    private static IEnumerable<string> Duplicates(IEnumerable<string> values) =>
        values.GroupBy(static value => value, StringComparer.Ordinal).Where(static group => group.Count() > 1).Select(static group => group.Key);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();
}
