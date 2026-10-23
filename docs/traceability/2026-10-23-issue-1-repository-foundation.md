# Session 01 — repository foundation (#1)

- **Issue:** [#1, Repository foundation](https://github.com/Hafeok/varve-demo/issues/1)
- **Date:** 2026-10-23
- **Tool:** Claude Code 2.1.309, in a cloud session started from the desktop app
- **Model:** `claude-opus-5-5`, as the session's metadata reports it
  (`session_context.model` and `last_served_model`)
- **Branch:** `claude/festive-keller-huyqzn`
- **Commits:** every commit on the branch from `chore: start the repository`
  to the one that closes #1.

## The prompt

Verbatim, as the session received it.

`````markdown
# Session 01 — Repository foundation

Hand-off prompt for a Claude Code session on `Hafeok/varve-demo`. The
repository exists on GitHub and is empty. Paste everything below the line.

Before handing off, fill in the one gap marked `[[EMIL: …]]` in Appendix A.
The brief's last rule is cut off at "stubs are marked and".

---

You are setting up `varve-demo`, a runnable demonstration of Varve. Varve is a
.NET-native, event-sourced RDF/SPARQL store
(https://github.com/Hafeok/Varve). This session builds the foundation:
- the brief and the working method, so later sessions can read them;
- the specification;
- the first eight ADRs;
- the build, and gates that run as tests.

It builds no demo step. Later sessions will start with nothing but this
repository, so anything they need to know has to be written into it.

## Read first

1. Appendix A is the project brief. Write it verbatim into `docs/brief.md`.
   It is the authority: where anything else disagrees with it, the brief wins.
2. Clone Varve read-only beside this repository
   (`git clone --depth 1 https://github.com/Hafeok/Varve ../varve`). It is your
   reference for its conventions and for facts about its public surface. Read
   its `AGENTS.md`, `README.md` ("Status" and "Quick start"), `docs/decisions/README.md`,
   and `docs/adr/` for any ADR cited below. Never copy code from it, never
   reference it as a project, and never change it.

## Deliverables

### 1. `AGENTS.md` and `CLAUDE.md`

`AGENTS.md` is the vendor-neutral working method for every later session.
Follow Varve's `AGENTS.md` in shape, with only what applies here:
- what the demo is for;
- that `docs/brief.md` is the authority;
- the rules from the brief;
- spec, then ADR, then code;
- small vertical slices that end in a step running end to end;
- disagree, with reasons;
- never take a version from memory, resolve it from nuget.org;
- the DD rules: a finding is fixed or answered by a decision, never
  suppressed, and a session never writes `accepted-by`;
- how a commit looks (below);
- the never-list.

`CLAUDE.md` is one line pointing at `AGENTS.md`.

The never-list must include these:
- no fork, vendored copy, patch or build of Varve;
- no use of Varve's internals, reflection or `derived/` files;
- no `--anonymous`;
- no mocked Varve behaviour or canned output presented as live;
- no image from outside `ghcr.io` or `public.ecr.aws`;
- no `Varve.` prefix on our own names;
- no product name in prose, which says "developed with AI assistance under
  human review".

### 2. The specification, `docs/spec/script.md`

The seven steps from the brief plus the evidence page, E. For each step give
what the viewer sees, numbered acceptance criteria that CI can check
automatically, a target time, and the name of its check.

Put a machine-readable table between `<!-- spec-table:begin -->` and
`<!-- spec-table:end -->`, with the columns `| Step | Title | Target | Check | State |`.
`State` is `pending`, `passing` or `withdrawn`. Every step starts `pending`.
The README carries an identical copy of the table.

Proposed targets: these are proposals, so say so in the spec.

| Step | Target |
|---|---|
| S1 | ≤ 3 min from `docker compose up` to the first answer, image pulls excluded |
| S2 | ≤ 2 s per query |
| S3 | ≤ 2 s per as-of query or diff |
| S4 | ≤ 5 s from a commit landing to the subscriber printing it |
| S5 | set by ADR 0005's measurement |
| S6 | ≤ 30 s per crash cycle, at least 50 cycles per CI run |
| S7 | ≤ 1 min from `git clone` to an answer |

The spec has these common rules:
- real runs only;
- released Varve only;
- public API only;
- always authenticated;
- reproducible from a pinned snapshot, with S4's live tail as the one
  exception. Offline, S4 replays the snapshot's tail and labels it on screen
  as a replay.
- a gap is shown, with its Varve issue linked, never worked around.

Facts the acceptance criteria depend on, verified in Varve at
`v0.1.0-preview.11`. Re-check them against the clone.
- Over HTTP, a write is one commit. Every response carries `Varve-Position`.
  As-of reads use `Varve-As-Of: position:n | time:<RFC 3339>`. The time form
  resolves to the greatest position whose *Varve* timestamp is at or before
  it.
- The sequencer assigns commit timestamps and never accepts one. Over HTTP,
  the commit cause is the request id, set by the server.
- `FollowAsync` delivers at least once and resumes from a checkpoint. S4
  shows the repeats; it does not hide them.
- A store refuses to open a log whose hash chain does not verify. No public
  "verify the chain" command was found. List it in the spec as a Varve issue
  to file for S6, so the viewer can see the check.
- S2 asks: "Which of my dependencies, from a `packages.lock.json`, resolve to
  a deprecated or vulnerable version, and what should I move to?" Packages
  outside the demo's subset are listed as such, never dropped. CI checks the
  answer against an independent reader of the snapshot's JSON. That reader
  uses `System.Text.Json` and no Varve code.

### 3. ADRs 0001–0008 and their decision sets

Each ADR goes in `docs/adr/NNNN-slug.md`, with the sections Status, Context,
Decision, Alternatives considered and Consequences. Add an index at
`docs/adr/README.md`.

Each ADR also gets a decision set at `docs/decisions/<slug>.md`, in the form
`DecisionDriven.Analyzers` reads. Use namespace `varvedemo` and copy the
format from Varve's `docs/decisions/README.md`. Every ruling is filed
unaccepted.

Below are the decisions already reached in planning. Write them up, and argue
the alternatives properly. If something in the Varve clone contradicts a
point, say so in the ADR and in your final report. Do not silently change the
decision.

- **0001 Record architecture decisions.**
  - Use the form above.
  - Every dependency needs an ADR before it enters the repository: a package,
    an image or a service.
  - A decision changes by a dated supersession or amendment, never by editing
    an accepted ADR.
- **0002 Consume Varve only as released.**
  - Packages come from nuget.org only. The brief's "decision-driven-design
    feed" means the packages Varve publishes there; its authors field is
    `decision-driven-design`. Use `nuget.config` with `<clear/>` and source
    mapping.
  - The server is `ghcr.io/hafeok/varve`, pinned by tag and digest. CI checks
    it with `gh attestation verify` before use.
  - One pin, a `VarveVersion` property in `Directory.Packages.props`, set to
    the latest release, which you resolve. The compose image tag must equal
    `v$(VarveVersion)`.
  - A bump to a new Varve release is one pull request: it reruns every check
    and regenerates the evidence page.
  - A gap in Varve becomes a Varve issue with a reproduction.
- **0003 Conventions taken from Varve.**
  - The SDK is pinned as Varve pins it in `global.json`, with
    `rollForward: latestFeature`, targeting `net10.0`.
  - Everything that ships is AOT-compatible, with the trim, AOT and
    single-file analyzers on, and IL diagnostics as errors.
  - MPL-2.0: the `LICENSE` text is copied from Varve's, `NOTICE` names Emil
    Okkels Klein, and the Exhibit A header is on every `.cs` file.
  - Build settings: warnings are errors, `latest-recommended`,
    `EnforceCodeStyleInBuild`, nullable on, implicit usings off,
    deterministic builds.
  - Central package management, with `Adr="NNNN"` on every `PackageVersion`.
  - `DecisionDriven.Analyzers` is build-time only, with `ArchFamily`
    `VarveDemo` and the ledger in `docs/decisions`.
  - Names start with `VarveDemo`, never `Varve.`.
  - We publish images to GHCR only. We pull images only from `ghcr.io` or
    `public.ecr.aws`, pinned by digest.
  - Tests use xunit.v3 on Microsoft Testing Platform, with TRX reports. The
    repository's gates are tests.
  - Issue-first commits with `Refs #N`, plus a traceability record per
    session in `docs/traceability/`, as Varve does. Not adopted yet, with
    reasons: DCO, the agent-review gate and release descriptors. Revisit them
    when the demo has releases.
- **0004 The time-travel clock.**
  - Catalog time cannot ride in commit metadata (see the facts above). So
    each importer commit also asserts its catalog `commitId` and
    `commitTimeStamp` in the named graph `<urn:varve-demo:catalog-log>`, in
    the same commit as the data.
  - The slider uses the catalog clock, never the package's `published`, which
    is 1900-01-01 for unlisted versions. Every as-of answer shows the catalog
    time asked for, the resolved Varve position, and that position's Varve
    timestamp.
  - Resolution: a disposable time index of (catalog time, position) pairs,
    folded from the change feed through `FollowAsync`. It uses only the
    public API and is rebuildable. CI cross-checks it by binary search with
    as-of reads.
  - File the Varve issue "as-of by a declared domain clock". If Varve ships
    it, the index goes.
  - Rejected alternatives: real-time import; caller-supplied commit
    timestamps; position arithmetic, which breaks when any other commit lands;
    binary search alone.
- **0005 The browser step is gated by a measurement.**
  - Before S5 is built, a harness measures the WASM payload (Brotli and raw),
    the cold and warm start to the first S2 answer, and an offline reload. It
    runs on a GitHub runner with headless Chromium and on a stated laptop.
  - Thresholds, fixed before measuring: ≤ 15 MB over the wire, ≤ 5 s cold,
    ≤ 1.5 s warm. These are proposals Emil may change at acceptance.
  - If any threshold fails, S5 is `withdrawn` with the numbers, plus a Varve
    issue.
  - The measurement reruns on every bump.
  - It is measured in slice 1; S5 is built last.
- **0006 JSON-LD ingestion through `Varve.JsonLd`.**
  - The brief's fallback is not needed: the reader shipped in
    `v0.1.0-preview.4`.
  - The catalog's remote contexts are recorded in the snapshot and supplied
    through `JsonLdOptions.DocumentLoader`, which refuses every remote context
    by default.
  - Processing happens in the importer. The result goes to the server as one
    SPARQL Update per catalog commit, so the server never fetches a context.
  - A rejected or misread leaf fails the import and becomes a Varve issue. It
    is never patched or skipped.
- **0007 Local authentication with a mock OIDC issuer.**
  - Never use `--anonymous`.
  - The issuer is `ghcr.io/navikt/mock-oauth2-server`, from upstream, at the
    version and digest pinned in Varve's `.github/ci-images.yaml`. Varve's CI
    uses a private mirror; we use upstream.
  - Commit `deploy/mock-oauth2.json` with one client per role (importer, which
    writes; viewer, which reads; operator), using client credentials.
  - The README says plainly that the issuer is a mock, and shows how to point
    the demo at a real issuer.
  - Rejected: Zitadel by default (a heavy startup, though it could be an
    optional profile later), and Keycloak (`quay.io`).
- **0008 The dataset and its mapping.**
  - The top N package ids by downloads at a pinned date, with N = 3 000 as a
    proposal. The list, the date and the query are committed.
  - A snapshot is the leaves for those ids between two catalog times, plus
    the context documents and a manifest of counts, bounds, hashes and tool
    version. CI builds it, it is published as a release asset, and it is
    verified by hash.
  - Each catalog commit that touches the subset becomes exactly one Varve
    commit, in catalog order.
  - One named graph per package version,
    `<urn:varve-demo:pkg/{id}/{version}>`. A details leaf replaces the graph
    and a delete drops it, so the S3 diff shows effective changes.
  - Open, decided by slice 1's first measurement: replay everything, or
    bulk-load a base and then replay. Varve's bulk load is one logical
    commit, so it would flatten the history before the base.

### 4. Build and gates

Create these files:
- `global.json`, `nuget.config`, `Directory.Build.props`,
  `Directory.Build.targets` and `Directory.Packages.props`, resolving every
  version from nuget.org;
- `.editorconfig`, adapted from Varve's, with Varve-only rules removed;
- `.gitattributes`, `.gitignore`, `LICENSE`, `NOTICE`, and `VarveDemo.slnx`.

Create `tests/VarveDemo.Spec.Tests` with these gates. Each one must fail on a
planted violation; verify that, then revert the plant.
- The README's step table equals the spec's table.
- Steps are unique, and each check is a unique identifier.
- Every step has a target and a known state.
- A `passing` step has a test method with its check's name somewhere under
  `tests/`.
- Every `PackageVersion` cites an existing ADR, and no `.csproj` pins its own
  version.
- ADR numbers run without gaps. Every ADR that is not superseded has a set
  whose `adr:` matches it. Every set is named after its file. The index links
  every ADR.
- Every `.cs` file starts with the Exhibit A notice, and `LICENSE` is the
  MPL-2.0 text.
- No `--anonymous` in configuration files (yaml, json, shell, props,
  targets, Dockerfiles).
- Every `image:` or `FROM` comes from `ghcr.io/` or `public.ecr.aws/` and is
  pinned by `@sha256:`.
- The compose image tag matches the Varve pin. Skip this with an explicit
  reason until `compose.yaml` exists.

Add `.github/workflows/ci.yml` to restore, build in Release, run the tests
with TRX output, and upload the results. Pin the actions by SHA, at the SHAs
Varve's workflows pin.

### 5. README

The README has these sections:
- what the demo is, and the evidence-first argument;
- **Status: nothing runs yet**, stated plainly;
- the step table, copied from the spec;
- the rules;
- the build order: slice 1 is S1, S2, S6 and the ADR 0005 measurement;
  slice 2 is S3 and S4; slice 3 is S7 and E; slice 4 is S5;
- links to the ADRs;
- how to build;
- the licence, and the line "developed with AI assistance under human
  review".

Lead with evidence. Leave out how Varve was built.

## How to land it

1. Open issue #1, "Repository foundation", with this deliverable list.
2. Work on a branch. Each commit is one logical change, uses the conventional
   form, and carries `Refs #1`. The last commit closes it with a
   `Closes #1` trailer line.
3. Before opening the pull request, check that:
   - `dotnet build` and `dotnet test` are clean on a fresh clone;
   - every gate has been seen to fail on a plant;
   - every claim about Varve in the spec and ADRs has been checked against
     the clone.
4. File the traceability record at
   `docs/traceability/2026-MM-DD-issue-1-repository-foundation.md`. It holds:
   this prompt, verbatim; the tool and model, named exactly; your report; and
   the issue number.
5. Open a pull request to `main`. Do not merge it. Emil reviews and accepts
   the ADRs.

## Report back

End with a short report:
- What is in the pull request.
- Every point where Varve's clone contradicted this prompt, and what you did
  about it.
- The Varve issues to file, each with a one-paragraph reproduction draft:
  as-of by a domain clock, and chain verification on demand, at least.
- Anything in this prompt you disagree with, and why.

## Appendix A — the brief

````markdown
# Project prompt: Varve demo

## What this project is

A runnable demonstration of Varve, the .NET-native, event-sourced RDF/SPARQL store (github.com/Hafeok/Varve). Its job is to convince sceptical developers in about five minutes that Varve is worth taking seriously. It does that by showing capabilities their current store cannot, and evidence they can check without trusting us.

The demo lives in its own repository (`varve-demo`). It consumes Varve only as published packages and container images from the decision-driven-design feed. It never forks, vendors or patches Varve.

## Who you are working with

Emil: founder and architect of Varve, deep RDF/SPARQL/SHACL/Oxigraph and event-sourcing/.NET/Azure background. Skip introductions. When you disagree, say so and give the reason.

## The audience and the objection

The audience is .NET and data developers who think "you can't just build a new database; why trust something that isn't battle-tested?" Every design choice in this repo answers that objection. The answer has two halves:

1. Capability: things Oxigraph, GraphDB, dotNetRDF or Marten cannot show out of the box, such as time travel, diffs between positions, live change feeds, the same engine in the browser, and the database as files in Git.
2. Evidence: the risky part of a database is the storage engine. In Varve the only durable truth is an append-only log with a frozen format and a hash chain, and every index is a rebuildable projection. The demo proves this live, with crash tests, conformance numbers and benchmarks a viewer can rerun.

Lead with evidence, not with how Varve was built. The AI-assisted development story is told after people are convinced, never as the argument.

## The demo script (the product of this repo)

Dataset: a subset of the NuGet catalog (the top few thousand packages), which is itself an append-only log of package publishes, edits and deletes. One catalog commit maps to one Varve commit.

1. Zero to query: `docker compose up` or a `dotnet tool`; bulk load with the load rate shown.
2. A question developers care about, e.g. deprecated or vulnerable versions among my dependencies.
3. Time travel: the same query as of an earlier point, plus a diff between two positions.
4. Live change feed: a pattern subscription receiving new commits as they land.
5. The same log in the browser: the identical engine in WASM over OPFS, same query, same answer, offline.
6. Crash: `kill -9` during a write loop, restart, the hash chain verifies, and every acknowledged commit is present. Repeatable on demand.
7. `git log` on the dataset: the database is files you can clone.

Plus an evidence page generated by CI from real runs: W3C conformance counts, differential results against Oxigraph, failure-injection results, BenchmarkDotNet numbers with stated hardware and dataset, and a "not yet proven" list.

## Open questions to settle first, by ADR

- Which clock time travel uses. Imported history has two clocks: Varve's commit timestamp (import time) and the catalog's commit timestamp (publish time). The slider must not lie. Decide how catalog time is carried (commit metadata or in-graph) and how as-of-by-that-time resolves. Anything this needs from Varve is a Varve issue, not demo code.
- Browser engine size and startup: measure the WASM payload and cold-start time before step 5 is promised. Record the numbers and a threshold below which step 5 stays in the script.
- JSON-LD ingestion: if Varve's JSON-LD reader (milestone 6b) is not released, the importer converts the catalog to N-Quads and says so in the README.
- Authentication: Varve.Server is OIDC-only with no API keys. The local compose setup uses a mock OIDC issuer, never a bypass.

## Rules

- No faking. Every number, chart and result shown comes from a real run in the session or from CI. No mocked Varve behaviour, no canned outputs presented as live.
- Nothing demo-specific goes into Varve. When the demo needs something Varve lacks, the deliverable is a Varve issue with a reproduction, and the demo either waits or shows the gap honestly.
- The demo uses only Varve's public API. Reaching into internals is a sign that Varve needs a feature, not a workaround.
- Every script step has an automated check in CI that runs the step end to end, so the demo cannot silently rot between Varve releases.
- A clean machine with Docker reaches step 1 in one command. Each step's target time goes in the README and is checked in CI where measurable.
- Containers are published to GHCR only. Test and compose images pull only from ghcr.io and public.ecr.aws.
- Same conventions as Varve where they apply: current LTS .NET, Native AOT-compatible host, MPL-2.0, DecisionDriven.Analyzers, ADRs for decisions and new dependencies, and README prose that says only "developed with AI assistance under human review" without naming a product.

## How we work

- Specification first: the demo script and its acceptance checks are the spec. Then ADRs, then code.
- Small vertical slices that each end in a step that runs end to end, starting with steps 1, 2 and 6 (load, query, crash). Then 3 and 4. Step 5 comes last, after its measurement.
- Code is complete, compiling and tested; stubs are marked and [[EMIL: finish this line — the project prompt is cut off here]]
````
`````

## The report

### What is in the pull request

- `docs/brief.md`: the brief, verbatim. **Its last line still carries the
  `[[EMIL: …]]` marker**: the prompt arrived with the gap unfilled, and the
  brief is the authority, so the session did not write the maintainer's words
  for them. Varve's own brief reads "No placeholder bodies unless explicitly
  marked as a stub and listed at the end", which suggests "stubs are marked and
  reported"; `AGENTS.md` uses that reading ("A placeholder body not marked as a
  stub and reported") until the brief is finished.
- `AGENTS.md` (the working method, rules, DD rules, commit form, never-list) and
  `CLAUDE.md`.
- `docs/spec/script.md`: S1–S7 and E, each with what the viewer sees, numbered
  acceptance criteria, a target and a check name; the common rules C1–C6; the
  facts F1–F7 as re-checked; the step table, all `pending`.
- ADRs 0001–0008, each `Proposed`, each with a decision set (48 rulings, all
  unaccepted), the ADR index and `docs/decisions/README.md`. The
  `DecisionDriven.Analyzers` generator reads the ledger: 8 sets and 48 types,
  each `[Obsolete]` until accepted.
- The build: `global.json` (SDK 10.0.401, as Varve), `nuget.config`,
  `Directory.Build.props`/`.targets`, `Directory.Packages.props` (VarveVersion
  `0.1.0-preview.11`; `DecisionDriven.Analyzers` `0.1.0-preview.8`; `xunit.v3`
  `4.0.2`; `Microsoft.Testing.Extensions.TrxReport` `2.5.1`, all resolved from
  nuget.org on 2026-10-23), `.editorconfig`, `.gitattributes`, `.gitignore`,
  `LICENSE` (byte-identical to Mozilla's MPL-2.0 text and to Varve's),
  `NOTICE`, `VarveDemo.slnx`.
- `tests/VarveDemo.Spec.Tests`: 17 gates. 16 pass and the compose gate skips,
  with its reason, until `compose.yaml` exists. Every gate was seen to fail on
  a planted violation (27 plants), and three control plants passed: a passing
  step with its test present, an allowed and pinned image, a matching compose
  tag. One control was not run: a superseded ADR without a set. Fresh clone:
  0 warnings, 17 tests, 16 passed, 1 skipped.
- `.github/workflows/ci.yml` (actions at Varve's SHAs: checkout v7, setup-dotnet
  v6, upload-artifact v7) and the README.

### Where Varve's clone contradicted the prompt

1. **"Every response carries `Varve-Position`."** Too broad. Varve ADRs 0094
   and 0096 say every response *that touched a dataset* carries it. Health
   routes do not, and neither do problems written before a dataset is resolved
   (`401`, `403`, an unknown dataset, a malformed `Varve-As-Of`). Spec F2 says
   so, and S2/S3 assert the header only on successful reads.
2. **S4's "pattern subscription".** `/commits` takes a `pattern` filter, but
   `FollowAsync`'s `FollowOptions` filters by graph only. S4 criterion 6
   subscribes by graph and shows the gap. Varve issue drafted below.
   *Changed in review: S4 now uses `/commits?pattern=` (ADR 0009; see the
   addendum).*
3. **Bulk load is embedded only** (`varve load` over a directory, no agent, no
   cause; no server endpoint, Varve ADR 0105). This bears on ADR 0008's open
   question. The ADR says so; the question stays open.
   *Closed in review: S1 is a full replay and bulk load is dropped (see the
   addendum).*
4. **"The sequencer never accepts a timestamp."** True of requests, but the
   embedded API exposes `DatasetOptions.Clock` (a host-supplied `TimeProvider`)
   and settable `CommitMetadata`. ADR 0004 records this and rejects driving the
   clock with catalog time on its merits (alternative 3). The decision is
   unchanged.
5. **Anonymous mode has more names than `--anonymous`.** It is also
   `--auth-mode Anonymous`, `Varve:Auth:Mode` and `VARVE__AUTH__MODE`. The
   gate and ADR 0007 refuse all of them.
6. **Roles are not fixed.** Varve grants permissions to roles by management-plane
   declarations applied with `varve apply`. ADR 0007 makes the operator client
   the one that applies them.
7. *Corrected in review; this was the session's misreading, not a fault in
   Varve.* `Varve:Limits:MaxAsOfDistance` (default 10 000) bounds an as-of
   read's distance from the nearest checkpoint at or below its position, so
   that an as-of read never replays an unbounded log. It is not a distance from
   the head. The first draft of ADR 0004 raised it above the snapshot's length,
   which disables that bound; the review reverted it. ADR 0004 now keeps the
   default and requires checkpoints (see the addendum). Varve's operator guide
   describes the bound as "how many positions below the head"
   (`docs/operator/configure.md:169`), where its decision set and code say
   "from the nearest checkpoint"; the session read the guide.
8. `DecisionDriven.Analyzers`' nuspec `authors` is `Hafeok`, not
   `decision-driven-design`. That does not contradict ADR 0002, whose test
   applies to Varve's own packages.
9. The image attestation could not be verified from this session: the session's
   GitHub scope refused the attestations API. CI verifies it before slice 1
   uses the image (ADR 0002).

### Varve issues to file

Drafts only: the maintainer files them in Varve. Updated in review to match the
review's decisions; the reproductions cite Varve at `v0.1.0-preview.11`.

1. **As-of by a declared domain clock.** Reproduction: import history whose
   domain time differs from import time, e.g. two commits made a second apart
   over HTTP, each `INSERT DATA`-ing `<urn:x> <urn:at>
   "2016-01-01T00:00:00Z"^^xsd:dateTime` and then `"2020-01-01…"` into a log
   graph. `Varve-As-Of: time:2018-01-01T00:00:00Z` answers `404
   before-first-commit`, because it resolves against Varve's commit timestamps
   (both from today; Varve ADR 0096). The domain's 2018 state is position 1,
   and no request can ask for it without the client keeping its own index.
   Proposal: a dataset declares a predicate (or graph) as its domain clock,
   monotonic by position; Varve keeps it beside the commit index, and
   `Varve-As-Of` takes a form such as `domain-time:<RFC 3339>` that resolves
   to the greatest position whose declared time is at or before it.

2. **Verify a dataset's hash chain on demand.** What opening a dataset verifies
   today, from the source:
   - **Every position, every segment and record header.** Each segment header
     continues the chain or the log is refused (`src/Varve.Store/Log.cs:287-311`).
     Each record header's self-hash, its position, its `prev` against the hash
     of the header before, and its index and kind within its commit
     (`Log.cs:419-452`). Each commit header's position, kind and `prev`, read
     from the closing record's tail (`Log.cs:674-703`, `ReadClosingTailAsync`
     at `Log.cs:541`).
   - **Bodies only above `bodiesAfter`.** `bodiesAfter` is the highest of the
     newest checkpoint, the persisted projection state's end and the extent
     catalog's end: "what derived/ claims to hold" (`src/Varve.Store/Dataset.cs:295-300`).
     Above it (`full = expected > bodiesAfter`, `Log.cs:456`), each record
     body's SHA-256 is checked against its header (`Log.cs:461-467`), and the
     commit's content hash against its bodies (`Log.cs:710-712`). At or below
     it, the body is skipped (`pending.Skip()`, `Log.cs:473`), and the commit
     closes before the content-hash check (`Log.cs:705-708`).

   So after a checkpoint, or once the projection has been persisted, a body
   byte flipped at or below that point passes `Dataset.OpenAsync`. The header
   chain still covers each body's hash, so the damage is detectable, and it is
   detected late: a later read of that commit (`Log.ReadAsync`, used when a
   commit's body is needed after the open) checks each body's hash and refuses
   with `LogVerificationException` (`Log.cs:600-639`). So damage below the
   newest checkpoint surfaces only when, and if, something reads that
   commit. `Dataset.OpenAsync`'s own remarks say as much: "bodies
   are read only after the newest checkpoint that names this log"
   (`Dataset.cs:267-271`). Nothing re-checks them on demand: the CLI has
   `create`, `info`, `load`, `query`, `update`, `export`, `checkpoint`,
   `feed`, `setup`, `apply`, `lease`, `storage` and `serve`
   (`src/Varve.Server/Commands/Cli.cs:25-37`), and `varve info` verifies only
   as a side effect of opening. Reproduction: create a dataset, write three
   commits, take a checkpoint, stop the server; flip one byte inside the body
   of position 1's record in `log/00000000.seg` (not its header); start the
   server. It opens and serves; the damage is reported only when position 1's
   body is next read. Proposal: `varve verify <dir>` and an operator
   route that verify every header and every body against the chain, and
   report the positions checked, the head hash and the first failure. The
   demo's S6 needs it so the viewer sees the whole log checked, not the part
   above the newest checkpoint.

3. **A checkpoint policy configurable on the server.** `DatasetOptions.Checkpoints`
   (`CheckpointPolicy`: `EveryCommits`, `EveryLogBytes`) defaults to
   `CheckpointPolicy.Never` (`src/Varve.Store/Dataset.cs:72-74`). The server
   builds each dataset's options without setting it
   (`src/Varve.Server/OpenDatasets.cs:402`), and `Settings.cs` has no key for
   it, so a server never checkpoints unless an operator asks
   (`POST /manage/datasets/{name}/checkpoints`). Reproduction: serve a dataset
   on files with default settings; write 10 002 commits over HTTP; `GET
   /manage/datasets/d/status` lists no checkpoints; a query with `Varve-As-Of:
   position:10001` is `422 as-of-distance-exceeded` with `actual` 10001,
   because the distance from position 0 exceeds `MaxAsOfDistance` (Varve ADR
   0114; `src/Varve.Protocol/Http/Reads.cs:179-198`). Proposal: a checkpoint
   policy in server configuration (server-wide, or per dataset in its
   declaration) that feeds `DatasetOptions.Checkpoints`. Secondary: name, on
   an as-of response, the checkpoint the read started from. Today a client
   derives it from `/manage/datasets/{name}/status`, which needs `admin`.
   Until then the demo takes the checkpoints itself through the operator
   client, as Varve's operator guide documents (demo ADR 0004), and stops when
   this ships.

4. **A pattern filter on `FollowAsync`**, a convenience, not a gap: S4 uses
   `/commits?pattern=` (demo ADR 0009). `FollowOptions` has only `Graph`, and
   `FollowAsync` sends only `from` and `graph`
   (`src/Varve.Protocol.Client/FollowOptions.cs:26-27`, `Follow.cs:152-158`).
   Proposal: `FollowOptions.Pattern`, passed through. Worth adding with it,
   because nothing in Varve tests these today: a test of a pattern combined
   with `from`/`Last-Event-ID` on an open tail, and one of the store-side
   `SubscriptionFilter.ForPattern` path. The change-feed specification's §6
   resumption property is stated as tested "by generated histories", and no
   such test of the feed was found.

5. **The operator guide describes the as-of bound wrongly.** Varve's
   `docs/operator/configure.md:169` says:

   > `MaxAsOfDistance` | 10,000 | how many positions below the head a
   > `Varve-As-Of` read may go; further is `422 as-of-distance-exceeded`; a
   > checkpoint at or below the position makes any distance cheap (ADR 0114)

   Varve's accepted ruling `AsOfDistanceBoundedBeforeTheViewOpens`
   (`docs/decisions/resource-governance.md:18-19`, ADR 0114 point 3) says:

   > Varve:Limits:MaxAsOfDistance bounds an as-of position's distance from the
   > nearest checkpoint at or below it; beyond it is 422
   > as-of-distance-exceeded, decided from Dataset.Checkpoints before any view
   > is opened, so an as-of read never scans an unbounded log

   The code agrees with the ruling (`Dataset.AsOfLogDistance`,
   `src/Varve.Store/DatasetExtents.cs:1311-1322`). Reproduction: on a dataset
   with one checkpoint at position 20 000 and a head at 25 000, a read as of
   position 100 is 24 900 below the head, so by the guide it is refused. In
   fact it is served, at distance 0. Proposal: the guide's row reads "how many
   commits above its nearest checkpoint (or position 0) a `Varve-As-Of` read
   may replay; a position at or below the newest checkpoint replays none". A
   documentation fix; this misreading led the demo's first draft of ADR 0004
   to raise the bound.

### Where I disagree with the prompt

1. **The brief gap should be filled before acceptance.** I left it marked rather
   than guess. Accepting the PR with the marker in the authority document would
   make the authority incomplete.
2. **S1's 3-minute target and "replay everything" may not fit together.** The
   top 3 000 packages across the catalog's whole history are likely to be
   hundreds of thousands of catalog commits. Over HTTP each one is a request
   and a durable flush. The snapshot's time window, not N, decides whether S1
   can be met by replay. ADR 0008 keeps the question open for slice 1's
   measurement, as the prompt asks. I would add the window as a second knob
   beside N.
3. **S4: showing the gap versus using `/commits?pattern=` directly.** The
   protocol's `/commits` is public API too, so S4 could show a real pattern
   subscription today, at the cost of re-implementing at-least-once resumption.
   I chose the gap, because the prompt pins S4 to `FollowAsync`. Reasonable
   people could choose the other way.
4. **E's target** was not given in the prompt; "every CI run on `main` and every
   bump, in ≤ 30 min of CI" is mine and is a proposal.
5. **`main` did not exist.** The repository was empty, so a pull request had no
   base. The branch starts from an empty root commit, `chore: start the
   repository`, so that `main` can be created there without carrying any of the
   foundation's content. Creating `main` was put to the maintainer, not done
   silently.

## Addendum — review of the pull request, 2026-10-23

Same session, same tool and model (Claude Code 2.1.309, `claude-opus-5-5`).
The maintainer's review of [the pull request](https://github.com/Hafeok/varve-demo/pull/2),
received as the session's next prompt.

### The prompt

Verbatim.

`````markdown
Review of PR #2. Decisions from Emil, then changes to make on the branch. Commit each logical change separately with `Refs #1`, and keep the existing `Closes #1` commit last (rebase onto it or amend the order, your call; don't merge).

1. Brief. Emil finishes the cut-off line as: "Code is complete, compiling and tested; stubs are marked and reported." Write exactly that into docs/brief.md, replacing the [[EMIL: …]] marker, in its own commit whose message says the line is Emil's wording, decided in review. AGENTS.md already matches; check it.

2. As-of cap — your fix is wrong, revert it. Varve:Limits:MaxAsOfDistance bounds the distance from the nearest checkpoint at or below the requested position, not from the head (see Varve's decision set for it: "so an as-of read never scans an unbounded log"). Raising it above the snapshot length disables that bound and makes every slider move potentially replay the whole log. Instead, in ADR 0004 and the spec: keep the default limit, require a checkpoint policy that checkpoints at least every MaxAsOfDistance commits during import (resolve from the Varve clone how that policy is configured on the server and cite it), and make S3 state on screen which checkpoint an as-of read started from. That is evidence, not a footnote. Update the decision set: remove the ruling that raised the limit, add one for the checkpoint policy. If the clone shows the policy cannot be configured from the server, say so and draft it as a fourth Varve issue rather than working around it.

3. S4 uses the protocol's /commits?pattern=, conditionally. The brief promises a pattern subscription and Varve has one as public HTTP API; only Varve.Protocol.Client's FollowAsync lacks the filter. Before changing the spec, establish from the clone (source and tests, not docs alone) whether a pattern-filtered stream resumes after a disconnect and a restart without missing a commit: Last-Event-ID or an equivalent position parameter, and how paging of history up to the head interacts with the filter.
   - If it resumes: S4 subscribes with /commits?pattern= through a small client in this repo, named and documented as ours. Acceptance criterion 3 (stop, restart, resume, nothing missed, duplicates counted and shown) stays as written. Downgrade "pattern filter on FollowAsync" to a convenience issue.
   - If it does not: keep S4 on FollowAsync by graph, show the gap, and make "a pattern-filtered feed that resumes" the Varve issue, with the reproduction.
   Either way, record the evidence in ADR form: add ADR 0009 "S4's subscription path", filed unaccepted, with its decision set; update the spec and the index.

4. S1 is a full replay inside a time window; bulk load is dropped. In ADR 0008, close the open question:
   - Replay every retained catalog commit over HTTP, one Varve commit each. Remove base-plus-bulk-load as an option, recording why in Alternatives: embedded only, needs the CLI to hold the dataset before serve, records no agent (contradicts ADR 0007's every-write-has-an-agent), and flattens history before the base.
   - The snapshot has two parameters, both in the manifest: N (top packages, 3 000) and a catalog time window. The window's lower bound is where the catalog starts carrying deprecation and vulnerability data, because S2 and S3 depend on it. Do not take that date from memory: slice 1 determines it from the catalog itself and records it here by amendment. The upper bound is the snapshot date.
   - Slice 1 measures the retained commit count and the replay rate, and tunes the window, never N, to meet S1's target. If no window that still contains the S2/S3 story meets the target, the target moves and the README says so.
   Update the spec's S1 acceptance criteria and the decision set to match.

5. Keep, as you had them: Varve-Position only on dataset responses; the gate refusing every anonymous alias; roles granted by the operator client via `varve apply`; attestation verified in CI; your E target.

6. The superseded-ADR control you could not run: add it as a test fixture instead of a deletion. A temporary directory with an ADR marked superseded and no set, run the check against it. Then the gate's allowance is tested in CI, not just reasoned about.

7. Varve issues: don't file them. Emil files them in Varve. Update the drafts in the traceability record so they match these decisions (the as-of-cap point should not appear as a Varve problem; the S4 issue depends on point 3's outcome), and make the "verify the chain on demand" draft precise: state exactly what opening a dataset verifies today and what it does not, citing the clone's source by file and line, so "only in part" is a fact and not an impression.

8. Then watch the PR. Fix CI failures. Answer review comments with changes or reasons. Never accept an ADR and never write accepted-by. When done, add an addendum to the traceability record with this prompt verbatim and what you changed, and report back briefly: what changed, the outcome of point 3 with its evidence, and anything you disagree with.
`````

### What changed

One commit per point, each `Refs #1`, with this record's commit kept last and
closing #1.

1. **Brief.** The last rule is now "Code is complete, compiling and tested;
   stubs are marked and reported.", Emil's wording, decided in review.
   `AGENTS.md` already matched (its "How we work" and never-list).
2. **As-of bound.** The first draft's misreading is reverted: the bound is a
   distance from the nearest checkpoint, not from the head.
   - ADR 0004 keeps Varve's default and requires a checkpoint at least every
     `MaxAsOfDistance` commits during the import.
   - S3 shows on screen where each as-of read started: "served from the extents
     up to checkpoint c" or "n commits replayed after checkpoint c". CI holds
     that derivation to the `actual` in Varve's own `422`.
   - The clone shows the server **cannot** be configured with a checkpoint
     policy (`Dataset.cs:72-74`, `OpenDatasets.cs:402`, no key in
     `Settings.cs`, and Varve's guide: "the server's defaults write none").
     That is Varve issue draft 3, and S3 shows the refusal until it ships.
   - The decision set loses `AsOfDistanceAboveSnapshot`. It gains
     `AsOfAnswerShowsItsCheckpoint`, `CheckpointAtLeastEveryMaxAsOfDistance`
     and `ServerCheckpointPolicyIsAVarveIssue`.
   - Contradiction 7 above is corrected in place as the session's error.
3. **S4.** ADR 0009, "S4's subscription path", is filed unaccepted with its
   decision set. The outcome is below. The spec's F5 and S4, the ADR index,
   the README and a `Varve.Delta` pin at `VarveVersion` follow.
4. **S1 and the dataset.** ADR 0008's open question is closed:
   - S1 is a full replay over HTTP, one Varve commit per retained catalog
     commit.
   - Bulk load is dropped, with the four reasons under Alternatives.
   - The snapshot has N (3 000) and a catalog time window in its manifest. The
     window's lower bound comes from the catalog in slice 1, by amendment.
   - The window is tuned, never N.
   - The spec's S1 criteria and F7 follow. The decision set swaps
     `ReplayOrBaseByMeasurement` for `SnapshotHasNAndAWindow`,
     `WindowStartsWhereTheCatalogCarriesAdvisories`, `FullReplayOverHttp` and
     `TuneTheWindowNeverN`.
5. **Kept as they were:** `Varve-Position` on dataset responses only, the gate
   refusing every anonymous alias, roles granted by the operator client, the
   attestation verified in CI, and E's target.
6. **Superseded-ADR fixture.** `AdrLedgerFixtureTests` builds a ledger in a
   temporary directory and runs the gate's own check (`AdrFiles.WithoutASet`)
   on it, in four cases. Planted: with the allowance broken, the superseded
   case fails; with an ADR in force losing its set, the gate fails.
   21 tests: 20 pass, 1 skipped (the compose gate, with its reason).
7. **Varve issue drafts** (above) now match the decisions:
   - there are four drafts;
   - the as-of point is no longer a Varve problem;
   - the `FollowAsync` pattern is a convenience;
   - the chain-verification draft states, file by file and line by line, what
     an open checks and what it does not.

   The spec's F6 and S6 now say the same, in their own commit. No issue was
   filed.

### Point 3: the outcome

**It resumes.** From Varve's source at `v0.1.0-preview.11`:
- **Resumption is by position.** Resumption is exclusive, by `from` or by
  `Last-Event-ID`, which replaces it (`CommitsEndpoint.cs:129-131`,
  `Ranges.cs:29-45`). A cut stream ends with an event naming the last position
  delivered (`CommitsEndpoint.cs:333-350`).
- **A filtered-out commit is never a position the client holds**
  (`CommitsEndpoint.cs:284`). So resuming after the last position applied
  re-filters everything after it and skips nothing.
- **The pattern is re-resolved on every request.** A term the dataset does not
  know yet is learned from the commit that allocates it
  (`CommitsEndpoint.cs:175-179`; `FeedFilter.cs:15-21, 112-121, 134-150`).
- **Paging.** An open range is one unpaged stream from `from` to the head and
  on (`Ranges.cs:92`). Only a bounded range is paged, and its `next` link keeps
  `pattern` (`CommitsEndpoint.cs:162-168, 188-206`).

**Tests** cover each part:
- a pattern on a bounded range, with a learned term
  (`Varve.Protocol.Tests/BehaviourTests.cs:235-247`);
- an unfiltered resume by `Last-Event-ID` (`:250-271`);
- graph-filtered paging (`GovernanceTests.cs:180-204`).

None covers the combination: no test pairs a pattern with resumption, none
exercises `SubscriptionFilter.ForPattern`, and no generated-history test of the
feed's resumption property was found. So S4 uses `/commits?pattern=` through
`VarveDemo.PatternFeed`, ours. Criterion 3 stays, and it now also restarts the
server mid-tail, which puts the untested combination under CI.

### Where I disagree, or the review should know

1. **The checkpoint mechanism (point 2).** Varve's own operator guide gives an
   operator procedure: `POST /manage/datasets/{name}/checkpoints`, which is
   public API. Having the importer's operator client call it every
   `MaxAsOfDistance` commits would meet the requirement today. I don't think
   that is a workaround, but the review said not to work around, so it is not
   adopted. It is recorded as ADR 0004's alternative 7 for a ruling. As it
   stands, S3 cannot reach positions more than 10 000 commits past the last
   checkpoint until Varve ships the server-side policy.
2. **A refinement, not a disagreement:** since Varve ADR 0138, any position at
   or below the newest checkpoint has distance 0 (`DatasetExtents.cs:1311-1322`).
   So one checkpoint at the end of the import already puts all history in
   reach. The "every `MaxAsOfDistance` commits" rule matters during the import
   and for live commits after it. It is still the right rule.
3. **ADR 0007 has no explicit "every write has an agent" ruling.** It follows
   from "never anonymous", and ADR 0008 now cites it that way. If you want it
   as a ruling of its own, it is a one-line addition to ADR 0007's set.
4. **The brief's step 1 says "bulk load with the load rate shown".** ADR 0008
   reads that as "the whole snapshot loaded, rate shown", not as Varve's bulk
   loader, and says so. If the brief meant the API, this is a departure from
   the authority and should be recorded as one.
5. **Varve's operator guide describes the bound wrongly.** It calls the bound
   "how many positions below the head" (`docs/operator/configure.md:169`),
   which disagrees with its own decision set and code. A documentation fix for
   Varve, noted here and not filed.
6. **Two keys were removed from unaccepted sets** (`AsOfDistanceAboveSnapshot`,
   `ReplayOrBaseByMeasurement`). They were never accepted, so nothing cites
   them. They are not reused.

## Addendum — rulings on the review's five points, 2026-10-23

Same session, same tool and model. The maintainer's rulings, received after
CI went green on the previous head. This addendum was written once CI was
green on the head carrying them (`bef8c00`, run 3).

### The prompt

Verbatim.

`````markdown
Rulings on your five points. Same rules as before: one logical change per commit with `Refs #1`, `Closes #1` stays last, don't merge, never write accepted-by.

1. Checkpoints via POST /manage/datasets/{name}/checkpoints: adopted. It is documented public API, called by the operator client in the operator's role, and in the absence of a server-side checkpoint policy it is the documented way to operate Varve. Move it from ADR 0004's alternatives into the decision. Conditions: S3's screen says the importer takes the checkpoints and why; the "server-side checkpoint policy" Varve issue draft stays, and the decision states the importer stops calling the endpoint when Varve ships the policy. The operator client's grant must cover it; check the permission it needs against the clone and say which in ADR 0007.

2. The checkpoint rule becomes: one checkpoint at the end of the import, then whenever more than K commits have landed since the last one, K below MaxAsOfDistance and named in configuration. Drop checkpointing during the import. S3 states on screen that an as-of read anywhere in history is served from checkpoints with validity intervals; keep your CI cross-check against the 422's `actual` distance, and add one that an as-of read at an early position reports distance 0 after the final checkpoint, so the DatasetExtents.cs reading is tested, not cited. Update ADR 0004, the spec's S3 criteria and the decision set.

3. Add an explicit ruling to ADR 0007's decision set: every write in the log carries a real agent, the subject of the token that made it. Cite it from the S1 and S6 criteria that show the agent.

4. The brief: Emil amends step 1 to read "1. Zero to query: `docker compose up` or a `dotnet tool`; load the snapshot, with the load rate shown." Write exactly that into docs/brief.md in its own commit, saying it is Emil's wording from review and why (the old wording read as Varve's bulk-load API, which contradicts "one catalog commit maps to one Varve commit"). Then remove the departure note from ADR 0008 if you added one, since the brief now agrees with it.

5. The operator guide's description of the cap: Emil files it in Varve. Add a short issue draft to the traceability record next to the other four, quoting configure.md:169 and the decision set ruling it contradicts.

When CI is green on the new head, add the addendum to the traceability record with this prompt verbatim and what changed, and report back in a few lines. Emil then reviews and accepts the ADRs himself.
`````

### What changed

One commit per ruling, each `Refs #1`, with this record's commit kept last and
closing #1.

1. **Checkpoints by the operator route.** ADR 0004 moves the route from its
   alternatives into its decision. The importer calls
   `POST /manage/datasets/{name}/checkpoints` through the operator client, and
   S3's screen says it does and why. The server-side policy stays a Varve
   issue draft, and the importer stops when it ships.

   ADR 0007 now states the operator client's permissions, checked against
   Varve's source. It is a bootstrap operator on the management audience.
   That gives it `varve:server-admin`, which it needs to create the dataset
   (`ManagementEndpoints.cs:679-692`). It also gives it `varve:admin` on the
   dataset, which the checkpoint route and the status list demand
   (`AdminEndpoints.cs:247`, `StatusEndpoint.cs:29`), because an operator
   passes admin everywhere (`GrantTable.cs:87-94`). An `admin` grant on the
   dataset alone would suffice for the checkpoints.

   The status route is corrected to `/manage/datasets/{name}/status` in ADR
   0004, the spec and issue draft 3. It had been given as
   `/datasets/{name}/status`, which is wrong (`VarveEndpoints.cs:99-100`).
2. **One final checkpoint, then every K commits.** There are no checkpoints
   during the import. K is `VarveDemo:Checkpoints:EveryCommits`, proposed at
   5 000. The importer refuses to start unless K is below the
   `varve:maxAsOfDistance` the server advertises
   (`ServiceDescriptionEndpoint.cs:239`).

   S3 states that an as-of read anywhere in history is served from checkpoints
   with validity intervals. To test the distance-0 reading rather than cite
   it, CI runs a server whose `MaxAsOfDistance` is 0, which Varve allows
   (`SettingsCheck.cs:268`). There, any non-zero distance is a `422` carrying
   it as `actual`:
   - before the final checkpoint, a read at `p` is refused with `actual` `p`;
   - after it, an early read succeeds, which is Varve's own check that its
     distance is 0.
3. **Every write carries its agent** is now a ruling of its own,
   `EveryWriteCarriesItsAgent`, in ADR 0007. Varve's source shows a token with
   no `iss` or no subject claim, or an issuer containing `#`, gives no agent
   even under OIDC (`Identity.cs:26-40`). So every mock client gets a `sub`.
   S1's criterion 6 and S6's new criterion 5 read the agent from each commit's
   feed record and cite the ruling.
4. **The brief's step 1** now reads exactly as Emil wrote it, in its own
   commit, and ADR 0008's note on the old wording is gone.
5. **Issue draft 5**, on the operator guide's description of the bound, sits
   beside the other four, quoting `configure.md:169` and the accepted ruling
   `AsOfDistanceBoundedBeforeTheViewOpens`.

Decision sets: `ImporterTakesCheckpointsAsOperator`,
`OperatorClientIsABootstrapOperator`, `CheckpointDerivationHeldToVarve`,
`FinalCheckpointThenEveryKCommits`, `KIsConfiguredBelowTheBound` and
`EveryWriteCarriesItsAgent` are added. `CheckpointAtLeastEveryMaxAsOfDistance`
is removed. All are unaccepted; no `accepted-by` was written.

On a fresh build, `dotnet build` gives 0 warnings. `dotnet test` gives
21 tests, 20 passed and 1 skipped (the compose gate, with its reason). CI is
green.
