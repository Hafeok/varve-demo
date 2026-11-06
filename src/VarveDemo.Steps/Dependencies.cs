// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Varve.Protocol.Client;
using VarveDemo.Importer;

namespace VarveDemo.Steps;

/// <summary>One package version a lock file resolves.</summary>
/// <param name="Id">The package id, as the lock file writes it.</param>
/// <param name="Version">The resolved version, as the lock file writes it.</param>
public sealed record LockedPackage(string Id, string Version);

/// <summary>Which of S2's three groups a locked package is in (S2 criterion 2).</summary>
public enum DependencyGroup
{
    /// <summary>Deprecated or vulnerable.</summary>
    Flagged,

    /// <summary>In the snapshot, neither deprecated nor vulnerable.</summary>
    Clean,

    /// <summary>Not among the top N, or with no leaf inside the snapshot's window.</summary>
    Outside,
}

/// <summary>S2's answer for one locked package version: the fields the oracle checks (S2 criterion 3).</summary>
public sealed record DependencyAnswer
{
    /// <summary>The package id, lower case.</summary>
    public required string Id { get; init; }

    /// <summary>The resolved version, lower case.</summary>
    public required string Version { get; init; }

    /// <summary>The group.</summary>
    public required DependencyGroup Group { get; init; }

    /// <summary>For <see cref="DependencyGroup.Outside"/>: why.</summary>
    public string? OutsideReason { get; init; }

    /// <summary>The deprecation's reasons, sorted; empty when not deprecated.</summary>
    public required IReadOnlyList<string> DeprecationReasons { get; init; }

    /// <summary>Whether the version is deprecated.</summary>
    public required bool Deprecated { get; init; }

    /// <summary>The advisories, each <c>url severity</c>, sorted.</summary>
    public required IReadOnlyList<string> Advisories { get; init; }

    /// <summary>What to move to: the deprecation's alternate package, or a version; null when there is none.</summary>
    public string? Suggested { get; init; }

    /// <summary>The one-line form both the tool and the oracle print, and the check compares.</summary>
    public string Line() => string.Join(" | ",
        Id,
        Version,
        Group.ToString().ToLowerInvariant(),
        OutsideReason ?? "-",
        Deprecated ? "deprecated: " + (DeprecationReasons.Count == 0 ? "(no reason)" : string.Join(",", DeprecationReasons)) : "-",
        Advisories.Count == 0 ? "-" : string.Join(",", Advisories),
        Suggested ?? "-");
}

/// <summary>S2's whole answer, with the query that produced it.</summary>
/// <param name="Query">The query text, as sent.</param>
/// <param name="Answers">One per locked package version, sorted by id and version.</param>
/// <param name="Position">The response's <c>Varve-Position</c>.</param>
/// <param name="Elapsed">From the request to the last byte.</param>
public sealed record DependencyReport(string Query, IReadOnlyList<DependencyAnswer> Answers, long Position, TimeSpan Elapsed);

/// <summary>
/// S2 (<c>varve-demo deps</c>): reads a <c>packages.lock.json</c>, asks the
/// question of <c>Queries/deps.rq</c> as SPARQL over the protocol, and sorts
/// every locked package into flagged, clean or outside the subset.
/// </summary>
public static class Dependencies
{
    private const string BeginBlock = "# BEGIN per package\n";
    private const string EndBlock = "# END per package\n";
    private const string SampleId = "\"newtonsoft.json\"";
    private const string SampleVersion = "\"13.0.1\"";
    private const string SampleGraph = "<urn:varve-demo:pkg/newtonsoft.json/13.0.1>";

    /// <summary>The package versions a lock file resolves, each once, project references excluded.</summary>
    public static IReadOnlyList<LockedPackage> ReadLockFile(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        SortedDictionary<string, LockedPackage> packages = new(StringComparer.Ordinal);
        foreach (JsonProperty framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            foreach (JsonProperty package in framework.Value.EnumerateObject())
            {
                if (string.Equals(package.Value.GetProperty("type").GetString(), "Project", StringComparison.Ordinal)
                    || !package.Value.TryGetProperty("resolved", out JsonElement resolved))
                {
                    continue;
                }

                LockedPackage locked = new(package.Name, resolved.GetString()!);
                packages.TryAdd(Key(locked.Id, locked.Version), locked);
            }
        }

        return [.. packages.Values];
    }

    /// <summary>
    /// The query for <paramref name="packages"/>: <c>deps.rq</c>'s per-package
    /// block once for each, its sample constants replaced, joined by UNION.
    /// </summary>
    public static string Query(IEnumerable<LockedPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);
        string template = SparqlSelect.Load("deps.rq").ReplaceLineEndings("\n");
        int begin = template.IndexOf(BeginBlock, StringComparison.Ordinal);
        int end = template.IndexOf(EndBlock, StringComparison.Ordinal);
        if (begin < 0 || end < begin)
        {
            throw new InvalidDataException("deps.rq has lost its per-package block markers.");
        }

        string block = template[(begin + BeginBlock.Length)..end].Trim('\n');
        List<string> blocks = [];
        foreach (LockedPackage package in packages)
        {
            string id = package.Id.ToLowerInvariant();
            string version = package.Version.ToLowerInvariant();
            StringBuilder idLiteral = new();
            NTriplesTerms.Literal(idLiteral, id);
            StringBuilder versionLiteral = new();
            NTriplesTerms.Literal(versionLiteral, version);
            StringBuilder graph = new();
            NTriplesTerms.Iri(graph, DemoNames.VersionGraph(id, version));
            blocks.Add(block
                .Replace(SampleGraph, graph.ToString(), StringComparison.Ordinal)
                .Replace(SampleId, idLiteral.ToString(), StringComparison.Ordinal)
                .Replace(SampleVersion, versionLiteral.ToString(), StringComparison.Ordinal));
        }

