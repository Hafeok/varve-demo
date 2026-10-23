// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VarveDemo.Spec.Tests;

/// <summary>An ADR file, <c>docs/adr/NNNN-slug.md</c>.</summary>
internal sealed record Adr(int Number, string FileName, string Text)
{
    /// <summary>The Status section names a superseding ADR.</summary>
    public bool IsSuperseded
    {
        get
        {
            int status = Text.IndexOf("## Status", StringComparison.Ordinal);
            if (status < 0)
            {
                return false;
            }

            int next = Text.IndexOf("\n## ", status + 1, StringComparison.Ordinal);
            string section = next < 0 ? Text[status..] : Text[status..next];
            return section.Contains("Superseded by", StringComparison.Ordinal);
        }
    }
}

/// <summary>A decision set's front matter: <c>set:</c>, <c>namespace:</c> and <c>adr:</c>.</summary>
internal sealed record DecisionSet(string FileName, string? Set, string? Namespace, string? Adr);

internal static partial class AdrFiles
{
    public static IReadOnlyList<Adr> All() =>
        [.. Directory.EnumerateFiles(RepositoryTree.PathOf("docs/adr"), "*.md")
            .Select(static path => (Path: path, Match: AdrFileName().Match(Path.GetFileName(path))))
            .Where(static file => file.Match.Success)
            .Select(static file => new Adr(
                int.Parse(file.Match.Groups["number"].Value, CultureInfo.InvariantCulture),
                Path.GetFileName(file.Path),
                File.ReadAllText(file.Path).ReplaceLineEndings("\n")))
            .OrderBy(static adr => adr.Number)];

    public static IEnumerable<int> Numbers() => All().Select(static adr => adr.Number);

    public static IReadOnlyList<DecisionSet> Sets() =>
        [.. Directory.EnumerateFiles(RepositoryTree.PathOf("docs/decisions"), "*.md")
            .Where(static path => !string.Equals(Path.GetFileName(path), "README.md", StringComparison.Ordinal))
            .Select(static path => ReadSet(path))];

    private static DecisionSet ReadSet(string path)
    {
        string text = File.ReadAllText(path).ReplaceLineEndings("\n");
        string fileName = Path.GetFileName(path);
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return new DecisionSet(fileName, null, null, null);
        }

        int end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        string frontMatter = end < 0 ? text[4..] : text[4..end];
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match field in TopLevelField().Matches(frontMatter))
        {
            fields.TryAdd(field.Groups["key"].Value, field.Groups["value"].Value.Trim().Trim('"'));
        }

        return new DecisionSet(fileName, fields.GetValueOrDefault("set"), fields.GetValueOrDefault("namespace"), fields.GetValueOrDefault("adr"));
    }

    [GeneratedRegex(@"^(?<number>\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex AdrFileName();

    [GeneratedRegex(@"^(?<key>[a-z-]+):[ \t]*(?<value>[^\n]*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TopLevelField();
}
