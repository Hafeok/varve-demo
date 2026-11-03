# 0004 — The time-travel clock

## Status

**Accepted — by the maintainer on 2026-10-26, on the foundation's pull request
[#2](https://github.com/Hafeok/varve-demo/pull/2).** Filed by session 01 of
#1, 2026-10-23. Its decision set is
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
- An as-of read is bounded by `Varve:Limits:MaxAsOfDistance` (default
  10 000). The bound is **not** a distance from the head. It bounds how many
  commits an as-of read replays from the log: the distance from the nearest
  checkpoint at or below the position, or 0 for a position at or below the
  newest extent set's end, which the extents answer with no replay
  (`Dataset.AsOfLogDistance`, Varve's `src/Varve.Store/DatasetExtents.cs:1305-1322`;
  the check in `src/Varve.Protocol/Http/Reads.cs:179-198`). Beyond it is
  `422 as-of-distance-exceeded`, decided before any log is read, "so an as-of
  read never scans an unbounded log" (Varve's decision
  `AsOfDistanceBoundedBeforeTheViewOpens`, ADR 0114). With no checkpoint, the
  distance of position `p` is `p` itself.
- **The server takes no checkpoints of its own.** `DatasetOptions.Checkpoints`
  is a `CheckpointPolicy` (every `EveryCommits` commits or `EveryLogBytes`
  bytes), `CheckpointPolicy.Never` by default (`src/Varve.Store/Dataset.cs:72-74`).
  The server builds its `DatasetOptions` without one
  (`src/Varve.Server/OpenDatasets.cs:402`), and its settings have no key for
  one (`src/Varve.Server/Settings.cs`). Varve's operator guide says so: "the
  server's defaults write none" (`docs/operator/backup-and-restore.md:58-59`).
  A checkpoint can be taken on demand, by an operator, with
  `POST /manage/datasets/{name}/checkpoints` (`ManagementEndpoints.cs:389`) or
  `varve checkpoint` on a dataset no server holds.
- The checkpoint route needs the `admin` permission on the dataset
  (`DatasetPermissions.Admin`, `src/Varve.Protocol/Endpoints/AdminEndpoints.cs:247`,
  which `ManagementEndpoints.CheckpointAsync` calls), with a token for the
  management plane's audience (`src/Varve.Server/ServerHost.cs:376-381, 415`).
  ADR 0007 says which client holds it.
- A dataset's checkpoints are listed by `GET /manage/datasets/{name}/status`
  (`src/Varve.Protocol/VarveEndpoints.cs:99-100`), which needs the same `admin`
  permission (`src/Varve.Protocol/Endpoints/StatusEndpoint.cs:29, 60-65`).
  No read response names the checkpoint it started from.

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
3. **Every as-of answer also shows where Varve started the read.** The screen
   states that an as-of read anywhere in history is served from checkpoints,
   whose extents carry validity intervals (Varve ADR 0138), not by replaying
   the log. Each answer then says "served from the extents up to checkpoint
   `c`, no log replayed" for a position at or below the newest checkpoint, or
   "`n` commits replayed after checkpoint `c`" above it. The demo derives this
   from the checkpoint list (`GET /manage/datasets/{name}/status`, by the
   operator client) and Varve's rule above. No Varve response names it on
   success, so CI holds the derivation to Varve with a CI-only server whose
   `MaxAsOfDistance` is 0 (allowed: zero or more,
   `src/Varve.Server/SettingsCheck.cs:268`). At that bound every as-of read
   with a non-zero distance is refused with `422`, its `actual` member the
   distance, and a read that succeeds has distance 0, by Varve's own check:
   - **before the final checkpoint**, an as-of read at a position `p` is
     refused with `actual` equal to `p`, the distance the demo computes;
   - **after the final checkpoint**, an as-of read at an early position
     succeeds: distance 0, so the reading of `DatasetExtents.cs` above is
     tested, not cited. A read above the newest checkpoint is refused with
     `actual` equal to its distance from it.

   This is evidence about the storage engine, shown on screen, not a
   footnote.
