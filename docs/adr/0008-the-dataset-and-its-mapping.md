# 0008 — The dataset and its mapping

## Status

**Proposed — filed unaccepted by session 01 of #1, 2026-10-23.** Acceptance is
the maintainer's act on the pull request. Its decision set is
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
a dataset directory, holds the sequencer, and commits with no agent or cause;
the server has no bulk-load endpoint (Varve ADR 0105).

## Decision

1. **The subset**: the top N package ids by total downloads at a pinned date,
   N = 3 000 as a proposal. The id list, the date and the query that produced
   it are committed, so the subset is reproducible.
2. **A snapshot** is: the catalog leaves for those ids between two pinned
   catalog times; the JSON-LD context documents they name (ADR 0006); and a
   manifest of counts (catalog commits, leaves, quads), the time bounds, a
   SHA-256 of every file, and the version of the tool that built it. CI builds
   it, it is published as a GitHub release asset, and every consumer verifies
   it by hash before use.
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
5. **Open, decided by slice 1's first measurement**: replay every catalog
   commit through the server, or bulk-load a base and replay after it. Varve's
   bulk load is one logical commit, so a base flattens the history before it,
   and S3's slider could not reach behind it. It is also embedded only, so it
   would run as `varve load` on the dataset's directory before the server
   opens it, with no agent recorded. The measurement is the time to replay the
   whole snapshot through the server against S1's three-minute target; the
   decision is an amendment to this ADR with the numbers.

## Alternatives considered

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
- If the measurement forces a base load, S3's slider starts at the base and the
  README says where history begins.
- The URN scheme `urn:varve-demo:` is ours; it is not registered, and the
  README says so.
