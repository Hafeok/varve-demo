// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Linq;

namespace VarveDemo.Importer;

/// <summary>
/// The graph model (ADR 0010's amendment of 2026-11-03): of the quads
/// <c>Varve.JsonLd</c> produces from a whole leaf, the store keeps the
/// catalog's advisory state of the version and nothing else:
/// <list type="bullet">
/// <item>on the leaf: <c>nuget:id</c>, <c>nuget:version</c>, <c>nuget:listed</c>, <c>nuget:deprecation</c>, <c>nuget:vulnerability</c>;</item>
/// <item>on its deprecation: <c>nuget:reasons</c>, <c>nuget:alternatePackage</c>;</item>
/// <item>on the alternate package: <c>nuget:id</c>, <c>nuget:range</c>;</item>
/// <item>on each vulnerability: <c>nuget:advisoryUrl</c>, <c>nuget:severity</c>.</item>
/// </list>
/// Everything else is dropped: dependency groups, package entries, types,
/// descriptions, authors, tags, URLs, timestamps and hashes. The importer adds
/// the version key (ADR 0010 point 5).
/// </summary>
public static class AdvisoryModel
{
    private const string Nuget = "http://schema.nuget.org/schema#";

    private static string P(string local) => "<" + Nuget + local + ">";

    /// <summary>The quads the model keeps, in the order they came, given the leaf's node as written.</summary>
    public static IEnumerable<(string Subject, string Predicate, string Object)> Keep(IReadOnlyList<(string Subject, string Predicate, string Object)> quads, string leaf)
    {
        HashSet<string> onLeaf = [P("id"), P("version"), P("listed"), P("deprecation"), P("vulnerability")];
        HashSet<string> deprecations = [.. quads.Where(q => q.Subject == leaf && q.Predicate == P("deprecation")).Select(static q => q.Object)];
        HashSet<string> vulnerabilities = [.. quads.Where(q => q.Subject == leaf && q.Predicate == P("vulnerability")).Select(static q => q.Object)];
        HashSet<string> alternates = [.. quads.Where(q => deprecations.Contains(q.Subject) && q.Predicate == P("alternatePackage")).Select(static q => q.Object)];

        foreach ((string subject, string predicate, string obj) in quads)
        {
            bool kept = (subject == leaf && onLeaf.Contains(predicate))
                || (deprecations.Contains(subject) && (predicate == P("reasons") || predicate == P("alternatePackage")))
                || (alternates.Contains(subject) && (predicate == P("id") || predicate == P("range")))
                || (vulnerabilities.Contains(subject) && (predicate == P("advisoryUrl") || predicate == P("severity")));
            if (kept)
            {
                yield return (subject, predicate, obj);
            }
        }
    }
}
