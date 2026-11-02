---
set: the-dataset-and-its-mapping
namespace: varvedemo
adr: 0008
decisions:
  - key: TopPackagesByDownloads
    statement: "The subset is the top N package ids by downloads at a pinned date, N 3000 as a proposal, with the list, date and query committed"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: SnapshotIsAHashedReleaseAsset
    statement: "A snapshot is the subset leaves inside its window, their context documents and a manifest of N, the window, counts, hashes and tool version, built by CI and verified by hash"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: SnapshotHasNAndAWindow
    statement: "A snapshot has two parameters in its manifest: N top packages, 3000, and a catalog time window whose upper bound is the snapshot date"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: WindowStartsWhereTheCatalogCarriesAdvisories
    statement: "The window's lower bound is where the catalog starts carrying deprecation and vulnerability data, determined from the catalog in slice 1 and recorded by amendment, never taken from memory"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: OneCatalogCommitOneVarveCommit
    statement: "Each catalog commit that touches the subset becomes exactly one Varve commit, in catalog order"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: GraphPerPackageVersion
    statement: "Each package version is the named graph urn:varve-demo:pkg/id/version, replaced by a details leaf and dropped by a delete"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: FullReplayOverHttp
    statement: "S1 replays every retained catalog commit over HTTP as the importer, one Varve commit each, with no base and no bulk load"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: TuneTheWindowNeverN
    statement: "Slice 1 measures the retained commit count and replay rate and tunes the window, never N, to meet S1's target; if no window keeping the S2 and S3 story meets it, the target moves and the README says so"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
---

The rulings of [ADR 0008](../adr/0008-the-dataset-and-its-mapping.md), filed by session 01 of #1 on
2026-10-23 and accepted by the maintainer on 2026-10-26, on the foundation's
pull request #2. Every ruling carries `accepted-by` and `accepted-at`.
