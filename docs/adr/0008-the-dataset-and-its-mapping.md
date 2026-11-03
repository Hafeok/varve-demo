# 0008 — The dataset and its mapping

## Status

**Accepted — by the maintainer on 2026-10-26, on the foundation's pull request
[#2](https://github.com/Hafeok/varve-demo/pull/2).** Filed by session 01 of
#1, 2026-10-23. Its decision set is
[`the-dataset-and-its-mapping`](../decisions/the-dataset-and-its-mapping.md).

## Context

The brief: "a subset of the NuGet catalog (the top few thousand packages),
which is itself an append-only log of package publishes, edits and deletes. One
catalog commit maps to one Varve commit."

The NuGet catalog is a sequence of pages; each item is a leaf with a
`commitId` and a `commitTimeStamp`, of type `PackageDetails` (a version
published or edited: listing, deprecation, vulnerabilities) or `PackageDelete`.
Every leaf of one catalog commit shares its `commitId`. A details leaf is the
whole current state of a package version, not a delta.

What Varve offers for loading, read at `v0.1.0-preview.11`: over HTTP a write
request is at most one commit (Varve ADR 0094). A bulk load is one logical
commit (Varve ADRs 0076, 0081), and it is **embedded only**: `varve load` opens
a dataset directory, holds the sequencer, and commits with no agent or cause
(`src/Varve.Server/Commands/Load.cs:27, 64`); the server has no bulk-load
endpoint (Varve ADR 0105).

## Decision

1. **The subset**: the top N package ids by total downloads at a pinned date,
   N = 3 000 as a proposal. The id list, the date and the query that produced
   it are committed, so the subset is reproducible.
2. **A snapshot has two parameters, both in its manifest**: N, the number of
   top packages (3 000), and a **catalog time window**.
   - The window's **lower bound** is where the catalog starts carrying
     deprecation and vulnerability data on its leaves, because S2 and S3 depend
     on it. That date is **not** taken from memory: slice 1 determines it from
     the catalog itself and records it here, with the evidence, by a dated
     amendment.
   - The **upper bound** is the snapshot date.

   A snapshot is the catalog leaves for those ids inside the window; the
   JSON-LD context documents they name (ADR 0006); and a manifest of N, the
   window, counts (catalog commits retained, leaves, quads), a SHA-256 of every
   file, and the version of the tool that built it. CI builds it, it is
   published as a GitHub release asset, and every consumer verifies it by hash
   before use.
3. **One catalog commit that touches the subset is exactly one Varve commit**,
   in catalog order: one SPARQL Update per catalog commit (ADR 0006), carrying
   its catalog-log assertion (ADR 0004). A catalog commit that touches no
   package in the subset is not imported.
4. **One named graph per package version**,
   `<urn:varve-demo:pkg/{id}/{version}>`, with `{id}` lower-cased and
   `{version}` normalised as NuGet normalises it, both percent-encoded. A
   details leaf **replaces** its graph (`DROP SILENT GRAPH` then `INSERT
   DATA`, in one update); a delete leaf **drops** it. Varve records the
   effective delta, so S3's diff shows only what changed.
5. **S1 is a full replay inside the window.** Every retained catalog commit is
   replayed over HTTP, authenticated as the importer, one Varve commit each,
   in catalog order. There is no base and no bulk load. (Closed in review; the
   first draft left it open.)
6. **The window is the knob, never N.** Slice 1 measures the retained commit
   count and the replay rate, and tunes the window to meet S1's target. It
   never lowers N. If no window that still contains the S2 and S3 story meets
   the target, the target moves, by a pull request to the specification with
   the measurement, and the README says so.

## Alternatives considered

- **Bulk-load a base, then replay after it** (the first draft's open option).
  Rejected in review, for four reasons:
  - it is embedded only: `varve load` needs the dataset's directory before any
    server holds it, so S1 would be two processes taking turns, not one
    server;
  - it records no agent, against ADR 0007, under which every write is
    authenticated and so every commit names who made it;
  - it is one logical commit, so it flattens the history before the base, and
    S3's slider could not reach behind it;
  - the brief's evidence is about the server a viewer runs, and a load that
    bypasses it shows nothing about that server.
- **Shrink N to meet the target.** Rejected: N is the "top few thousand" the
  brief names. A smaller window keeps S2's answer complete for today's
  packages; a smaller N would drop packages from the lock file's answer.

- **All of nuget.org.** Rejected: millions of packages put S1's target out of
  reach, and the snapshot would be too large to publish as a release asset.
- **A hand-picked subset.** Rejected: a choice by downloads is reproducible and
  free of a suspicion of cherry-picking.
- **Batch several catalog commits into one Varve commit.** Rejected by the
  brief, and because S3 and S4 would lose the catalog's own granularity.
- **One graph per package id, or one default graph.** Rejected: a details leaf
  is the state of one version; replacing a per-version graph is the natural
  effective delta. A per-id graph would make every leaf rewrite every version
  of its package, and the default graph would make replacement a pattern
  delete.
- **Append leaves without replacing** (keep every leaf as data). Rejected for
  the data graphs: history is Varve's job, and S3 shows it through as-of
  reads. Keeping every leaf would duplicate the log inside the projection.
- **Fetch leaves live at import.** Rejected by C5: the snapshot is pinned and
  hashed.

## Consequences

- The importer must group leaves by `commitId` and keep catalog order.
- The snapshot is a released artefact with its own tool version; changing the
  mapping means a new snapshot.
- S3's slider starts at the window's lower bound, and the README says where
  history begins and why.
- S1's time is the replay's time. The rate shown is commits and quads per
  second over HTTP, which is itself evidence about the server.
- The URN scheme `urn:varve-demo:` is ours; it is not registered, and the
  README says so.

## Amendment, 2026-11-03: the window, measured from the catalog

Filed unaccepted by session 03, on #5 (slice 1a). Point 2 asks slice 1 to find
the window's lower bound from the catalog, with the evidence. Point 6 makes
the window the knob that meets S1's target.

**The subset.** The top 3 000 ids by total downloads came from nuget.org's
search on 2026-11-03T11:34:33Z (`snapshot/query.json`, `snapshot/ids.txt`).

**How the catalog was read.** Every page of the catalog index (23 462 pages,
read on 2026-11-03) was scanned for items whose `nuget:id` is in the subset:
890 024 leaves, 2015 to 2026. Leaves were then fetched and checked for a
`deprecation` member and a `vulnerabilities` member:

- every subset details leaf committed from 2019-01-01 to 2019-10-02, and from
  2020-04-01 to 2020-07-18;
- 150 subset leaves a month, chosen with a fixed seed, from 2018-01 to
  2022-12.

**The first deprecation data** among the subset: catalog commit
`a70b11ea-c7fa-4caf-9ad4-491d7ed7fcd1` at `2019-09-23T20:19:02.5590692Z`, on
catalog page 8916, leaf
`https://api.nuget.org/v3/catalog0/data/2019.09.23.20.19.02/nuget.core.2.14.0.json`
(NuGet.Core 2.14.0). No subset leaf from 2019-01-01 up to that commit carries
`deprecation`, and none of the 2018 sample does.

**The first vulnerability data** among the subset: catalog commit
`86092097-3e3a-45c4-93da-a112c8cfc71b` at `2020-07-17T01:23:23.8340284Z`, on
catalog page 10556, leaf
`https://api.nuget.org/v3/catalog0/data/2020.07.17.01.23.23/dotnetzip.1.9.2.json`
(DotNetZip 1.9.2). No subset leaf from 2020-04-01 up to that commit carries
`vulnerabilities`, and none of the sample from 2018-01 does.

**What a window from there would cost.** From 2019-09-23 to 2026-11-03 the
subset has 237 767 catalog commits and 657 014 leaves. From 2020-07-17 it
has 214 921 commits and 614 895 leaves. The import measured below runs at
about 34 commits and 35 000 quads a second, and a leaf averages 334 quads.
Either window is a replay of hours, not minutes.

**The rate, measured.** Varve `v0.1.0-preview.15` in a container, on this
session's machine (a cloud container, not the CI runner). The import is
two weeks, below, and the importer runs on the host:

| | |
|---|---|
| retained catalog commits | 1 731 |
| quads | 1 753 337 |
| import | 50.6 s: 34.2 commits/s, 34 639 quads/s |
| final checkpoint | 12.5 s |
| server resident memory | 53 MB before, 713 MB after |

Four weeks (2026-10-06 to 2026-11-03) is 3 399 commits but 12 579 247 quads,
since one advisory reflow rewrote hundreds of large SixLabors.ImageSharp
leaves several times. At the same rate that is about six minutes, over S1's
budget.


**The decision: a base as of T0, then the window** (the maintainer's ruling
of 2026-11-03). A window alone was rejected for these reasons:

- a version enters the snapshot only if a leaf lands in the window, so a
  viewer's own lock file would mostly answer "outside the subset";
- "lowest clean version" would ignore most versions;
- a sample that passes because it was picked from the window would read as
  rigged.

So:

1. **The base.** For every version of the N packages, its latest catalog leaf
   before the window's lower bound T0. The snapshot builder derives it from
   every catalog page before T0, deterministically: the latest leaf by
   `commitTimeStamp`, ties by URL. A version whose latest leaf before T0 is a
   delete is absent. The base's files hold each leaf pruned to the members
   the graph model reads, by the rule the manifest records (ADR 0010's
   amendment). The window's leaves are whole.
2. **The import loads the base first, over HTTP**, one Varve commit per
   package, as the importer, so every base commit carries the importer's
   agent (ADR 0007). Each base commit asserts in
   `<urn:varve-demo:catalog-log>` that it is the package's state as of T0
   (ADR 0010's amendment), so the time index places it at T0 (ADR 0004's
   amendment). Then the window is replayed, one commit per catalog commit,
   as before. The importer refuses a dataset that is not empty.
3. **What the viewer is told.** S1's screen and S3's slider say plainly that
   the base is the catalog's state as of T0, that history before T0 is not
   replayed, and that the slider starts at T0. S2's "outside the subset"
   means outside the N packages.
4. **The deep history stays rerunnable.** `varve-demo import --since <date>`
   rebuilds from the catalog every catalog commit from that date to the
   snapshot's upper bound, with no base, and replays it. It is never run in
   the step checks.
5. **T0 is 2026-10-20T00:00:00Z and the upper bound 2026-11-03T00:00:00Z**
   (`snapshot/inputs.json`). The window holds S3's story: advisory
   `GHSA-v76p-62qx-wwq2` is added to SixLabors.ImageSharp 3.0.0, 3.0.1,
   3.0.2 and 15 other versions at `2026-10-26T16:04:33.9723859Z`, after
   earlier leaves of the same versions inside the window.

**The snapshot** (`snapshot/manifest.json`, sha256
`edb36caaa27f45b517f6977987bb7e9f1202e72f5e567085db24deb39275dc9e`):

| | |
|---|---|
| base | 3 000 packages, 512 390 versions (46 deleted before T0), 3 018 866 quads |
| window | 1 731 catalog commits retained, 27 138 skipped, 5 255 leaves, 36 128 quads |
| files | 661 MB on disk; 22.5 MB as one `tar.gz` release asset, far below GitHub's 2 GB per asset, so not split |
| built from nuget.org | 24 min 50 s on this session's machine: every catalog page, and about 517 000 leaves |

**The projection, before the full run** (the maintainer's ruling). Samples
were taken from the base with the seed `varve-demo slice 1a projection
2026-11-03`:

- **1% of versions:** 5 137 versions, 4.73 quads a version under the model.
- **1% and 5% of packages, every version of each**, for realistic commit
  sizes: the 5% sample, 23 788 versions, loaded at 27 235 quads a second.

The importer's own CPU was the bottleneck. The base was projected at about
2.43M quads and 85 s to 90 s, and S1 at about 134 s here, about 100 s on CI's
faster runner. Measured since, with the package id added (ADR 0010's
amendment):

| S1 on this session's machine | |
|---|---|
| `docker compose up` to the first answer, snapshot mounted | 109 s; in the check, 113.6 s |
| the same, snapshot downloaded over HTTP | 125.5 s, with the deep-history run competing for the CPU |
| base | 72.6 s to 84.9 s, 35 000 to 41 000 quads a second |
| window | 12.4 s to 14.0 s |
| final checkpoint | 16.3 s to 20.9 s |

S1's target of 3 minutes holds. CI's numbers are in the pull request.

**CI** rebuilds the snapshot from nuget.org only when what it is a function of
changes (`snapshot/`, the builder, the mapping), weekly, and on
`workflow_dispatch`. A rebuild that differs from the committed manifest fails
the run. Every other run downloads the published asset, verifies it file by
file against the manifest, and caches it by its hash. The cache is
`actions/cache`, which this amendment admits at the SHA Varve's workflows pin
(`55cc8345863c7cc4c66a329aec7e433d2d1c52a9`, v6).

Rulings superseded by this amendment, under new keys filed unaccepted:

- `SnapshotIsAHashedReleaseAsset` by `SnapshotIsBaseAndWindowHashed`;
- `WindowStartsWhereTheCatalogCarriesAdvisories` by
  `BaseAtTheWindowsLowerBound`;
- `FullReplayOverHttp` by `BaseThenFullReplayOverHttp`.

The old keys leave the set, and their text is kept in its notes.