4. **Resolution is a time index.** A disposable index of (catalog time,
   position) pairs, folded from the change feed through `FollowAsync`,
   reading each commit's `<urn:varve-demo:catalog-log>` assertion. Catalog time
   `t` resolves to the greatest position whose catalog time is at or before
   `t`, and the read is `Varve-As-Of: position:<n>`. The index uses only the
   public API, is rebuildable from the log at any time, and is never the source
   of truth.
5. **CI cross-checks the index against Varve** by binary search: for sampled
   catalog times, as-of reads of `<urn:varve-demo:catalog-log>` over positions
   find the greatest position whose newest catalog time is at or before `t`,
   and it must equal the index's answer (spec S3, criterion 3).
6. **The default bound stays, and checkpoints keep every position in reach.**
   `MaxAsOfDistance` is left at Varve's default. The rule:
   - **one checkpoint at the end of the import**, which puts every imported
     position at or below the newest extent set's end, distance 0;
   - **then a checkpoint whenever more than K commits have landed since the
     last one**, so a position above the newest checkpoint is at most K away
     from it.

   K is configuration, `VarveDemo:Checkpoints:EveryCommits`, proposed at 5 000.
   It must be below `MaxAsOfDistance`, and the importer checks that at start
   against the limit the server advertises in its service description
   (`varve:maxAsOfDistance`, `src/Varve.Protocol/Endpoints/ServiceDescriptionEndpoint.cs:239`).
   It refuses to run otherwise. There are **no checkpoints during the
   import**: S3 reads nothing until the import has finished, and the final
   checkpoint covers all of it.
7. **The importer takes the checkpoints, as the operator.** Varve's server
   cannot be configured with a checkpoint policy (see Context), and its
   operator guide gives an on-demand checkpoint as the way to operate it
   without one. So the importer calls `POST /manage/datasets/{name}/checkpoints`
   with the **operator client**, in the operator's role, by the rule in
   point 6. This is documented public API used as documented; it is not a
   workaround. S3's screen says so: "checkpoints are taken by the importer, as
   the operator, because Varve's server has no checkpoint policy yet", with
   the Varve issue linked.
8. **The Varve issue stays.** "A checkpoint policy configurable on the server"
   is still drafted for Varve. **When Varve ships it, the importer stops
   calling the endpoint**, the server is configured with the equivalent
   policy, and this ADR is amended to say so.
9. **File the Varve issue "as-of by a declared domain clock"**: a dataset
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
7. **Wait for a server-side checkpoint policy, and show the `422` until then**
   (this ADR's second draft, before the review's ruling). Rejected in review: Varve documents an operator
   taking checkpoints on demand as the way to run a server without a policy,
   so doing that is operating Varve as documented. Showing a refusal the
   documented procedure avoids would understate Varve.
8. **Checkpoint at least every `MaxAsOfDistance` commits during the import**
   (also the second draft). Rejected in review: one checkpoint at the end
   already serves all imported history at distance 0, and nothing reads the
   dataset as of a position while the import runs. Checkpoints during the
   import would cost import time against S1's target for no reader.
9. **Raise `MaxAsOfDistance` above the snapshot's length** (this ADR's first
   draft). Rejected in review: the bound exists so that an as-of read never
   replays an unbounded log. Raising it past the snapshot disables it, and every
   slider move could then replay the whole log, which is the opposite of the
   evidence S3 is for.

## Consequences

- The importer writes one extra small graph per commit; it is data like any
  other, visible to the viewer and to the diff.
- The index is state outside Varve. It is disposable, rebuilt from the feed,
  and checked against Varve in CI, so it can be wrong only briefly and never
  silently.
- S3 depends on checkpoints, and the importer depends on the operator client
  holding `admin` on the dataset (ADR 0007).
- S3's checks need a second, CI-only server configuration with
  `MaxAsOfDistance` 0. It changes no step a viewer runs.
