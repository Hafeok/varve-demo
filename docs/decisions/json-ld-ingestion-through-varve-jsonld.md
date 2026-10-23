---
set: json-ld-ingestion-through-varve-jsonld
namespace: varvedemo
adr: 0006
decisions:
  - key: ReadWithVarveJsonLd
    statement: "The importer reads catalog leaves with Varve.JsonLd, and the N-Quads fallback is not used"
  - key: ContextsFromTheSnapshot
    statement: "Remote contexts are recorded in the snapshot and supplied through JsonLdOptions.DocumentLoader, which refuses any other"
  - key: OneUpdatePerCatalogCommit
    statement: "The importer sends one SPARQL Update per catalog commit, so the server never fetches a context"
  - key: MisreadFailsTheImport
    statement: "A leaf Varve rejects or misreads fails the import and becomes a Varve issue, never patched or skipped"
---

The rulings of [ADR 0006](../adr/0006-json-ld-ingestion-through-varve-jsonld.md), filed unaccepted by session 01 of
#1 on 2026-10-23. Every citation is `CS0618` until the maintainer accepts them by
adding `accepted-by` and `accepted-at`, which a session never writes.
