# 0004 — The time-travel clock

## Status

**Proposed — filed unaccepted by session 01 of #1, 2026-10-23.** Acceptance is
the maintainer's act on the pull request. Its decision set is
[`the-time-travel-clock`](../decisions/the-time-travel-clock.md).

## Context

The brief: "Imported history has two clocks: Varve's commit timestamp (import
time) and the catalog's commit timestamp (publish time). The slider must not
lie. Decide how catalog time is carried (commit metadata or in-graph) and how
as-of-by-that-time resolves. Anything this needs from Varve is a Varve issue,
not demo code."

What Varve offers, read at `v0.1.0-preview.11`:

- The sequencer assigns every commit timestamp as `max(clock, head's)` and
  accepts none from a request (Varve ADR 0011; `CommitMetadata`'s own comment:
  "the sequencer assigns it, and never accepts one").
- Over HTTP the commit's metadata is fixed by the server: the agent from the
  token, the cause the server-minted request id. No header sets either, and
  Trace Context is deliberately never written to the log (Varve ADR 0094).
- `Varve-As-Of: time:<RFC 3339>` resolves to the greatest position whose
  **Varve** timestamp is at or before the time (Varve ADR 0096). Over an
  import, that is import time: a few minutes covering ten years of catalog.
- As-of reads more than `Limits:MaxAsOfDistance` positions below the head
  (default 10 000) are refused with `422`.

**A point the prompt's facts do not cover.** Through the *embedded* API both
clocks are reachable: `CommitMetadata` (agent, cause, attachments) is public
and settable on a `CommitRequest`, and `DatasetOptions.Clock` is a
`TimeProvider` the host supplies. An embedded importer could stamp each commit
with catalog time by driving the clock. The decision below rejects that on its
merits (alternative 3); it is not ruled out by Varve.

The NuGet catalog carries its own clock: every catalog page item has a
`commitId` and a `commitTimeStamp`, in order. A package's `published` field is
not a clock: an unlisted version's is `1900-01-01T00:00:00Z`.

## Decision

1. **Catalog time is in-graph, in the same commit as the data.** Each importer
   commit also asserts, in the named graph `<urn:varve-demo:catalog-log>`, a
   node for its catalog commit with the catalog `commitId` and
   `commitTimeStamp` (as `xsd:dateTime`). It is the same SPARQL Update, so the
   same Varve commit (ADR 0008): there is no position where the data is present
   and its catalog time is not.
2. **The slider is catalog time, never `published`.** Every as-of answer shows
   three things: the catalog time asked for, the Varve position it resolved to,
   and that position's Varve timestamp. Both clocks are on screen; neither is
   passed off as the other.
3. **Resolution is a time index.** A disposable index of (catalog time,
   position) pairs, folded from the change feed through `FollowAsync`,
   reading each commit's `<urn:varve-demo:catalog-log>` assertion. Catalog time
   `t` resolves to the greatest position whose catalog time is at or before
   `t`, and the read is `Varve-As-Of: position:<n>`. The index uses only the
   public API, is rebuildable from the log at any time, and is never the source
   of truth.
4. **CI cross-checks the index against Varve** by binary search: for sampled
   catalog times, as-of reads of `<urn:varve-demo:catalog-log>` over positions
   find the greatest position whose newest catalog time is at or before `t`,
   and it must equal the index's answer (spec S3, criterion 3).
5. **The server's `Limits:MaxAsOfDistance` is set above the snapshot's commit
   count**, in `compose.yaml` with this ADR cited, so the slider reaches the
   start of the history.
6. **File the Varve issue "as-of by a declared domain clock"**: a dataset
   declares a predicate (or a graph) as its domain clock, and `Varve-As-Of`
   takes a form that resolves against it. If Varve ships it, the index goes and
   this ADR is superseded.

## Alternatives considered

1. **Import in real time**, one catalog commit when its catalog time arrives.
   Rejected: ten years of catalog would take ten years, and a sped-up clock is
   the same lie with a scale factor.
2. **Caller-supplied commit timestamps** over HTTP. Not available: Varve
   accepts none, by design (Varve ADRs 0011, 0094). Asking Varve for it would be
   asking it to let a client forge time in an append-only log, which is the
   opposite of the evidence the demo exists to show.
3. **Drive the embedded store's clock with catalog time**
   (`DatasetOptions.Clock`), or put catalog time in an embedded commit's
   metadata or attachments. Possible, but rejected: Varve's timestamp would then
   claim the commit was made in 2016, which is false; the demo would run the
   store in-process instead of through the authenticated server the other steps
   use (C4); and live commits in S4 would carry real time beside imported ones
   carrying catalog time, in one clock. Attachments are also not something
   `Varve-As-Of` resolves against, so an index would still be needed.
4. **Position arithmetic**: the n-th catalog commit is position n. Rejected: it
   holds only while nothing else ever commits. A settings commit, a test write,
   a re-import or S4's live tail breaks it silently, and a slider built on it
   would lie without failing.
5. **Binary search alone**, with no index: resolve each slider move by as-of
   reads of `<urn:varve-demo:catalog-log>`. Rejected as the primary path:
   `log2(n)` as-of reads per move puts S3's 2 s target at risk on a large
   snapshot. It is kept as the cross-check, which is where its independence
   from the index is worth most.
6. **Carry catalog time in commit metadata** (the brief's other option).
   Not available over HTTP (F4), and the embedded route is alternative 3.

## Consequences

- The importer writes one extra small graph per commit; it is data like any
  other, visible to the viewer and to the diff.
- The index is state outside Varve. It is disposable, rebuilt from the feed,
  and checked against Varve in CI, so it can be wrong only briefly and never
  silently.
- The as-of distance limit is a server setting the demo depends on; a bump
  re-checks its name and default.
- When Varve ships a domain clock, S3 gets simpler and this ADR is superseded.
