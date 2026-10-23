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
  - key: AsOfDistanceAboveSnapshot
    statement: "The server sets Limits MaxAsOfDistance above the snapshot commit count so the slider reaches the start of history"
  - key: DomainClockIsAVarveIssue
    statement: "As-of by a declared domain clock is filed as a Varve issue, and the index goes if Varve ships it"
---

The rulings of [ADR 0004](../adr/0004-the-time-travel-clock.md), filed unaccepted by session 01 of
#1 on 2026-10-23. Every citation is `CS0618` until the maintainer accepts them by
adding `accepted-by` and `accepted-at`, which a session never writes.
