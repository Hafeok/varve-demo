---
set: json-ld-ingestion-through-varve-jsonld
namespace: varvedemo
adr: 0006
decisions:
  - key: ReadWithVarveJsonLd
    statement: "The importer reads catalog leaves with Varve.JsonLd, and the N-Quads fallback is not used"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ContextsFromTheSnapshot
    statement: "Remote contexts are recorded in the snapshot and supplied through JsonLdOptions.DocumentLoader, which refuses any other"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: OneUpdatePerCatalogCommit
    statement: "The importer sends one SPARQL Update per catalog commit, so the server never fetches a context"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: MisreadFailsTheImport
    statement: "A leaf Varve rejects or misreads fails the import and becomes a Varve issue, never patched or skipped"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
---

The rulings of [ADR 0006](../adr/0006-json-ld-ingestion-through-varve-jsonld.md), filed by session 01 of #1 on
2026-10-23 and accepted by the maintainer on 2026-10-26, on the foundation's
pull request #2. Every ruling carries `accepted-by` and `accepted-at`.
