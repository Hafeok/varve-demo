// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NuGet.Versioning;

namespace VarveDemo.Oracle;

/// <summary>A package version as the snapshot leaves it after its last commit.</summary>
/// <param name="Id">The package id, lower case.</param>
/// <param name="Version">The version, lower case, as the page names it.</param>
/// <param name="LeafVersion">The leaf's own <c>version</c>, as written.</param>
/// <param name="Parsed">The version, parsed by NuGet.</param>
/// <param name="Listed">Whether the leaf states <c>listed</c> true.</param>
/// <param name="Deprecated">Whether the leaf has a <c>deprecation</c>.</param>
/// <param name="Reasons">The deprecation's reasons.</param>
/// <param name="Alternate">The deprecation's alternate package id and range, or null.</param>
/// <param name="Advisories">Each vulnerability as <c>url severity</c>.</param>
public sealed record OracleVersion(
    string Id,
    string Version,
    string LeafVersion,
    NuGetVersion Parsed,
    bool Listed,
    bool Deprecated,
    IReadOnlyList<string> Reasons,
    string? Alternate,
    IReadOnlyList<string> Advisories);

/// <summary>
/// Replays a snapshot directory's <c>commits.ndjson</c> and
/// <c>leaves.ndjson</c> into the state its last commit leaves, and answers
/// S2's question from it by the specification's rules. It shares no code with
/// the importer or the query.
/// </summary>
public sealed class CatalogOracle
{
    private readonly Dictionary<(string Id, string Version), OracleVersion> _present;

    private CatalogOracle(Dictionary<(string, string), OracleVersion> present, IReadOnlyList<string> everyVersion)
    {
        _present = present;
        EveryVersion = everyVersion;
    }

    /// <summary>Every version string a details leaf of the snapshot names, as written, deletes and replaced leaves included.</summary>
    public IReadOnlyList<string> EveryVersion { get; }

    /// <summary>The versions present after the last commit.</summary>
    public IReadOnlyCollection<OracleVersion> Present => _present.Values;

    /// <summary>Replays the snapshot in <paramref name="directory"/>.</summary>
    public static CatalogOracle Load(string directory)
    {
        Dictionary<(string, string), OracleVersion> present = [];
        HashSet<string> versions = new(StringComparer.Ordinal);
        using StreamReader leaves = new(Path.Combine(directory, "leaves.ndjson"));
        foreach (string line in File.ReadLines(Path.Combine(directory, "commits.ndjson")))
        {
            using JsonDocument commit = JsonDocument.Parse(line);
            foreach (JsonElement entry in commit.RootElement.GetProperty("leaves").EnumerateArray())
            {
                string json = leaves.ReadLine() ?? throw new InvalidDataException("leaves.ndjson ends early.");
                (string, string) key = (entry.GetProperty("id").GetString()!.ToLowerInvariant(), entry.GetProperty("version").GetString()!.ToLowerInvariant());
                if (entry.GetProperty("type").GetString() == "PackageDelete")
                {
                    present.Remove(key);
                    continue;
                }

                using JsonDocument leaf = JsonDocument.Parse(json);
                JsonElement root = leaf.RootElement;
                string leafVersion = root.GetProperty("version").GetString()!;
                versions.Add(leafVersion);
                bool deprecated = root.TryGetProperty("deprecation", out JsonElement deprecation);
                string[] reasons = deprecated && deprecation.TryGetProperty("reasons", out JsonElement r) ? [.. r.EnumerateArray().Select(static x => x.GetString()!)] : [];
                string? alternate = null;
                if (deprecated && deprecation.TryGetProperty("alternatePackage", out JsonElement alt) && alt.TryGetProperty("id", out JsonElement altId))
                {
                    alternate = altId.GetString() + (alt.TryGetProperty("range", out JsonElement range) ? " " + range.GetString() : string.Empty);
                }

                string[] advisories = root.TryGetProperty("vulnerabilities", out JsonElement vulnerabilities)
                    ? [.. vulnerabilities.EnumerateArray().Select(static v => v.GetProperty("advisoryUrl").GetString() + " " + v.GetProperty("severity").GetString()).Distinct(StringComparer.Ordinal)]
                    : [];
                bool listed = root.TryGetProperty("listed", out JsonElement l) && l.ValueKind == JsonValueKind.True;
                present[key] = new OracleVersion(key.Item1, key.Item2, leafVersion, NuGetVersion.Parse(leafVersion), listed, deprecated, reasons, alternate, advisories);
            }
        }

        return new CatalogOracle(present, [.. versions.Order(StringComparer.Ordinal)]);
    }

    /// <summary>S2's answer for one locked package version, as one line in the tool's form.</summary>
    public string Answer(string id, string version, IReadOnlyCollection<string> topIds)
    {
        ArgumentNullException.ThrowIfNull(topIds);
        string lowerId = id.ToLowerInvariant();
        string lowerVersion = version.ToLowerInvariant();
        if (!_present.TryGetValue((lowerId, lowerVersion), out OracleVersion? locked))
        {
            bool inTop = topIds.Any(top => string.Equals(top, id, StringComparison.OrdinalIgnoreCase));
            return Line(lowerId, lowerVersion, "outside", inTop ? "no leaf in the snapshot's window" : "not among the top " + topIds.Count + " packages", "-", "-", "-");
        }

        bool flagged = locked.Deprecated || locked.Advisories.Count > 0;
        if (!flagged)
        {
            return Line(lowerId, lowerVersion, "clean", "-", "-", "-", "-");
        }

        string suggested;
        if (locked.Deprecated && locked.Alternate is not null)
        {
            suggested = locked.Alternate;
        }
        else
        {
            OracleVersion? lowest = _present.Values
                .Where(c => c.Id == lowerId && c.Listed && !c.Deprecated && c.Advisories.Count == 0)
                .Where(c => VersionComparer.Default.Compare(c.Parsed, locked.Parsed) > 0)
                .Where(c => locked.Parsed.IsPrerelease || !c.Parsed.IsPrerelease)
                .OrderBy(static c => c.Parsed, VersionComparer.Default)
                .FirstOrDefault();
            suggested = lowest?.LeafVersion ?? "-";
        }

        return Line(
            lowerId,
            lowerVersion,
            "flagged",
            "-",
            locked.Deprecated ? "deprecated: " + (locked.Reasons.Count == 0 ? "(no reason)" : string.Join(",", locked.Reasons.Order(StringComparer.Ordinal))) : "-",
            locked.Advisories.Count == 0 ? "-" : string.Join(",", locked.Advisories.Order(StringComparer.Ordinal)),
            suggested);
    }

    private static string Line(params string[] fields) => string.Join(" | ", fields);
}
