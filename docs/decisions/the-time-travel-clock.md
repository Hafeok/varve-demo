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
    statement: "Every as-of answer shows the checkpoint the read started from and how many commits Varve replayed after it, and CI holds that derivation to the actual distance in Varve's 422"
  - key: CheckpointAtLeastEveryMaxAsOfDistance
    statement: "MaxAsOfDistance stays at Varve's default, and the dataset is checkpointed at least every MaxAsOfDistance commits during import"
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
