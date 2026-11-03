// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Varve.JsonLd;
using Varve.Rdf;
using VarveDemo.Snapshot;

namespace VarveDemo.Importer;

/// <summary>The names ADRs 0004, 0008 and 0010 fix.</summary>
public static class DemoNames
{
    /// <summary>The graph that carries catalog time (ADR 0004).</summary>
    public const string CatalogLog = "urn:varve-demo:catalog-log";

    /// <summary>The demo's namespace for derived terms (ADR 0010).</summary>
    public const string Term = "urn:varve-demo:term:";

    /// <summary>The version key's predicate (ADR 0010 point 5).</summary>
    public const string VersionKey = Term + "versionKey";

    /// <summary>The catalog's own namespace for commit terms.</summary>
    public const string Catalog = "http://schema.nuget.org/catalog#";

    /// <summary>The catalog's package vocabulary.</summary>
    public const string Schema = "http://schema.nuget.org/schema#";

    /// <summary>A catalog commit's node in the catalog log (ADR 0010 point 4).</summary>
    public static string CatalogCommit(string commitId) => "urn:varve-demo:catalog-commit:" + Encode(commitId);

    /// <summary>
    /// A package version's graph (ADR 0008 point 4):
    /// <c>urn:varve-demo:pkg/{id}/{version}</c>, both lower-cased and
    /// percent-encoded, the version as the catalog normalises it.
    /// </summary>
    public static string VersionGraph(string id, string version) =>
        "urn:varve-demo:pkg/" + Encode(id.ToLowerInvariant()) + "/" + Encode(version.ToLowerInvariant());

    /// <summary>Percent-encodes everything but RFC 3986's unreserved characters, as UTF-8.</summary>
    public static string Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder output = new(text.Length);
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            char c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~')
            {
                output.Append(c);
            }
            else
            {
                output.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return output.ToString();
    }
}

/// <summary>One catalog commit as a SPARQL Update, and what it inserts.</summary>
/// <param name="Text">The update.</param>
/// <param name="Quads">Quads it inserts.</param>
/// <param name="Graphs">Version graphs it replaces or drops.</param>
public sealed record CatalogUpdateText(string Text, long Quads, int Graphs);

/// <summary>A leaf <c>Varve.JsonLd</c> refused, with its reason (ADR 0006 point 4).</summary>
public sealed class LeafRefusedException : Exception
{
    /// <summary>For serialisers.</summary>
    public LeafRefusedException()
    {
    }

