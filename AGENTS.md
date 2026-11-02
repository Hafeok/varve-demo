# AGENTS.md — varve-demo

A runnable demonstration of [Varve](https://github.com/Hafeok/Varve), the
.NET-native, event-sourced RDF/SPARQL store. Its job is to convince a sceptical
developer, in about five minutes, that Varve is worth taking seriously: by
showing what their current store cannot do, and by handing them evidence they
can check without trusting us. Every design choice answers one objection:
"why trust a database that isn't battle-tested?"

**`docs/brief.md` is the authority.** Nothing summarises it accurately,
including this file. Read it before proposing anything structural. Where this
file, the specification or an ADR disagrees with the brief, the brief wins.

## Rules (from the brief)

1. **No faking.** Every number, chart and result shown comes from a real run in
   the session or from CI. No mocked Varve behaviour, no canned output presented
   as live.
2. **Nothing demo-specific goes into Varve.** When the demo needs something
   Varve lacks, the deliverable is a Varve issue with a reproduction, and the
   demo waits or shows the gap honestly, with the issue linked.
3. **Varve's public API only.** Reaching into internals means Varve needs a
   feature, not that we need a workaround.
4. **Every step has an automated check in CI** that runs it end to end, so the
   demo cannot rot silently between Varve releases.
5. **A clean machine with Docker reaches step 1 in one command.** Each step's
   target time is in the README and checked in CI where measurable.
6. **Containers are published to GHCR only.** Test and compose images come only
   from `ghcr.io` and `public.ecr.aws`.
7. **Varve's conventions where they apply**: current LTS .NET, a Native
   AOT-compatible host, MPL-2.0, `DecisionDriven.Analyzers`, ADRs for decisions
   and new dependencies.
8. **Evidence first.** Lead with evidence, never with how Varve was built.

## How we work

**Specification, then ADR, then code.** The demo script and its acceptance
criteria (`docs/spec/script.md`) are the specification. A change to what a
viewer sees starts there; a decision it needs is an ADR; only then code.

- **Small vertical slices**, each ending in a step that runs end to end and
  whose check passes in CI. Never scaffolding that compiles and does nothing.
  The order is in the README: slice 1 is S1, S2, S6 and the ADR 0005
  measurement; slice 2 is S3 and S4; slice 3 is S7 and E; slice 4 is S5.
- **A step's `State` changes in the same commit as its evidence.** `passing`
  only when a test named after its check exists under `tests/` and CI runs it;
  `withdrawn` only with the numbers or the Varve issue that justify it.
- **Check every proposal against the ADRs.** If it contradicts one, say so and
  propose a superseding ADR. Never diverge quietly. When one artefact changes,
  list what else must: spec, README table, ADR, decision set, code, test.
- **Disagree, with reasons.** That includes disagreeing with the brief, the
  spec, an ADR or the prompt you were given. Say it in the report, and do not
  quietly do something else.
- **Never take a version from memory.** Resolve it from nuget.org (or the
  registry, for an image) when it is added or bumped, and write it into
  `Directory.Packages.props` with `Adr="NNNN"`. No `.csproj` carries a version.
- **Every dependency needs an ADR first**: a package, an image, a service, a
  GitHub Action (ADR 0001).
- **Facts about Varve come from Varve**: its released packages, its documented
  public surface, and its repository read-only. Cite the Varve ADR or spec
  section; when you cannot, say so rather than guess.
- **Code is complete, compiling and tested.** A stub is marked as a stub in the
  code and listed in the session's report.

## Decisions: the `DD` rules

`DecisionDriven.Analyzers` runs at build time on every project. Every ADR is
also a decision set in `docs/decisions/` (namespace `varvedemo`, format in
`docs/decisions/README.md`), and code cites a ruling as a type.

- **A finding is fixed, or answered by a decision.** Take the design change when
  the only reason for the code is that it already looked like this. Otherwise
  file a decision and cite it with `[DesignDecision]` on the narrowest symbol.
- **Never suppress a `DD` finding**: no `#pragma`, no `[SuppressMessage]`, no
  `.editorconfig` downgrade, no `NoWarn`.
- **A new decision is filed unaccepted.** Citing it is `CS0618` until the
  maintainer adds `accepted-by`. **A session never writes `accepted-by`.**
- **An accepted ADR is never edited.** It changes by a dated amendment or a
  supersession (ADR 0001).
- A finding about the analyzers themselves is an issue in their repository,
  never a local workaround.

## Every commit

Conventional commits (`feat:`, `fix:`, `docs:`, `build:`, `test:`, `ci:`,
`chore:`), one logical change each.

- **`Refs #N`** in the body. A closing keyword with a number (`Closes #N`) stands
  only as a trailer line of its own, on the commit that finishes the issue;
  in prose write a bare `#N`. No issue covers the work? Open one first.
- **A commit message never carries a model's identifier.** A tool's co-author
  trailer naming the product is not one.

```text
docs(adr): add ADR 0004, the time-travel clock

Catalog time rides in the named graph <urn:varve-demo:catalog-log>,
in the same commit as the data, and resolves through a disposable
time index folded from the change feed.

Refs #1
```

**Every AI-assisted session files a record** in `docs/traceability/`, named
`YYYY-MM-DD-issue-N-<slug>.md`: the prompt verbatim, the tool and model named
exactly, the report, and the issue. Prose everywhere else (README,
documentation) says "developed with AI assistance under human review" and
names no product.

## Commands

```bash
dotnet build VarveDemo.slnx -c Release   # zero warnings, or it is broken
dotnet test --solution VarveDemo.slnx -c Release   # the gates are tests
```

## Never

- A fork, a vendored copy, a patch or a build of Varve. Varve comes from
  nuget.org and `ghcr.io/hafeok/varve`, as released (ADR 0002).
- Varve's internals, reflection into Varve, or anything under a dataset's
  `derived/` directory.
- `--anonymous`, or any other authentication bypass (ADR 0007).
- Mocked Varve behaviour, or canned output presented as live.
- An image from anywhere but `ghcr.io` or `public.ecr.aws`, or one not pinned
  by digest.
- A `Varve.` prefix on our own names. Ours start with `VarveDemo`.
- A product name in prose. It says "developed with AI assistance under human
  review".
- A version taken from memory, a `.csproj` that pins its own version, or a
  dependency without an ADR.
- A suppressed `DD` finding, an `accepted-by` written by a session, or an edited
  accepted ADR.
- A `.cs` file without the MPL-2.0 Exhibit A notice.
- A gap in Varve worked around instead of shown, with its Varve issue linked.
- A placeholder body not marked as a stub and reported.
