// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VarveDemo.Spec.Tests;

/// <summary>One row of the step table: <c>| Step | Title | Target | Check | State |</c>.</summary>
internal sealed record SpecRow(string Step, string Title, string Target, string Check, string State);

/// <summary>
/// The machine-readable step table, between <c>&lt;!-- spec-table:begin --&gt;</c>
/// and <c>&lt;!-- spec-table:end --&gt;</c> in <c>docs/spec/script.md</c>, with an
/// identical copy in <c>README.md</c>.
/// </summary>
internal static class SpecTable
{
    public const string Begin = "<!-- spec-table:begin -->";
    public const string End = "<!-- spec-table:end -->";

    public static readonly string[] Header = ["Step", "Title", "Target", "Check", "State"];

    public static readonly string[] States = ["pending", "passing", "withdrawn"];

    /// <summary>The text between the markers, trimmed; it fails when the markers are missing or repeated.</summary>
    public static string Block(string document, string name)
    {
        int begin = document.IndexOf(Begin, StringComparison.Ordinal);
        int end = document.IndexOf(End, StringComparison.Ordinal);
        if (begin < 0 || end < begin)
        {
            throw new FormatException(name + " has no " + Begin + " … " + End + " block.");
        }

        if (document.IndexOf(Begin, begin + Begin.Length, StringComparison.Ordinal) >= 0)
        {
            throw new FormatException(name + " has more than one step table.");
        }

        return document[(begin + Begin.Length)..end].Trim();
    }

    public static IReadOnlyList<SpecRow> Rows(string block, string name)
    {
        string[] lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            throw new FormatException(name + "'s step table has no rows.");
        }

        string[] header = Cells(lines[0]);
        if (!header.SequenceEqual(Header, StringComparer.Ordinal))
        {
            throw new FormatException(name + "'s step table header is | " + string.Join(" | ", header) + " |, not | " + string.Join(" | ", Header) + " |.");
        }

        List<SpecRow> rows = [];
        foreach (string line in lines.Skip(2))
        {
            string[] cells = Cells(line);
            if (cells.Length != Header.Length)
            {
                throw new FormatException(name + ": a row has " + cells.Length + " cells: " + line);
            }

            rows.Add(new SpecRow(cells[0], cells[1], cells[2], cells[3], cells[4]));
        }

        return rows;
    }

    public static IReadOnlyList<SpecRow> FromSpec() =>
        Rows(Block(RepositoryTree.Read("docs/spec/script.md"), "docs/spec/script.md"), "docs/spec/script.md");

    private static string[] Cells(string line)
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith('|') || !trimmed.EndsWith('|'))
        {
            throw new FormatException("Not a table row: " + line);
        }

        return [.. trimmed[1..^1].Split('|').Select(static cell => cell.Trim())];
    }
}