        return template[..begin] + string.Join("\n  UNION\n", blocks) + "\n" + template[(end + EndBlock.Length)..].TrimStart('\n');
    }

    /// <summary>Asks S2's question for <paramref name="packages"/>; <paramref name="topIds"/> is the subset's id list.</summary>
    public static async Task<DependencyReport> AskAsync(SparqlHttpClient client, IReadOnlyList<LockedPackage> packages, IReadOnlyCollection<string> topIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(topIds);
        string query = Query(packages);
        SelectAnswer answer = await SparqlSelect.RunAsync(client, query, cancellationToken).ConfigureAwait(false);
        HashSet<string> top = new(topIds, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Solution> rows = answer.Solutions.ToDictionary(static s => Key(s["lockId"]!, s["lockVersion"]!), StringComparer.Ordinal);

        List<DependencyAnswer> answers = [];
        foreach (LockedPackage package in packages)
        {
            string key = Key(package.Id, package.Version);
            Solution row = rows.TryGetValue(key, out Solution? found)
                ? found
                : throw new InvalidDataException("The answer has no row for " + package.Id + " " + package.Version + ".");
            answers.Add(FromRow(package, row, top));
        }

        return new DependencyReport(query, [.. answers.OrderBy(static a => a.Id, StringComparer.Ordinal).ThenBy(static a => a.Version, StringComparer.Ordinal)], answer.Position, answer.Elapsed);
    }

    private static DependencyAnswer FromRow(LockedPackage package, Solution row, HashSet<string> top)
    {
        string id = package.Id.ToLowerInvariant();
        string version = package.Version.ToLowerInvariant();
        if (row["key"] is null)
        {
            return new DependencyAnswer
            {
                Id = id,
                Version = version,
                Group = DependencyGroup.Outside,
                OutsideReason = top.Contains(id) ? "version not in the catalog by the snapshot's upper bound" : "not among the top " + top.Count.ToString(CultureInfo.InvariantCulture) + " packages",
                Deprecated = false,
                DeprecationReasons = [],
                Advisories = [],
            };
        }

        string[] reasons = Split(row["reasons"]);
        string[] advisories = Split(row["advisories"]);
        bool deprecated = row["deprecated"] is not null;
        string? suggested = deprecated && row["alternate"] is { } alternate
            ? alternate + (row["alternateVersions"] is { } range ? " " + range : string.Empty)
            : row["lowestClean"] is { } lowest ? lowest[(lowest.IndexOf(' ', StringComparison.Ordinal) + 1)..] : null;
        bool flagged = deprecated || advisories.Length > 0;
        return new DependencyAnswer
        {
            Id = id,
            Version = version,
            Group = flagged ? DependencyGroup.Flagged : DependencyGroup.Clean,
            Deprecated = deprecated,
            DeprecationReasons = reasons,
            Advisories = advisories,
            Suggested = flagged ? suggested : null,
        };
    }

    private static string[] Split(string? list) =>
        string.IsNullOrEmpty(list) ? [] : [.. list.Split(',').Order(StringComparer.Ordinal)];

    private static string Key(string id, string version) => id.ToLowerInvariant() + " " + version.ToLowerInvariant();

    /// <summary>Prints the query and the answer in S2's three groups.</summary>
    public static void Print(DependencyReport report, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);
        output.WriteLine("query (SPARQL, over the protocol, as the viewer):");
        output.WriteLine(report.Query);
        foreach ((DependencyGroup group, string heading) in new[]
        {
            (DependencyGroup.Flagged, "Deprecated or vulnerable"),
            (DependencyGroup.Clean, "Clean"),
            (DependencyGroup.Outside, "Outside the subset"),
        })
        {
            DependencyAnswer[] members = [.. report.Answers.Where(a => a.Group == group)];
            output.WriteLine();
            output.WriteLine(heading + " (" + members.Length.ToString(CultureInfo.InvariantCulture) + "):");
            foreach (DependencyAnswer answer in members)
            {
                output.WriteLine("  " + answer.Id + " " + answer.Version);
                if (answer.OutsideReason is { } why)
                {
                    output.WriteLine("    " + why);
                }

                if (answer.Deprecated)
                {
                    output.WriteLine("    deprecated: " + (answer.DeprecationReasons.Count == 0 ? "(no reason given)" : string.Join(", ", answer.DeprecationReasons)));
                }

                foreach (string advisory in answer.Advisories)
                {
                    int space = advisory.LastIndexOf(' ');
                    output.WriteLine("    advisory " + advisory[..space] + ", severity " + Severity(advisory[(space + 1)..]));
                }

                if (answer.Group == DependencyGroup.Flagged)
                {
                    output.WriteLine("    move to: " + (answer.Suggested ?? "no listed version above it in the snapshot is free of advisories and deprecation"));
                }
            }
        }

        output.WriteLine();
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"answered at position {report.Position}, in {report.Elapsed.TotalMilliseconds:0} ms"));
    }

    /// <summary>The catalog's severity code as a word (0 low, 1 moderate, 2 high, 3 critical).</summary>
    public static string Severity(string code) => code switch
    {
        "0" => "low",
        "1" => "moderate",
        "2" => "high",
        "3" => "critical",
        _ => code,
    };
}