- The importer does an operator's job until Varve's server can be configured
  to do it. The screen says so, and the Varve issue stays open for it.
- A bump re-checks the bound's meaning, its default, and whether the server
  has gained a checkpoint policy.
- When Varve ships a domain clock, S3 gets simpler and this ADR is superseded.

## Amendment, 2026-11-03: re-checked against `v0.1.0-preview.15`

Filed unaccepted by session 03, on #3, the bump to `0.1.0-preview.15`.

**The checkpoint and distance reasoning holds.** Between `v0.1.0-preview.11`
and `v0.1.0-preview.15`:

- `Dataset.AsOfLogDistance` is unchanged, body and comment; it moved to
  `src/Varve.Store/DatasetExtents.cs:1396-1413`. The `422` check in
  `src/Varve.Protocol/Http/Reads.cs:179-198`, `ProtocolLimits.MaxAsOfDistance`
  (default 10 000), `SettingsCheck.cs:268` and the service description's
  `varve:maxAsOfDistance` (`ServiceDescriptionEndpoint.cs:239`) are
  unchanged.
- The server still takes no checkpoints of its own:
  `DatasetOptions.Checkpoints` is still `CheckpointPolicy.Never`
  (`src/Varve.Store/Dataset.cs:72-74`), the server builds its options without
  one, now at `src/Varve.Server/OpenDatasets.cs:413`, and `Settings.cs` is
  unchanged. The operator guide's sentence is unchanged
  (`docs/operator/backup-and-restore.md:58-59`).
- The checkpoint route is now `ManagementEndpoints.cs:390-399`, unchanged in
  substance. The status endpoint still demands `admin`; that call is at
  `src/Varve.Protocol/Endpoints/StatusEndpoint.cs:30`, and was at line 30 at
  `v0.1.0-preview.11` too, so the Context's "29" was off by one when written.
  `v0.1.0-preview.13` added an `events` member to the status body (Varve
  ADR 0167); the `checkpoints` array the demo reads is unchanged.

**What `v0.1.0-preview.15` changes, and why it does not move this ADR.** As-of
reads inside an extent set now seek instead of narrowing the set (Varve
ADR 0177, derived format 5 for extents), and `MaxSetBytes` now bounds merges,
not reads (Varve ADR 0178; ADR 0159 amended). Both change what an as-of read
served from the extents *costs*, not whether it replays the log: the distance
is still 0 at or below the newest extent set's end, and the bound still
counts commits replayed above the nearest checkpoint. Point 3's wording,
"served from the extents up to checkpoint `c`, no log replayed", stays true
and is now cheaper. This ADR never relied on `MaxSetBytes` bounding a read.

