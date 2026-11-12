// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Immutable;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace VarveDemo.Snapshot;

/// <summary>
/// What the snapshot keeps of a base leaf (ADR 0008's and ADR 0010's
/// amendments of 2026-11-03): its inline <c>@context</c>, its <c>@id</c>, and
/// the members the graph model keeps, in the leaf's own order, values
/// untouched. Window leaves are kept whole. The rule is <see cref="Rule"/>,
/// and the manifest records it. A leaf whose context is not inline, a remote
/// context, fails the build (ADR 0006's amendment): it is never fetched.
/// </summary>
public static class LeafProjection
{
    /// <summary>The members kept, besides <c>@context</c> and <c>@id</c>.</summary>
    public static readonly ImmutableArray<string> Members = ["id", "version", "listed", "deprecation", "vulnerabilities"];

    /// <summary>The rule as the manifest records it.</summary>
    public static string Rule => "base leaves keep @context, @id, " + string.Join(", ", Members) + "; window leaves are whole";

    /// <summary>A leaf's context, checked to be inline, as the manifest hashes it.</summary>
    public static JsonElement InlineContext(JsonElement leaf, string url) =>
        leaf.TryGetProperty("@context", out JsonElement context) && context.ValueKind == JsonValueKind.Object
            ? context
            : throw new InvalidDataException("The leaf at " + url + " names a remote context (or none). The demo reads only inline contexts and fetches none (ADR 0006).");

    /// <summary>The projection of one leaf, minified.</summary>
    public static byte[] Project(JsonElement leaf, string url)
    {
        if (leaf.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The leaf at " + url + " is not a JSON object.");
        }

        if (!leaf.TryGetProperty("@context", out JsonElement context) || context.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The leaf at " + url + " names a remote context (or none). The demo reads only inline contexts and fetches none (ADR 0006).");
        }

        using MemoryStream output = new();
        using (Utf8JsonWriter json = new(output, new JsonWriterOptions { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            foreach (JsonProperty member in leaf.EnumerateObject())
            {
                if (member.NameEquals("@context") || member.NameEquals("@id") || Members.Contains(member.Name))
                {
                    member.WriteTo(json);
                }
            }

            json.WriteEndObject();
        }

        return output.ToArray();
    }
}
