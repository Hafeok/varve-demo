---
set: the-dataset-and-its-mapping
namespace: varvedemo
adr: 0008
decisions:
  - key: TopPackagesByDownloads
    statement: "The subset is the top N package ids by downloads at a pinned date, N 3000 as a proposal, with the list, date and query committed"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: SnapshotHasNAndAWindow
    statement: "A snapshot has two parameters in its manifest: N top packages, 3000, and a catalog time window whose upper bound is the snapshot date"
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
  - key: TuneTheWindowNeverN
    statement: "Slice 1 measures the retained commit count and replay rate and tunes the window, never N, to meet S1's target; if no window keeping the S2 and S3 story meets it, the target moves and the README says so"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: SnapshotIsBaseAndWindowHashed
    statement: "A snapshot is a base, the latest leaf before T0 of every version of the N packages, and the leaves of the retained catalog commits from T0 to its upper bound, each projected as ADR 0010 says, with a manifest of N, the bounds, the base's and the window's counts and hashes, and the tool version, built by CI, published as a release asset and verified by hash"
  - key: BaseAtTheWindowsLowerBound
    statement: "The window's lower bound T0 is chosen to keep S1's target with the base loaded; the first deprecation and vulnerability data in the catalog are recorded from the catalog, and import --since replays history from any date, measured and documented, never in the step checks"
  - key: BaseThenFullReplayOverHttp
    statement: "S1 loads the base over HTTP as the importer, one Varve commit per package with a base-state assertion as of T0 in the catalog log, then replays every retained catalog commit of the window, one Varve commit each, with no bulk load"
---

The rulings of [ADR 0008](../adr/0008-the-dataset-and-its-mapping.md), filed by session 01 of #1 on
2026-10-23 and accepted by the maintainer on 2026-10-26, on the foundation's
pull request #2. Every ruling filed then carries `accepted-by` and `accepted-at`.

**Superseded on 2026-11-03** by `SnapshotIsBaseAndWindowHashed`, filed unaccepted by session 03 on #5 (ADR 0008's amendment of 2026-11-03): `SnapshotIsAHashedReleaseAsset`, "A snapshot is the subset leaves inside its window, their context documents and a manifest of N, the window, counts, hashes and tool version, built by CI and verified by hash", accepted by mailto:emil@okkels-klein.dk at 2026-10-26T11:49:23Z. Keys are never reused.

**Superseded on 2026-11-03** by `BaseAtTheWindowsLowerBound`, filed unaccepted by session 03 on #5 (ADR 0008's amendment of 2026-11-03): `WindowStartsWhereTheCatalogCarriesAdvisories`, "The window's lower bound is where the catalog starts carrying deprecation and vulnerability data, determined from the catalog in slice 1 and recorded by amendment, never taken from memory", accepted by mailto:emil@okkels-klein.dk at 2026-10-26T11:49:23Z. Keys are never reused.

**Superseded on 2026-11-03** by `BaseThenFullReplayOverHttp`, filed unaccepted by session 03 on #5 (ADR 0008's amendment of 2026-11-03): `FullReplayOverHttp`, "S1 replays every retained catalog commit over HTTP as the importer, one Varve commit each, with no base and no bulk load", accepted by mailto:emil@okkels-klein.dk at 2026-10-26T11:49:23Z. Keys are never reused.
