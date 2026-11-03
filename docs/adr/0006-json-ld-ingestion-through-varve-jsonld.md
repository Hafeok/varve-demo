# 0006 — JSON-LD ingestion through `Varve.JsonLd`

## Status

**Accepted — by the maintainer on 2026-10-26, on the foundation's pull request
[#2](https://github.com/Hafeok/varve-demo/pull/2).** Filed by session 01 of
#1, 2026-10-23. Its decision set is
[`json-ld-ingestion-through-varve-jsonld`](../decisions/json-ld-ingestion-through-varve-jsonld.md).

## Context

The NuGet catalog is JSON-LD: every leaf names remote `@context` documents. The
brief: "if Varve's JSON-LD reader (milestone 6b) is not released, the importer
converts the catalog to N-Quads and says so in the README."

It is released. `Varve.JsonLd` shipped in `v0.1.0-preview.4` (Varve's
`releases/v0.1.0-preview.4.yaml`; Varve ADR 0123): JSON-LD 1.1 expansion and
toRdf over `Utf8JsonReader`, with the W3C suites whole. `JsonLdOptions` is a
readonly struct whose `DocumentLoader` (`JsonLdDocumentLoader?`, a delegate
`bool (ReadOnlySpan<byte> iri, out ReadOnlyMemory<byte> document)`) "fetches
remote contexts. Null, the default, refuses every one"; the package never opens
a connection of its own.

The server, separately, never fetches a context for a request it is given: the
importer decides what to send.

## Decision

1. **The brief's fallback is not needed.** The importer reads catalog leaves
   with `Varve.JsonLd` at `VarveVersion`. The README says JSON-LD is read by
   Varve's own reader.
2. **Remote contexts are recorded in the snapshot** (ADR 0008) and supplied
   through `JsonLdOptions.DocumentLoader`, which answers only from the
   snapshot's recorded documents. A context the snapshot does not hold is
   refused, as the default refuses every one, and the import fails.
3. **Processing happens in the importer.** The result goes to the server as
   **one SPARQL Update per catalog commit**, carrying that commit's graphs and
   its `<urn:varve-demo:catalog-log>` assertion (ADR 0004), so the server never
   fetches a context and one catalog commit is one Varve commit (F1). The
   update's body stays below the server's `Limits:MaxRequestBody` (100 MiB by
   default); the importer fails rather than splits a commit that would not.
4. **A leaf Varve rejects or misreads fails the import** and becomes a Varve
   issue with the leaf as its reproduction. It is never patched, skipped or
   converted another way. A misread is caught by the independent reader of S2
   and S3 disagreeing with Varve's answer.

## Alternatives considered

- **Convert to N-Quads with another library** (dotNetRDF, json-ld.net).
  Rejected: the brief makes it a fallback only, and the demo would then show
  someone else's JSON-LD processor in front of Varve.
- **Send JSON-LD to the server** as a Graph Store body. Rejected: one catalog
  commit touches several package versions' graphs plus the catalog log, and a
  Graph Store write names one graph, so a catalog commit would be several Varve
  commits. The server's context handling would also become part of the
  reproducibility story.
- **Let the importer fetch contexts live.** Rejected: C5 (reproducible from a
  pinned snapshot), and a viewer offline could not run S1.
- **Skip a leaf that fails.** Rejected: the S2 answer would silently omit a
  package; an omission presented as an answer is faking.

## Consequences

- `Varve.JsonLd` is a dependency at `VarveVersion`; this ADR admits it.
- The snapshot carries the context documents, and its manifest hashes them.
- A JSON-LD bug in Varve stops the demo's import until a release fixes it,
  which is the brief's intent: the gap is shown, with its issue.

## Amendment, 2026-11-03: every context is inline, and a remote one fails

Filed unaccepted by session 03, on #5 (slice 1a), at the maintainer's ruling.

**What the catalog does.** Point 2 expected leaves to name remote `@context`
documents. They do not. Every leaf read in slice 1a carries its context inline,
as a JSON object: the 30 406 leaves fetched while finding the window, and
every leaf of the snapshot, base and window. Four distinct inline contexts
occur; the manifest lists them by hash.

**So point 2 becomes:**

1. The snapshot records no context documents. It keeps each leaf's inline
   context with the leaf.
2. The importer's `JsonLdOptions.DocumentLoader` refuses every remote context,
   as `Varve.JsonLd`'s default does. A leaf naming a remote context fails
   loudly; it is never fetched:
   - at build, the snapshot builder refuses a leaf whose context is not an
     inline object, and names it;
   - at import, `Varve.JsonLd` refuses it through the loader, and the import
     stops and names the leaf (point 4).
3. If the catalog ever starts naming remote contexts, that is a change to
   this ADR, not a quiet fetch.

Ruling `ContextsFromTheSnapshot` is superseded by `InlineContextsOnly`, filed
unaccepted. Following Varve's rule that a key is never reused, the old key
leaves the set, and its text is kept in the set's notes.
