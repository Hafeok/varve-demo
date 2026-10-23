---
set: the-time-travel-clock
namespace: varvedemo
adr: 0004
decisions:
  - key: CatalogTimeInGraphSameCommit
    statement: "Each importer commit asserts its catalog commitId and commitTimeStamp in the graph urn:varve-demo:catalog-log in the same commit as the data"
  - key: SliderIsCatalogTime
    statement: "The slider uses catalog time, never published, and every as-of answer shows the catalog time asked, the resolved position and its Varve timestamp"
  - key: ResolveByDisposableTimeIndex
    statement: "Catalog time resolves through a disposable index of catalog time and position folded from the change feed with FollowAsync, read with Varve-As-Of position"
  - key: IndexCrossCheckedByBinarySearch
    statement: "CI cross-checks the time index against binary search over as-of reads of the catalog-log graph"
  - key: AsOfAnswerShowsItsCheckpoint
    statement: "S3 states that an as-of read anywhere in history is served from checkpoints with validity intervals, and every as-of answer shows the checkpoint it started from and the commits replayed after it"
  - key: CheckpointDerivationHeldToVarve
    statement: "With a CI-only server at MaxAsOfDistance 0, CI checks a 422's actual against the demo's computed distance before the final checkpoint, and that an early as-of read succeeds at distance 0 after it"
  - key: FinalCheckpointThenEveryKCommits
    statement: "MaxAsOfDistance stays at Varve's default; one checkpoint ends the import, then one whenever more than K commits have landed since the last, with no checkpoints during the import"
  - key: KIsConfiguredBelowTheBound
    statement: "K is VarveDemo:Checkpoints:EveryCommits, proposed at 5000, and the importer refuses to start unless it is below the maxAsOfDistance the server advertises"
  - key: ImporterTakesCheckpointsAsOperator
    statement: "The importer takes the checkpoints with POST /manage/datasets/name/checkpoints through the operator client, Varve's documented operator procedure, and S3's screen says the importer takes them and why"
  - key: ServerCheckpointPolicyIsAVarveIssue
    statement: "A checkpoint policy configurable on the server stays a Varve issue, and when Varve ships it the importer stops calling the checkpoint endpoint and the server is configured instead"
  - key: DomainClockIsAVarveIssue
    statement: "As-of by a declared domain clock is filed as a Varve issue, and the index goes if Varve ships it"
---

The rulings of [ADR 0004](../adr/0004-the-time-travel-clock.md), filed unaccepted by session 01 of
#1 on 2026-10-23. Every citation is `CS0618` until the maintainer accepts them by
adding `accepted-by` and `accepted-at`, which a session never writes.
