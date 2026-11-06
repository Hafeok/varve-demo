# Session 03 — bump Varve to `0.1.0-preview.15` (#3)

- **Issue:** [#3, Bump Varve to 0.1.0-preview.15](https://github.com/Hafeok/varve-demo/issues/3)
- **Date:** 2026-11-03
- **Tool:** Claude Code 2.1.317, in a cloud session
- **Model:** `claude-opus-5-5`, as the session's metadata reports it
  (configured and last served)
- **Branch:** `claude/hopeful-ramanujan-v3a1ex`

Session 03 has two pull requests: this one, the bump, and the slice 1a pull
request that branches from it. This record covers the bump and carries the
session's prompt; the slice's own record is filed on its issue.

## The prompt

Verbatim, as the session received it.

`````markdown
Session 03 — Varve bump, then slice 1a: snapshot, importer, S1 and S2
Hand-off prompt for a Claude Code session on `Hafeok/varve-demo`. It is written for the state after PR #2 is merged with ADRs 0001–0009 accepted (main at `39d874a`). Session 02 was the acceptance session. Paste everything below the line.
Slice 1 from the README (S1, S2, S6 and ADR 0005's measurement) is split across two sessions, each with its own pull request. This session covers 1a: the snapshot, the importer, step S1 and step S2. Session 04 covers 1b: step S6 and the browser measurement. Both depend on what this session builds.
You are working in `varve-demo`. Read `AGENTS.md`, `docs/brief.md`, `docs/spec/script.md` and ADRs 0001–0009 before anything else. They are the authority, and this prompt only selects the work and records decisions made in planning since then.
Clone Varve read-only beside the repository, as session 01 did, and use it as a reference for its public surface.
First: bump Varve, as its own pull request
The repository pins `0.1.0-preview.11`. Varve has since released up to `0.1.0-preview.15` (as of 2026-11-03). Preview.14 adds the SHACL validation projection. Preview.15 adds derived format 5 and as-of reads that seek, and amends ADR 0159 on `MaxSetBytes`. Re-check ADR 0004's checkpoint and distance reasoning against preview.15 specifically.
ADR 0002 says a bump is one pull request, so do it before the slice, on its own branch, with its own issue:

* Move `VarveVersion` to the latest release on nuget.org, which you resolve yourself.
* Re-check every claim the spec and the ADRs make about Varve against the clone at that release's tag. Read `releases/` and `CHANGELOG.md` for what changed since preview.11. Correct anything that no longer holds, in its own commit, citing the release that changed it.
* When the ADRs are accepted, a correction is a dated amendment, never an edit (ADR 0001).
* Also in this pull request, as its own commit: propose a dated amendment to ADR 0001, filed unaccepted. Align its rule with Varve's wording: "a session never writes `accepted-by` for a decision it filed". Then a later session may transcribe an acceptance at the maintainer's explicit instruction, with his identity and timestamp, recorded in its traceability record. Session 02 did exactly that, and its record notes it as a departure. Update `AGENTS.md` to match.
* Merge nothing. Emil lands the bump, and the slice branches from it.

Varve issues already filed
Link these from the spec and ADRs wherever a draft in session 01's traceability record was cited, and do not draft them again:

* Hafeok/Varve#157: as-of by a declared domain clock (ADR 0004).
* Hafeok/Varve#158: verify the log end to end on demand (S6).
* Hafeok/Varve#159: a checkpoint policy in server configuration (ADR 0004).
* Hafeok/Varve#160: a pattern on `FollowAsync`, and its resumption tested (ADR 0009).
* Hafeok/Varve#161: the operator guide's description of `MaxAsOfDistance`.
* Hafeok/Varve#162: under OIDC, a write whose token yields no agent (ADR 0007).

From now on, a new Varve gap is drafted in the traceability record, and the planning chat files it. Never file one yourself.
Goal
After this pull request, a clean machine with Docker runs `docker compose up` and gets this:

1. A mock OIDC issuer and the Varve server start.
2. The importer loads the pinned snapshot over HTTP, one Varve commit per retained catalog commit, and shows the rate.
3. The importer takes the checkpoints ADR 0004 requires.
4. A query returns the count the manifest predicts.

`varve-demo deps --lock <path>` then answers S2. CI runs both steps end to end as `S1_ZeroToQuery` and `S2_VulnerableDependencies`. If both pass, both rows in the spec table and in the README become `passing`, with the measured numbers beside the targets.
Before code: issue and ADRs

1. Open an issue, "Slice 1a: snapshot, importer, S1, S2", listing the deliverables below. Every commit carries `Refs #N`, and the last commit closes the issue.
2. File these ADRs unaccepted, each with its decision set, before the code that depends on them. They are numbered from 0010.
   * The graph model and vocabulary.
      * Use the catalog's own JSON-LD terms as `Varve.JsonLd` expands them.
      * Add a small namespace of our own for anything derived. Do not invent a `w3id.org` prefix without registering it. A `urn:` namespace is fine, and the ADR says why.
      * The ADR must decide how versions are ordered for S2's "what should I move to". SPARQL cannot compare NuGet versions as strings. The recommended approach: the importer asserts a sortable version key it computes itself, by NuGet's SemVer 2.0 rules, and the independent oracle orders versions with `NuGet.Versioning`, which is a separate implementation and needs an ADR line to enter. They must agree on every version in the snapshot, and CI checks that.
   * Our container images.
      * We publish the importer, and later the browser page, as images on GHCR. Compose pulls them from there and never builds them.
      * A published image's base is `mcr.microsoft.com/dotnet/runtime-deps` chiseled, pinned by digest. This is the same exception Varve makes in its ADR on the container image; cite that ADR by number. The pull from MCR happens only in our image build, never in compose or tests.
      * The hygiene gate allows MCR in those Dockerfiles by name and nowhere else. Add a test that a stray MCR reference elsewhere still fails.
      * On a pull request, CI builds the importer image and tags it `pr-<sha>`, as Varve does, and the step checks run against that tag.
      * Publishing to GHCR happens on `main`. ADR 0002's attestation check applies to our images too.
   * The demo tool. `VarveDemo.Tool` is a .NET tool with the command `varve-demo`. It is Native AOT-compatible and packable.
      * In this slice, CI packs it and installs it from a local folder source. That is how S1's criterion 5 is checked.
      * Publishing to nuget.org waits for the demo's first release. Say that in the ADR and in the README.
3. Amend ADR 0008 with a dated amendment, for the catalog time window's lower bound (below), once you have measured it.

Deliverables
1. The subset and the snapshot (`VarveDemo.Snapshot`)

* Build the top-N id list from nuget.org's search API, at N = 3 000 as ADR 0008 says. Commit the list, the query, and the time it ran at under `snapshot/`.
* Find the window's lower bound from the catalog itself. Look for the earliest catalog commit whose leaves, among the subset, carry deprecation data, and the earliest that carry vulnerability data. Record both in the ADR 0008 amendment, with the catalog page and commit you found them in. Then propose the lower bound. It must be early enough that S3 will have advisories that appear inside the window. Do not take any of these dates from memory.
* The snapshot is a pure function of the id list, the window and the tool version. The catalog is append-only, and its leaves are immutable. The manifest records:
   * the counts: catalog commits retained, catalog commits skipped, leaves, packages and versions;
   * the bounds;
   * the context documents and their hashes;
   * a hash for each file and one hash over the whole snapshot.
Commit the manifest. CI rebuilds the snapshot from nuget.org and fails if the hash differs. That makes a test out of the claim that the fixture is reproducible.
* A `workflow_dispatch` job on `main` publishes the snapshot as a release asset. The importer downloads it, verifies its hash, and uses it. The importer also accepts a local path, which is how a pull request's CI runs.

2. The importer

* Read each leaf with `Varve.JsonLd`, with the snapshot's contexts supplied through `JsonLdOptions.DocumentLoader` (ADR 0006).
* Send one SPARQL Update per retained catalog commit (ADR 0008). It replaces or drops the graph of each package version, and asserts the catalog commit's `commitId` and `commitTimeStamp` in `<urn:varve-demo:catalog-log>` (ADR 0004). Check the response's `Varve-Position` and record it.
* Take a checkpoint every K commits, and one at the end, through the operator's route (ADR 0004). Before starting, refuse to run unless K is below the server's advertised `maxAsOfDistance`.
* Print the progress described in S1.3: catalog commits per second, quads per second, elapsed time, and the final totals. Every figure is measured in the run. Stop on the first refused leaf or update, and name it (ADR 0006, point 4).
* Get tokens by client credentials from the issuer. No code path skips authentication.

3. `compose.yaml` and `deploy/`

* The Varve image, pinned by tag and digest. It matches the package pin; the skipped gate now runs.
* The mock issuer, with `deploy/mock-oauth2.json` as ADR 0007 describes. Every client has a `sub`.
* The importer image.
* Grants applied by the operator client with `varve apply`. The importer gets write. Checkpoints need `admin` on the dataset, and the importer gets that alone, not the bootstrap operator's `server-admin` (see "Least privilege").
* The dataset on a named volume.
* Ports, and the one command, documented in the README.

Least privilege. The bootstrap operator holds `server-admin`. Use it only to create the dataset and apply the grants, once, in a one-shot setup service. Checkpoints are taken under a dataset `admin` grant. You decide whether that grant is held by the importer client or by a separate `checkpointer` client; record the choice in ADR 0007 by amendment, with reasons. A sceptical viewer will check that the long-running services hold only what they need.
4. S2 (`varve-demo deps`)

* Commit a sample `packages.lock.json` under `samples/`. Choose real packages from the subset so that the answer includes at least one deprecated version and at least one vulnerable version. Do not choose them from memory: pick them by querying the snapshot.
* Load the S2 query from a `.rq` file. The tool prints the query, runs it, and prints the answer, as S2's acceptance criteria require. It lists locked packages that are not in the subset, under that heading.
* Build the oracle (`VarveDemo.Oracle`, test-only). It reads the snapshot's JSON with `System.Text.Json`, orders versions with `NuGet.Versioning`, and references no Varve package. Add a test that its project references no Varve assembly.

5. The checks

* `S1_ZeroToQuery`: on a GitHub-hosted runner, `docker compose up` with the PR's image tag. Wait for the import to finish. Compare the count query with the manifest. Compare the measured time with the target, excluding image pulls; measure and report the pulls separately. Repeat the flow through the locally installed tool.
* `S2_VulnerableDependencies`: the sample lock file, with the answer compared to the oracle's, field by field.
* Both checks publish their measured numbers as a CI artifact, in a machine-readable form. The evidence page will read them later, so choose the format with that in mind and say why in the pull request.
* Do not change a target to match a measurement. If a target is missed, the pull request says so with the numbers, and proposes a revised target or window for Emil to decide.

Rules for this session
These are on top of `AGENTS.md`.

* Network. If nuget.org or GHCR is unreachable from your session, say so and stop that part. Never substitute data you have not fetched.
* Memory. Varve#222 (open) reports that resident memory grows with every commit on a steady dataset. The import is a long run of commits, so record the server's resident memory at the start and the end of the S1 check, and in the artifact. If it grows, link #222 in the pull request. Don't work around it.
* Varve gaps. Anything Varve does not do that you need becomes an issue draft in the traceability record, with a reproduction. You never file it. The step shows the gap, or the work waits.
* Acceptance. Never write `accepted-by`. Never merge.
* Watch the pull request. Fix CI failures and answer review comments.

Report back
Keep the report short:

* the pull request link;
* the measured S1 and S2 numbers against their targets;
* the window bounds and how you found them;
* the snapshot's counts;
* new ADRs, and amendments to existing ones;
* Varve issue drafts;
* anything in this prompt you disagree with, and why.

File the traceability record with this prompt verbatim.
`````

## Report: the bump

### What was resolved, and from where

- **nuget.org**, read 2026-11-03: the latest version of `Varve.JsonLd`,
  `Varve.Protocol.Client`, `Varve.Delta` and `Varve.Store.Browser` is
  `0.1.0-preview.15`, published 2026-10-26T15:18Z; `authors` is
  `decision-driven-design`.
- **ghcr.io**, read 2026-11-03: `ghcr.io/hafeok/varve:v0.1.0-preview.15` is
  the image index
  `sha256:f3c1651a11256d35be1330b82d207a9d8b50ff1f39fb61b2ac059087d592d706`.
  No `compose.yaml` exists yet, so the digest is recorded in
  `Directory.Packages.props` and ADR 0002's amendment, and slice 1a writes it
  into compose. The attestation is not verified from this session: `gh`
  has no GitHub credential here. CI verifies it when compose first uses the
  image (slice 1a).
- **The Varve clone**, at `v0.1.0-preview.15`
  (`6cdb40264e68d5ce97734d37e62a61f376d70cfe`), read-only, beside the
  repository. `releases/v0.1.0-preview.12.yaml` to `-15.yaml` and
  `CHANGELOG.md` read.

### What changed between preview.11 and preview.15, for the demo

Every file the spec and ADRs cite was diffed between the two tags.

| Cited | Change | Effect on the demo |
|---|---|---|
| `DatasetExtents.cs` `AsOfLogDistance` | moved 1305-1322 → 1396-1413; body unchanged | none; ADR 0004 amended with the new lines |
| `Http/Reads.cs` 179-198, `ProtocolLimits`, `SettingsCheck.cs:268`, `ServiceDescriptionEndpoint.cs:239` | unchanged | none |
| `Dataset.cs:72-74` (`CheckpointPolicy.Never`) | unchanged at those lines | none |
| `OpenDatasets.cs:402` | now 413, still no checkpoint policy | none; new line cited |
| `Settings.cs` | unchanged | the server still has no checkpoint policy (Varve#159 stands) |
| `ManagementEndpoints.cs` 389, 679-692 | now 390, 783-796; same code | ADRs 0004 and 0007 amended with the new lines |
| `StatusEndpoint.cs:29` | `admin` check at 30 at both tags; preview.13 adds an `events` member | the citation was off by one; corrected by amendment |
| `ServerHost.cs` 376-381, 415, 459-470 | same lines, same code | none |
| `Identity.cs`, `Writes.cs`, `Permissions.cs`, `GrantTable.cs`, `AdminEndpoints.cs` | unchanged | none |
| `FollowOptions.cs`, `Follow.cs`, `CommitsEndpoint.cs`, `Ranges.cs`, `FeedFilter.cs`, `SubscriptionFilter.cs`, `change-feed.md`, the three test files | unchanged | ADR 0009 holds as written |
| `src/Varve.JsonLd` | unchanged | ADR 0006 holds |
| `docs/operator/configure.md:169` | unchanged | Varve#161 still applies |
| `docs/operator/backup-and-restore.md` | `MaxSetBytes` now bounds merges, not reads (ADR 0178) | none: the demo never relied on it |
| Varve CLI | `varve manage move-events` added; no verify command | F6 and Varve#158 stand |
| `.github/ci-images.yaml` | mock-oauth2-server pin unchanged | ADR 0007 point 2 holds |
| `global.json`, build props, action SHAs | unchanged | ADR 0003 holds |

**ADR 0004 against preview.15 specifically.** Preview.15's as-of reads seek
inside an extent set (Varve ADR 0177, derived format 5) and `MaxSetBytes` no
longer bounds a read (ADR 0178; ADR 0159 amended). These change what an as-of
read from the extents costs, not the distance rule: `AsOfLogDistance` still
returns 0 at or below the newest set's end and otherwise the commits above
the nearest checkpoint, and the `422` is still decided on it before any log
is read. The final checkpoint still puts all imported history at distance 0,
and S3's "no log replayed" wording stays true.

### Commits

1. `build: bump Varve to 0.1.0-preview.15`
2. `docs(adr): re-check ADRs 0002, 0004, 0007 and 0009 against preview.15`:
   four dated amendments, filed unaccepted, with the Varve issues linked.
3. `docs(spec): re-check the facts at preview.15 and link the filed Varve
   issues`: the spec is not an ADR, so it is edited in place.
4. `docs(adr): propose amending ADR 0001 on who writes accepted-by`: a dated
   amendment and one new ruling, `AcceptanceTranscribedOnlyOnInstruction`,
   filed unaccepted, and `AGENTS.md` to match, marked as proposed.
5. This record.

`dotnet build` gives 0 warnings; `dotnet test` gives 21 tests, 20 passed and 1
skipped (the compose gate, until slice 1a adds `compose.yaml`).

### What the bump does not do yet, and why

ADR 0002 point 5 says a bump reruns every check, reruns ADR 0005's
measurement and regenerates the evidence page. No step check, measurement or
evidence page exists yet, so there is nothing to rerun; the first to exist
are slice 1a's, built on this pin.

### Where this goes beyond the prompt

ADR 0001's proposed rule has a third clause the prompt did not ask for: the
session that filed a decision never transcribes its acceptance, even on
instruction. It is what "for a decision it filed" means if read strictly, and
it keeps filing and acceptance out of one session's hands. Strike it at
acceptance if it is not wanted.

### Varve issue drafts

None from the bump.
