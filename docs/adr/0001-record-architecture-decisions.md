# 0001 — Record architecture decisions

## Status

**Proposed — filed unaccepted by session 01 of #1, 2026-10-23.** Acceptance is
the maintainer's act on the pull request. Its decision set is
[`record-architecture-decisions`](../decisions/record-architecture-decisions.md).

## Context

The brief requires "ADRs for decisions and new dependencies", as Varve keeps
them. This repository is built by sessions that start with nothing but the
repository. A decision that lives only in a conversation is lost to the next
session, and a decision that can be edited in place loses the reasoning that
tells a later reader whether its circumstances have changed.

The demo also has an unusual exposure: it is a claim about someone else's
software. Every image, package and service it pulls is part of what a sceptical
viewer is asked to trust, so where each one comes from and why must be on the
record before it arrives.

Varve's own practice (its ADRs 0001, 0009 and 0068) is the model: a five-part
ADR, a register of dependencies that cites ADRs, and dated amendments.

## Decision

1. **Form.** ADRs live in `docs/adr/NNNN-slug.md`, numbered from 0001, in order,
   never reused or renumbered. Each has five sections in this order: **Status**,
   **Context**, **Decision**, **Alternatives considered**, **Consequences**.
   `docs/adr/README.md` indexes every one.
2. **A decision set per ADR.** Each ADR in force has a set at
   `docs/decisions/<slug>.md` in the form `DecisionDriven.Analyzers` reads
   (namespace `varvedemo`; `docs/decisions/README.md`), so code can cite a
   ruling as a type. A session files every ruling **unaccepted** and never
   writes `accepted-by`.
3. **Every dependency needs an ADR before it enters the repository**: a
   package, a container image, a GitHub Action or an external service. A
   package's `PackageVersion` names it with `Adr="NNNN"`; an image or an action
   names it in a comment beside its pin.
4. **A decision changes by a dated amendment or a supersession, never by
   editing an accepted ADR.** An amendment is an add-only block at the end,
   headed with its date, and may refine or extend; a change of meaning is a new
   ADR whose Status names the one it supersedes, and the superseded ADR's
   Status line is the only line of it that changes. A ruling a later ADR
   supersedes moves to that ADR's set under the same key.
5. **Enforced, or it does not bind.** Where a decision can be checked
   mechanically, the check is a test in `tests/VarveDemo.Spec.Tests` and the ADR
   names it. The gates for this ADR: numbers without gaps, a set for every ADR
   not superseded whose `adr:` matches, every set named after its file, and the
   index linking every ADR.

## Alternatives considered

- **A single decisions log.** One file, appended to. Rejected: supersession
  becomes an edit to shared text, and one decision's diff is no longer
  reviewable alone.
- **ADRs without decision sets.** Simpler, one file per decision. Rejected: the
  brief names `DecisionDriven.Analyzers`, and its rules need the ledger to
  cite; a decision code cannot name is one the build cannot hold code to.
- **A dependency register without ADRs** (a table of packages and reasons).
  Rejected: an image or a service brings a trust question, not just a licence,
  and the alternatives are the useful part of the record.
- **Editing accepted ADRs with a changelog line.** Rejected for the reason
  Varve's ADR 0001 gives: the reasoning stops being readable as it was written.

## Consequences

- Adding a package, image, action or service is two changes, the ADR and the
  pin, and the gate refuses the pin alone.
- Each ADR carries a second file to keep in step; the gates catch a set that is
  missing or misnamed, not one whose rulings drift from the prose. That is a
  review duty.
- Until the maintainer accepts a set, citing one of its rulings in code is
  `CS0618`, so code that needs one waits for acceptance or carries the warning
  as a known red on its pull request.
- The GitHub Actions in `.github/workflows/ci.yml` are dependencies under point
  3; this ADR set admits none of them on its own, so ADR 0003 does.
