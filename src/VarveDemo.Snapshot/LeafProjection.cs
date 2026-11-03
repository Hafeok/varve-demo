// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Immutable;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace VarveDemo.Snapshot;

/// <summary>
/// What the snapshot keeps of a catalog leaf (ADR 0010's amendment of
/// 2026-11-03): its inline <c>@context</c>, its <c>@id</c>, and the members
/// steps S2 to S4 read, in the leaf's own order, values untouched. The
/// importer and the oracle both read this projection, so they read the same
/// fields. A leaf whose context is not inline, a remote context, fails the
/// build (ADR 0006's amendment): it is never fetched.
/// </summary>
public static class LeafProjection
{
    /// <summary>The members kept, besides <c>@context</c> and <c>@id</c>.</summary>
    public static readonly ImmutableArray<string> Members = ["id", "version", "listed", "isPrerelease", "deprecation", "vulnerabilities"];

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
