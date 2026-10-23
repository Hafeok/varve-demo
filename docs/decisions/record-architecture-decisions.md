---
set: record-architecture-decisions
namespace: varvedemo
adr: 0001
decisions:
  - key: FiveSectionAdrs
    statement: "Every ADR is docs/adr/NNNN-slug.md with Status, Context, Decision, Alternatives considered and Consequences, numbered in order and indexed in docs/adr/README.md"
  - key: DecisionSetPerAdr
    statement: "Every ADR in force has a decision set in docs/decisions named after its file, in namespace varvedemo, filed unaccepted by the session that writes it"
  - key: DependencyNeedsAnAdr
    statement: "A package, a container image, a GitHub Action or a service enters the repository only after an ADR admits it"
  - key: ChangeByAmendmentOrSupersession
    statement: "An accepted ADR changes only by a dated add-only amendment or by a superseding ADR, never by editing"
  - key: EnforcedOrItDoesNotBind
    statement: "A decision that can be checked mechanically is checked by a test in tests/VarveDemo.Spec.Tests that the ADR names"
---

The rulings of [ADR 0001](../adr/0001-record-architecture-decisions.md), filed unaccepted by session 01 of
#1 on 2026-10-23. Every citation is `CS0618` until the maintainer accepts them by
adding `accepted-by` and `accepted-at`, which a session never writes.