    /// <summary>With a message.</summary>
    public LeafRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>With a message and a cause.</summary>
    public LeafRefusedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Maps one retained catalog commit to one SPARQL Update (ADRs 0004, 0006,
/// 0008, 0010): for each leaf, the version's graph is dropped and, for a
/// details leaf, its quads inserted, as <c>Varve.JsonLd</c> produces them with
/// the version key beside them; then the commit's catalog-log assertion.
/// </summary>
public sealed class CatalogUpdate
{
    private readonly IReadOnlyDictionary<string, byte[]> _contexts;

    /// <summary>A mapper whose document loader answers only from <paramref name="contexts"/> (ADR 0006 point 2).</summary>
    public CatalogUpdate(IReadOnlyDictionary<string, byte[]> contexts)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
    }

    /// <summary>The mapping's version, part of the tool version a snapshot names.</summary>
    public const string MappingVersion = "1";

    /// <summary>The update for <paramref name="commit"/>.</summary>
    public CatalogUpdateText Map(SnapshotCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        StringBuilder update = new(16 * 1024);
        long quads = 0;
        int leafNumber = 0;

        foreach (SnapshotLeaf leaf in commit.Leaves)
        {
            string graph = DemoNames.VersionGraph(leaf.Ref.Id, leaf.Ref.Version);
            update.Append("DROP SILENT GRAPH ");
            NTriplesTerms.Iri(update, graph);
            update.Append(" ;\n");

            if (leaf.Ref.IsDelete)
            {
                continue;
            }

            update.Append("INSERT DATA { GRAPH ");
            NTriplesTerms.Iri(update, graph);
            update.Append(" {\n");
            quads += Leaf(update, leaf, "l" + leafNumber.ToString(CultureInfo.InvariantCulture) + "_");
            update.Append("} } ;\n");
            leafNumber++;
        }

        update.Append("INSERT DATA { GRAPH ");
        NTriplesTerms.Iri(update, DemoNames.CatalogLog);
        update.Append(" {\n");
        NTriplesTerms.Iri(update, DemoNames.CatalogCommit(commit.Commit.CommitId));
        update.Append(' ');
        NTriplesTerms.Iri(update, DemoNames.Catalog + "commitId");
        update.Append(' ');
        NTriplesTerms.Literal(update, commit.Commit.CommitId);
        update.Append(" ;\n  ");
        NTriplesTerms.Iri(update, DemoNames.Catalog + "commitTimeStamp");
        update.Append(' ');
        NTriplesTerms.Literal(update, commit.Commit.CommitTimeStamp);
        update.Append("^^<http://www.w3.org/2001/XMLSchema#dateTime> .\n} }\n");
        quads += 2;

        return new CatalogUpdateText(update.ToString(), quads, commit.Leaves.Count);
    }

    /// <summary>Counts the quads <see cref="Map"/> would insert, without building the text.</summary>
    public long CountQuads(SnapshotCommit commit) => Map(commit).Quads;

    private long Leaf(StringBuilder update, SnapshotLeaf leaf, string blankPrefix)
    {
        IReadOnlyDictionary<string, byte[]> contexts = _contexts;
        JsonLdOptions options = new()
        {
            BaseIri = Encoding.UTF8.GetBytes(leaf.Ref.Url),
            DocumentLoader = (ReadOnlySpan<byte> iri, out ReadOnlyMemory<byte> document) =>
            {
                if (contexts.TryGetValue(Encoding.UTF8.GetString(iri), out byte[]? recorded))
                {
                    document = recorded;
                    return true;
                }

                document = default;
                return false;
            },
        };

        long count = 0;
        JsonLdResult result = JsonLdParser.Parse(leaf.Json, (in QuadView quad) =>
        {
            if (quad.HasGraph)
            {
                throw new LeafRefusedException("The leaf " + leaf.Ref.Url + " names a graph of its own; a catalog leaf is one default graph.");
            }

            NTriplesTerms.Write(update, quad.Subject, blankPrefix);
            update.Append(' ');
            NTriplesTerms.Write(update, quad.Predicate, blankPrefix);
            update.Append(' ');
            NTriplesTerms.Write(update, quad.Object, blankPrefix);
            update.Append(" .\n");
            count++;
        }, options);

        if (!result.Succeeded)
        {
            throw new LeafRefusedException("Varve.JsonLd refused the leaf " + leaf.Ref.Url + ": " + result.Error);
        }

        // The version key, on the leaf's own node (ADR 0010 point 5).
        string version = VersionOf(leaf);
        NTriplesTerms.Iri(update, leaf.Ref.Url);
        update.Append(' ');
        NTriplesTerms.Iri(update, DemoNames.VersionKey);
        update.Append(' ');
        NTriplesTerms.Literal(update, VersionKey.Of(version));
        update.Append(" .\n");
        return count + 1;
    }

    /// <summary>The leaf's own normalised <c>version</c>; the page's when the leaf has none.</summary>
    public static string VersionOf(SnapshotLeaf leaf)
    {
        ArgumentNullException.ThrowIfNull(leaf);
        Utf8JsonReader reader = new(leaf.Json.Span);
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.TryGetProperty("version", out JsonElement version) && version.GetString() is { Length: > 0 } text
            ? text
            : leaf.Ref.Version;
    }
}
