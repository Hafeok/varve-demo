---
set: the-browser-step-is-gated-by-a-measurement
namespace: varvedemo
adr: 0005
decisions:
  - key: MeasureBeforeBuilding
    statement: "Before S5 is built a harness measures the WASM payload Brotli and raw, cold and warm start to the first S2 answer, and an offline reload"
  - key: RunnerAndStatedLaptop
    statement: "The measurement runs on a GitHub runner with headless Chromium and on a stated laptop"
  - key: ThresholdsFixedFirst
    statement: "The thresholds are fixed before measuring at 15 MB over the wire, 5 s cold and 1.5 s warm"
  - key: WithdrawOnFailure
    statement: "If any threshold fails S5 is withdrawn with the numbers and a Varve issue"
  - key: RemeasureOnEveryBump
    statement: "The measurement reruns on every Varve bump and its numbers are on the evidence page"
  - key: MeasureFirstBuildLast
    statement: "The measurement is taken in slice 1 and S5 is built last"
---

The rulings of [ADR 0005](../adr/0005-the-browser-step-is-gated-by-a-measurement.md), filed unaccepted by session 01 of
#1 on 2026-10-23. Every citation is `CS0618` until the maintainer accepts them by
adding `accepted-by` and `accepted-at`, which a session never writes.
