---
set: the-dataset-and-its-mapping
namespace: varvedemo
adr: 0008
decisions:
  - key: TopPackagesByDownloads
    statement: "The subset is the top N package ids by downloads at a pinned date, N 3000 as a proposal, with the list, date and query committed"
  - key: SnapshotIsAHashedReleaseAsset
    statement: "A snapshot is the subset leaves between two catalog times, their context documents and a manifest of counts, bounds, hashes and tool version, built by CI and verified by hash"
  - key: OneCatalogCommitOneVarveCommit
    statement: "Each catalog commit that touches the subset becomes exactly one Varve commit, in catalog order"
  - key: GraphPerPackageVersion
    statement: "Each package version is the named graph urn:varve-demo:pkg/id/version, replaced by a details leaf and dropped by a delete"
  - key: ReplayOrBaseByMeasurement
    statement: "Whether to replay everything or bulk-load a base and replay is decided by slice 1's first measurement, by amendment"
---

The rulings of [ADR 0008](../adr/0008-the-dataset-and-its-mapping.md), filed unaccepted by session 01 of
#1 on 2026-10-23. Every citation is `CS0618` until the maintainer accepts them by
adding `accepted-by` and `accepted-at`, which a session never writes.
