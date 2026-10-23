# Decisions

Every ADR in force as a **decision set**: the rulings code cites as types. The
ADR in [`docs/adr/`](../adr/) is the narrative and the reasoning; the set is the
list of what was decided, one line each, in the form
[`DecisionDriven.Analyzers`](https://github.com/Hafeok/decision-driven-analyzers)
reads. The form is the one Varve uses
([its `docs/decisions/README.md`](https://github.com/Hafeok/Varve/blob/main/docs/decisions/README.md)),
with this repository's namespace. This file has no front matter, so the
generator skips it.

## One file per ADR

```yaml
---
set: the-time-travel-clock          # the ADR's file name without its number
namespace: varvedemo                # every varve-demo decision
adr: 0004                           # the ADR this set enumerates
decisions:
  - key: CatalogTimeInGraphSameCommit       # ^[A-Z][A-Za-z0-9]{0,63}$, unique across every file
    statement: "One line, double-quoted, no quote inside"
    accepted-by: mailto:...                 # written by the maintainer, never by a session
    accepted-at: 2026-10-23T00:00:00Z
---
```

- **A key is a ruling.** It identifies the decision across versions and becomes
  the type code cites, `[DesignDecision(typeof(TheTimeTravelClock.CatalogTimeInGraphSameCommit), Scope = …)]`.
  It is never renamed and never reused.
- **A session files every ruling unaccepted** and never writes `accepted-by` or
  `accepted-at`. A ruling filed without acceptance compiles to `[Obsolete]`, so
  citing it is `CS0618` until the maintainer accepts it on the pull request.
  `accepted-at` is the ADR's acceptance date at `T00:00:00Z`.
- **Supersession moves a ruling; it does not delete it.** A ruling a later ADR
  supersedes appears once, in the superseding ADR's set, under the same key.
  An ADR with no ruling in force has no set file.
- **`revoked-at`** is for a ruling withdrawn with no successor. It compiles to
  `CS0619` on every citation, and every use is listed in the report of the
  change that makes it.

The spec tests hold the ledger to its ADRs: every ADR that is not superseded
has a set whose `adr:` matches, every set is named after its file and declares
`namespace: varvedemo`, and every `adr:` names an existing ADR
(`tests/VarveDemo.Spec.Tests/AdrGates.cs`). The generator reports a key claimed
twice or not an identifier at build time.

## A set with no ADR

A decision filed by a session for a finding no ADR answers names where it comes
from with `origin:` in place of `adr:`, is filed without acceptance, and its
body says what the question is and what the alternative would be.
