# 0009 — S4's subscription path

## Status

**Proposed — filed unaccepted by session 01 of #1, 2026-10-23, in review of
the foundation's pull request.** Acceptance is the maintainer's act on the pull
request. Its decision set is
[`s4s-subscription-path`](../decisions/s4s-subscription-path.md).

## Context

The brief's step 4 is "a pattern subscription receiving new commits as they
land". Varve has one as public HTTP API: `GET /datasets/{name}/commits` takes a
`pattern` parameter (three N-Triples terms, each a term or `?name`; Varve's
`docs/spec/change-feed.md` §4). Its shipped follower, `Varve.Protocol.Client`'s
`FollowAsync`, does not: `FollowOptions` has only `Graph`, and the request it
sends carries only `from` and `graph` (`src/Varve.Protocol.Client/FollowOptions.cs:26-27`,
`Follow.cs:152-158`).

S4's acceptance criterion 3 says the subscriber is killed, restarted, resumes
from its checkpoint, misses nothing, and shows its repeats. So the question
the review put was whether a **pattern-filtered** stream resumes after a
disconnect and a restart without missing a commit. It was answered from Varve's
source and tests at `v0.1.0-preview.11`, not from its documentation alone.

### What the source shows

1. **Resumption is by position, exclusive.** In the event-stream framing each
   commit is an event whose `id` is its position (`change-feed.md` §3).
   `Last-Event-ID` replaces `from` and `fromTime`
   (`src/Varve.Protocol/Endpoints/CommitsEndpoint.cs:129-131`;
   `src/Varve.Protocol/Endpoints/Ranges.cs:29-45`), and `from` is exclusive. A
   stream cut by the server ends with a `shutdown` or `error` event whose id is
   the last position delivered (`CommitsEndpoint.cs:333-350`, `EndAsync`).
2. **A filtered-out commit is never a position the client holds.** A data
   commit whose filtered changes are empty is skipped, and the next record
   carries its own position (`CommitsEndpoint.cs:284`: `deliver =
   !delta.IsEmpty`). A client that resumes after the last position it applied
   therefore re-reads every commit after it, and re-filters the ones it never
   saw. None is skipped by the resumption itself.
3. **The pattern is re-resolved on every request, and an unknown term is
   learned, not lost.** Each request parses the pattern and resolves its terms
   against a pin of the head (`CommitsEndpoint.cs:175-179`). A term the
   dataset does not know yet matches nothing until a commit allocates it, and
   from then on matches by the handle learned from that commit's allocations
   (`src/Varve.Protocol/Endpoints/FeedFilter.cs:15-21, 112-121, 134-150`).
   A live tail whose terms are all known is filtered by the store
   (`CommitsEndpoint.cs:226`; `SubscriptionFilter.ForPattern` and `Matches`,
   `src/Varve.Store/Log/SubscriptionFilter.cs:55-65`), a plain handle
   comparison; otherwise by `FeedFilter.Apply` on every commit.
4. **History and the present are one stream.** A request with no `to` is an
   open range: it delivers every closed commit after `from` up to the head,
   then each new one as it closes (`Ranges.cs:92`; `change-feed.md` §4). It is
   not paged. Only a **bounded** range is paged, by position
   (`Limits:CommitsPageSize`), and its `rel="next"` link repeats every
   parameter but `from`, `pattern` included (`CommitsEndpoint.cs:162-168,
   188-206`). A page may be empty of matches and still link to the next.

### What the tests show, and what they do not

- **Tested**: a pattern on a bounded range, with a term learned from the commit
  that allocates it (`tests/Varve.Protocol.Tests/BehaviourTests.cs:235-247`);
  an event-stream tail that resumes by `Last-Event-ID`, unfiltered
  (`BehaviourTests.cs:250-271`); paging that carries a `graph` filter across
  pages (`tests/Varve.Protocol.Tests/GovernanceTests.cs:180-204`); a store
  subscription that resumes after a position under a graph filter
  (`tests/Varve.Store.Tests/BehaviourTests.cs:285-298`).
- **Not tested** in the clone: a `pattern` combined with `Last-Event-ID` or
  `from` on an open tail; the store-side `SubscriptionFilter.ForPattern` path
  (no test names it); paging with a `pattern`. `change-feed.md` §6 states a
  resumption property "tested against these, by generated histories"; no
  generated-history test of the feed was found.

The source says the combination resumes without loss. The tests show each
part, not the combination.

## Decision

1. **S4 subscribes with `GET /commits?pattern=…`**, an open range, through a
   small client in this repository, **`VarveDemo.PatternFeed`**. It is named
   and documented as ours, not Varve's, and it uses only Varve's public HTTP
   API and the public `Varve.Delta` reader (`ChangeFeedReader`) at
   `VarveVersion`, which this ADR admits.
2. **Its delivery contract is `FollowAsync`'s**: at least once, resumable by
   position. It persists the last position it applied only after its handler
   returns, resumes after it with `from`, reconnects with back-off on a
   dropped connection or a `shutdown` event, and counts and shows every
   redelivered commit as a repeat.
3. **S4's acceptance criterion 3 stays as written**: stop, restart, resume,
   nothing missed, duplicates counted and shown. It also kills the server
   mid-tail. Its check therefore exercises in CI the combination Varve's own
   tests leave out: a pattern with resumption, through the store-filtered
   live path.
4. **"A pattern filter on `FollowAsync`" is a convenience issue** for Varve, not
   a gap S4 shows: the capability is public; only the follower lacks it. If
   Varve ships it, S4 moves to `FollowAsync` and this ADR is superseded.
5. If S4's check finds a commit missed, or delivered out of order, the finding
   is a Varve issue with S4's run as its reproduction, and S4 shows it. The
   client is not patched to hide it.

## Alternatives considered

- **`FollowAsync` by graph, showing the pattern as a gap** (the first draft of
  the specification). Rejected: the brief promises a pattern subscription,
  Varve has one as public API, and showing a gap where the capability exists
  would understate Varve.
- **`FollowAsync` unfiltered, with the pattern applied in the demo.**
  Rejected: the filter would be ours, and the viewer would be shown the demo's
  pattern matching presented as Varve's subscription.
- **Wait for `FollowOptions.Pattern`.** Rejected: the capability exists today,
  and waiting would hold back a step for a convenience.
- **The embedded store's `Dataset.Subscribe` with `SubscriptionFilter.ForPattern`.**
  Rejected: it runs in-process, bypassing the authenticated server every other
  step uses (C4), and it takes term handles, which are internal identifiers to
  a client.
- **The event-stream framing with `Last-Event-ID`.** Equivalent on the server
  (both resume by position). The delta framing with `from` is chosen because
  `Varve.Delta`'s reader parses it, so the client parses nothing itself.

## Consequences

- The demo carries a small piece of feed-following code, at-least-once
  resumption included, that Varve's client would otherwise provide. Its
  correctness is held by S4's check, run in CI on every push and every bump.
- `Varve.Delta` is a dependency at `VarveVersion`.
- S4's check becomes evidence about Varve beyond the demo: the only end-to-end
  test of a pattern-filtered feed resuming across a restart that either
  repository has.
- When `FollowAsync` gains a pattern, the client goes.
