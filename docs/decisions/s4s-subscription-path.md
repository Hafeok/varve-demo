---
set: s4s-subscription-path
namespace: varvedemo
adr: 0009
decisions:
  - key: SubscribeByCommitsPattern
    statement: "S4 subscribes with GET /commits?pattern= as an open range, through VarveDemo.PatternFeed, a small client of ours using only Varve's public HTTP API and the Varve.Delta reader"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: PatternFeedIsAtLeastOnceByPosition
    statement: "VarveDemo.PatternFeed persists the last position applied after its handler returns, resumes after it with from, reconnects on a dropped connection or a shutdown event, and counts and shows every repeat"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ResumeCheckKillsSubscriberAndServer
    statement: "S4's check kills the subscriber and the server mid-tail and requires that nothing is missed and every repeat is shown, exercising pattern with resumption end to end in CI"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: FollowAsyncPatternIsAConvenience
    statement: "A pattern filter on FollowAsync is a convenience issue for Varve, and S4 moves to FollowAsync if Varve ships it"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: MissedCommitIsAVarveIssue
    statement: "A commit S4's check finds missed or out of order is a Varve issue with the run as its reproduction, never patched over in the client"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
---

The rulings of [ADR 0009](../adr/0009-s4s-subscription-path.md), filed by session 01 of #1 on
2026-10-23 and accepted by the maintainer on 2026-10-26, on the foundation's
pull request #2. Every ruling carries `accepted-by` and `accepted-at`.
