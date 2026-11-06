---
set: the-time-travel-clock
namespace: varvedemo
adr: 0004
decisions:
  - key: CatalogTimeInGraphSameCommit
    statement: "Each importer commit asserts its catalog commitId and commitTimeStamp in the graph urn:varve-demo:catalog-log in the same commit as the data"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: SliderIsCatalogTime
    statement: "The slider uses catalog time, never published, and every as-of answer shows the catalog time asked, the resolved position and its Varve timestamp"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ResolveByDisposableTimeIndex
    statement: "Catalog time resolves through a disposable index of catalog time and position folded from the change feed with FollowAsync, read with Varve-As-Of position"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: IndexCrossCheckedByBinarySearch
    statement: "CI cross-checks the time index against binary search over as-of reads of the catalog-log graph"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: AsOfAnswerShowsItsCheckpoint
    statement: "S3 states that an as-of read anywhere in history is served from checkpoints with validity intervals, and every as-of answer shows the checkpoint it started from and the commits replayed after it"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: CheckpointDerivationHeldToVarve
    statement: "With a CI-only server at MaxAsOfDistance 0, CI checks a 422's actual against the demo's computed distance before the final checkpoint, and that an early as-of read succeeds at distance 0 after it"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: FinalCheckpointThenEveryKCommits
    statement: "MaxAsOfDistance stays at Varve's default; one checkpoint ends the import, then one whenever more than K commits have landed since the last, with no checkpoints during the import"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: KIsConfiguredBelowTheBound
    statement: "K is VarveDemo:Checkpoints:EveryCommits, proposed at 5000, and the importer refuses to start unless it is below the maxAsOfDistance the server advertises"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ServerCheckpointPolicyIsAVarveIssue
    statement: "A checkpoint policy configurable on the server stays a Varve issue, and when Varve ships it the importer stops calling the checkpoint endpoint and the server is configured instead"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: DomainClockIsAVarveIssue
    statement: "As-of by a declared domain clock is filed as a Varve issue, and the index goes if Varve ships it"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ImporterTakesCheckpointsAsDatasetAdmin
    statement: "The importer takes the checkpoints with POST /manage/datasets/name/checkpoints through varve-demo-checkpointer, which holds admin on the dataset alone, and S3's screen says the importer takes them and why"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-11-06T13:17:23Z
---

The rulings of [ADR 0004](../adr/0004-the-time-travel-clock.md), filed by session 01 of #1 on
2026-10-23 and accepted by the maintainer on 2026-10-26, on the foundation's
pull request #2. Every ruling filed then carries `accepted-by` and `accepted-at`.

**Superseded on 2026-11-03** by `ImporterTakesCheckpointsAsDatasetAdmin`, filed unaccepted by session 03 on #5 (ADR 0004's and ADR 0007's amendments of 2026-11-03): `ImporterTakesCheckpointsAsOperator`, "The importer takes the checkpoints with POST /manage/datasets/name/checkpoints through the operator client, Varve's documented operator procedure, and S3's screen says the importer takes them and why", accepted by mailto:emil@okkels-klein.dk at 2026-10-26T11:49:23Z. Keys are never reused.