**The Varve issues are filed.** Point 8's "a checkpoint policy configurable on
the server" is [Hafeok/Varve#159](https://github.com/Hafeok/Varve/issues/159);
point 9's "as-of by a declared domain clock" is
[Hafeok/Varve#157](https://github.com/Hafeok/Varve/issues/157). The operator
guide's description of `MaxAsOfDistance` as a distance below the head, which
led this ADR's first draft astray, is
[Hafeok/Varve#161](https://github.com/Hafeok/Varve/issues/161); at
`v0.1.0-preview.15` `docs/operator/configure.md:169` still says it.

## Amendment, 2026-11-03: the checkpoints are taken by a dataset admin

Filed unaccepted by session 03, on #5. Point 7's "through the operator
client" is narrowed by ADR 0007's amendment of this date: the importer takes
the checkpoints through `varve-demo-checkpointer`, which holds `admin` on the
dataset `nuget` and nothing more, on the same route. The bootstrap operator
only declares the dataset and its grants. S3's screen says the importer
takes the checkpoints "as the dataset's admin", not "as the operator".

**Every K commits, and slice 1a.** Point 6 says there are no checkpoints
during the import, one at its end, and then one whenever more than K commits
have landed since the last. Slice 1a's importer does exactly that: it checks
K against `maxAsOfDistance` at start, and takes the final checkpoint. The
"every K" rule binds the commits after the import, which arrive with S4's
live tail in slice 2. The hand-off prompt for slice 1a asked for a
checkpoint every K commits during the import; this ADR's point 6 and its
alternative 8 reject that, and the importer follows the ADR.

## Amendment, 2026-11-03: the base, and the superseded checkpoint ruling

Filed unaccepted by session 03, on #5, at the maintainer's rulings.

1. **The ruling for point 7 changes key.** `ImporterTakesCheckpointsAsOperator`
   is superseded by `ImporterTakesCheckpointsAsDatasetAdmin`, filed unaccepted:
   the same route, through `varve-demo-checkpointer`, which holds `admin` on
   the dataset alone. A key never changes meaning; the old one leaves the set,
   and its text is kept in the set's notes.
2. **The base sits at the window's lower bound T0** (ADR 0008's amendment of
   2026-11-03). The import loads the catalog's state of the N packages as of
   T0 first, one commit per package. Each base commit asserts, in
   `<urn:varve-demo:catalog-log>`, a node
   `<urn:varve-demo:base:{id}>` with `vd:basePackage` and `vd:baseAsOf` T0
   (ADR 0010's amendment). Point 4's time index reads `vd:baseAsOf` as those
   positions' catalog time, so every base position resolves to T0, and
   catalog time T0 resolves to the last base position.
3. **S3's slider starts at T0**, and its screen says the base is the
   catalog's state as of T0 and that history before T0 is not replayed. A
   viewer who wants it runs `varve-demo import --since <date>`.

Point 6's checkpoint rule is unchanged, as the maintainer ruled: no
checkpoints during the import, one at its end.

## Amendment, 2026-11-03: memory, measured; checkpoints during the import are not proposed

Filed unaccepted by session 03, on #5, after the maintainer's ruling to test
the import under a memory limit, and to propose checkpoints during long
imports for memory if the server failed under it.

**What was measured.** Varve `v0.1.0-preview.15`, on this session's machine.
The server is limited to 512 MB with a compose override (CI only, never the
viewer's compose).

| Import | Under 512 MB |
|---|---|
| the window, whole-leaf model (1 731 commits, 1.75M quads) | refused at commit 607: `400 operation-failed`, "Insufficient memory" |
| the base and window, advisory-state model (ADR 0010's amendment) | refused at base commit 1 068 of 3 000, about 1.41M quads: `503 unavailable`, "The default projection failed at position 1067 and must be rebuilt: Insufficient memory" |
| the same, with a checkpoint every 250 commits | refused at the same commit |

In none was the container OOM-killed. The server refuses the write and keeps
running.

Resident memory, without a limit, through S1 with the base:

- 45 MB at start;
- 576 MB at the end of the import, before the final checkpoint;
- 1.10 GB after it;
- 133 MB after a restart.

After a refusal under 512 MB:

- a restart under the same limit does not reopen the dataset within 60 s;
  it cycles between `closed` and not ready;
- a restart without the limit reopens it in about 10 s, at 680 MB to 705 MB,
  with every acknowledged commit present.

**What it means.** The server's memory grows with the data committed since
the process started. A checkpoint does not release it within the running
process: a checkpoint every 250 commits changed nothing. Opening a dataset
needs memory proportional to the commits above its newest checkpoint. The log
on disk is safe: no acknowledged commit was lost in any run.

**So checkpoints during the import are not proposed.** They were the remedy
the ruling named. They were measured not to bound the importing process's
memory, so adding them would cost import time for nothing S1 needs. Point 6
stands unchanged. The finding is a Varve issue draft, in the slice's
traceability record, with these numbers and the reproduction.
`ImportUnderAMemoryLimit` keeps measuring it in CI, and fails only if an
acknowledged commit is lost. The importer keeps
`VarveDemo:Checkpoints:ForMemoryEveryCommits`, 0 by default, so the
measurement can be repeated on a later Varve.
