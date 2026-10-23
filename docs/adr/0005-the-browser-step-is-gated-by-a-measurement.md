# 0005 — The browser step is gated by a measurement

## Status

**Proposed — filed unaccepted by session 01 of #1, 2026-10-23.** Acceptance is
the maintainer's act on the pull request. Its decision set is
[`the-browser-step-is-gated-by-a-measurement`](../decisions/the-browser-step-is-gated-by-a-measurement.md).

## Context

The brief: "Browser engine size and startup: measure the WASM payload and
cold-start time before step 5 is promised. Record the numbers and a threshold
below which step 5 stays in the script."

S5 is the step most likely to impress and most likely to disappoint. A .NET
WebAssembly application carries the runtime; a slow first load in front of a
sceptical audience is evidence *against* the claim. Varve publishes
`Varve.Store.Browser` on nuget.org (every release since `0.1.0-preview.1`),
which stores a dataset in OPFS through synchronous access handles in a worker,
and is tested by Varve in headless Chromium. Varve has not published payload
or start-up numbers for a third-party application.

Thresholds chosen after the measurement would be fitted to it. They are fixed
here, first.

## Decision

1. **Before S5 is built, a harness measures**, for a minimal application that
   opens a copy of the snapshot's log with `Varve.Store.Browser` and answers
   S2's question:
   - the payload over the wire, Brotli-compressed, and raw;
   - **cold start**: empty cache and empty OPFS, from navigation to S2's first
     answer;
   - **warm start**: cached payload and populated OPFS, from navigation to the
     first answer;
   - an **offline reload**: the network disabled, the page reloaded, an answer.
2. It runs on a GitHub-hosted runner with headless Chromium, and on one stated
   laptop (model, CPU, memory, browser version). Both sets are recorded.
3. **Thresholds, fixed before measuring**: ≤ 15 MB over the wire (Brotli),
   ≤ 5 s cold, ≤ 1.5 s warm, and the offline reload answers. These are
   proposals the maintainer may change at acceptance, and not after the
   numbers are in.
4. **If any threshold fails, S5 is `withdrawn`** in the specification and the
   README, with the numbers, and a Varve issue is filed with the measurement as
   its reproduction.
5. **The measurement reruns on every Varve bump**, and its numbers are on the
   evidence page. A withdrawn S5 comes back when a bump passes.
6. **It is measured in slice 1**, so the decision is known early; **S5 is built
   last**, in slice 4.

## Alternatives considered

- **Build S5 and see.** Rejected by the brief, and because the sunk cost would
  bend the thresholds.
- **No thresholds, just publish the numbers.** Rejected: the brief asks for a
  threshold, and without one the step's presence is a judgement made each time
  under pressure.
- **Thresholds set after a first measurement.** Rejected: they would describe
  what Varve does, not what a viewer accepts.
- **Measure on the laptop only.** Rejected: CI must rerun it on every bump,
  and a runner is what CI has. The laptop number is what a viewer at a talk
  sees, so both are kept.
- **A smaller engine (an interpreter-only build, trimming to the bone) built
  here.** Rejected: that is a demo-specific Varve build, which ADR 0002
  forbids. If size is the problem, it is Varve's issue.

## Consequences

- The ADR 0005 harness is slice 1 work although S5 is slice 4 work.
- S5 can disappear from the script; the README and the evidence page say so
  plainly with the numbers.
- A bump can bring S5 back or take it out; the bump's pull request says which.
