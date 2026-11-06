# Session 03 — slice 1a: snapshot, importer, S1, S2 (#5)

- **Issue:** [#5, Slice 1a: snapshot, importer, S1, S2](https://github.com/Hafeok/varve-demo/issues/5)
- **Pull request:** [#6](https://github.com/Hafeok/varve-demo/pull/6), based on
  the bump's pull request [#4](https://github.com/Hafeok/varve-demo/pull/4)
- **Date:** 2026-11-03
- **Tool:** Claude Code 2.1.317, in a cloud session
- **Model:** `claude-opus-5-5`, as the session's metadata reports it
- **Branch:** `claude/hopeful-ramanujan-v3a1ex-slice-1a`. The session was given
  one branch; the maintainer chose this second one for the slice when asked.

## The prompts

The session's opening prompt is recorded verbatim in the bump's record,
[`2026-11-03-issue-3-bump-varve-to-preview-15.md`](2026-11-03-issue-3-bump-varve-to-preview-15.md),
since one session carried both pull requests. The maintainer's rulings on the
slice followed, verbatim:

`````markdown
Rulings on your report. #4 will be merged first; rebase #6 onto main once it is.

1. GHCR. Emil is granting Hafeok/varve-demo Write access to the varve-demo-importer package (Manage Actions access). When he says it's done, re-run the steps job. If the push is still denied, report the full registry error and stop.

2. The window. A two-week window alone is rejected: a version enters the snapshot only if a leaf lands in the window, so a viewer's own lock file mostly answers "outside the subset", and "lowest clean version" ignores most versions. The sample passing because it was picked from the snapshot would read as rigged. Instead:
   - The snapshot gains a base: for every version of the N packages, its latest catalog leaf before the window's lower bound T0. The snapshot builder derives it deterministically from the catalog; the manifest records the base's counts and hash beside the window's.
   - The importer loads the base first, over HTTP, one Varve commit per package, under the importer's real agent. Each base commit asserts in <urn:varve-demo:catalog-log> that it is base state as of T0, so the time index places it at T0. Then it replays the window, one commit per catalog commit, as now.
   - S1's screen and S3's slider say plainly: base is the catalog's state as of T0, history before T0 is not replayed, the slider starts at T0. S2's "outside the subset" now means outside the N packages, nothing else.
   - The deep history stays rerunnable: `varve-demo import --since <date>` replays from any date. Measure and document how long it takes from the first-deprecation date you found; don't run it in the step checks.
   - Measure the base: versions, leaves, quads, and S1's total with it. If S1 misses its target, first trim each version's graph to the properties S2–S4 read, recorded in an ADR 0010 amendment, with the oracle reading the same fields. If it still misses, report the numbers: reducing N is Emil's decision, not yours.
   - Record this in the ADR 0008 amendment, replacing the three options with this decision, and remove the window-only cost notes from the spec and README.

3. Memory is not Varve#222: that issue measures about 36 bytes per commit, and you saw about 418 KB per commit, about 400 bytes per quad. Don't link #222. Measure resident memory at three points: at the end of the import before the final checkpoint, after it, and after a server restart. If it drops after the checkpoint, the growth is uncheckpointed state, and the importer may need checkpoints during long imports, for memory, not for as-of: propose that as an ADR 0004 amendment with K for memory named separately. If it stays high after a restart, draft a Varve issue with the numbers and a reproduction in the traceability record. Either way, the S1 artifact records all three figures.

4. ADR 0004's checkpoint rule: you were right and the prompt was wrong. Keep what you did.

5. ADR 0006: amend it. Leaves carry their @context inline, so no remote contexts exist today. The DocumentLoader stays refusing every remote context, so one appearing later fails the import loudly rather than being fetched. Adjust its decision set to match.

6. The narrowed checkpoint ruling: an amendment is fine, but an accepted key never changes meaning. Add a new key for checkpoints taken by the checkpointer client under dataset admin, and move the old key out as superseded, following Varve's rule that keys are never reused. File it unaccepted.

7. The S2 check name and S1's first answer: keep both as you have them.

Then finish as planned: pin compose.yaml to the pushed digest, flip S1 and S2 to passing only with CI's numbers, write the PR description, and file the slice's traceability record with this prompt verbatim. Report back with the base's counts, CI's S1 and S2 numbers, the three memory figures, and anything you disagree with.
`````

`````markdown
Hold the full local import with the base. At 512 390 versions and the window's ~377 quads per version, a full-leaf base is ~190M quads: ~80 minutes at your measured rate, and memory far beyond what the window used. Rulings:

1. One graph model, trimmed, for base and window alike. Every leaf is still processed in full by Varve.JsonLd; the importer then keeps only the properties S2–S4 read: the version's identity, the version key, listed, deprecation (reasons, alternate package and range) and vulnerabilities (advisory URL, severity). Record it as an ADR 0010 amendment, naming exactly what is kept and what is dropped (dependency groups and the rest), and saying the store holds the catalog's advisory state, not the whole catalog. The oracle reads the same fields. Spec and README say it in one sentence.

2. Project before you run. From a 1% random sample of base leaves (seeded, the seed recorded), measure quads per version under the trimmed model and the import rate with base commits of one package each, and extrapolate the base's quads, its load time, and S1's total. The snapshot download counts toward S1: only image pulls are excluded. The base's snapshot files may hold each leaf pruned to the fields the model keeps, by a rule recorded in the manifest; window leaves stay full. Report the projection and the snapshot's size before the full run. If the projected S1 total misses 3 minutes, stop and report: the choice between a smaller N and a longer target is Emil's.

3. Memory: the readings show the data is safe on disk, but not whether Varve holds memory or the runtime hasn't collected it. Rerun the window import with the server container limited to 512 MB (compose `mem_limit` in a CI-only override, not in the viewer's compose). If it completes, record "imports under 512 MB" as a measured fact on the evidence path and no Varve issue is needed. If it is OOM-killed, draft a Varve issue with the three readings and the limit as the reproduction, and propose checkpointing during long imports for memory as an ADR 0004 amendment.

4. CI rebuilds the snapshot only when snapshot/ or the builder changes, on a weekly schedule and on workflow_dispatch; every other run downloads the published asset and verifies it by hash, cached by that hash. A scheduled rebuild that disagrees with the committed manifest is a red run. Report the asset's size; GitHub release assets are limited to 2 GB each, so split if needed and record the parts in the manifest.

5. `import --since 2019-09-23`: measure it under the trimmed model, in the background as planned, and record the time. Don't run it in the step checks.

Then carry on as planned. Report back with the projection, the snapshot size, the memory-limit result, and anything you disagree with.
`````

`````markdown
Rulings on your report. Varve issues filed from your drafts: Hafeok/Varve#227 (memory under a limit, checkpoint not releasing it, reopen) and Hafeok/Varve#228 (VALUES not driving join order). Link them where your drafts were cited, and remove the drafts' "to file" status in the traceability record.

1. ImportUnderAMemoryLimit stays green: it tests a safety property (a clean refusal under memory pressure, nothing acknowledged lost), and that is evidence. Name and document it as that property. The limitation goes on the evidence page's "not yet proven" list with #227. The README states the memory S1 needs as measured (the server's peak, plus the other containers), and that Docker's default allowance on some machines is below it.

2. vd:packageId: keep it, and record it in ADR 0010's amendment with the measured reason.

3. S2's UNION of constants: keep it. deps.rq's comment and the query as printed on screen link #228. ADR 0010 (or the spec's S2) states that when Varve fixes it, the query returns to VALUES in the bump that adopts the fix.

4, 5 and 6: accepted as you did them.

7. compose.yaml pins the importer at a pull request's tag. That is acceptable for this PR as an interim pin, stated in the README. Amend ADR 0011 to decide how compose.yaml on main moves to the image main publishes. Preferred: the publish job on main opens a small pull request that re-pins the digest. Implement it if it fits in this PR; otherwise file the issue in varve-demo and link it.

8. Varve 0.1.0-preview.16 is out. Do not bump in this PR; the next session does.

Emil merges #4 next. Then rebase #6 onto main, re-run CI, and report back with the green run's S1 and S2 numbers and anything you disagree with. Emil then accepts #6's ADRs and merges it; snapshot-release and making the importer package public follow.
`````

## Report

Slice 1a, finished on the maintainer's rulings above.

### Results, from CI

[Run 41652404366](https://github.com/Hafeok/varve-demo/actions/runs/41652404366),
on `c886d5f`, the measurements in its `step-measurements` artifact:

| | Measured | Target |
|---|---|---|
| S1, `docker compose up` to the first answer | 102.7 s | ≤ 180 s |
| S1, base | 62.8 s: 3 000 commits, 3 018 866 quads | |
| S1, window | 12.1 s: 1 731 commits, 36 128 quads, 143.4 commits/s | |
| S1, again through the installed tool | base 54.8 s, window 6.8 s | |
| S2, sample lock file | 81 ms | ≤ 2 s |
| S2, generated lock file | 70 ms | ≤ 2 s |
| S2 against the oracle | agree, field by field, on both | |

In that run the snapshot was rebuilt (this pull request changes
`snapshot/`), so S1 mounted it. A run that does not rebuild downloads the
22.5 MB asset inside S1's time; locally that added under a second.

**The snapshot**, `edb36caa…`:

| | |
|---|---|
| base | 3 000 packages, 512 390 versions (46 deleted before T0), 3 018 866 quads |
| window | 1 731 catalog commits (27 138 skipped), 5 255 leaves, 36 128 quads |
| files | 661 MB on disk, 22.5 MB packed |
| rebuilt from nuget.org | 24 min 50 s locally; CI's rebuild matched the manifest |

**Memory.** The S1 check, in CI:

- 49 MB at start;
- 343 MB at the end of the import, before the final checkpoint;
- 963 MB after it;
- 125 MB after a restart.

Under 512 MB the import is refused at base commit 1 068 (`503`), the dataset
does not reopen under the limit within 60 s, and without the limit it
reopens with all 1 067 acknowledged commits. ADR 0004's amendment has the
rest. A checkpoint during the import was measured not to help, so none is
proposed.

**The deep history**, `import --since 2019-09-23`: 63.7 min in all. That is
about 33 min to build it from nuget.org, then 237 766 commits and 13.6M quads
replayed in 28.7 min, and a 122 s checkpoint. The server peaked at 4.5 GB.

### ADRs and amendments, all unaccepted

- **New:**
  - 0010, the graph model and vocabulary, with the advisory-state amendment;
  - 0011, our container images;
  - 0012, the demo tool.
- **Amended:**
  - 0004: the checkpoint key superseded, the base at T0, memory;
  - 0006: contexts are inline, and a remote one fails;
  - 0007: least privilege, the checkpointer client, the operator's key
    superseded;
  - 0008: the window, the base, the projection, the deep history, CI's
    rebuild policy.
- **Keys superseded:** six accepted keys moved out of their sets under new
  ones, each kept in its set's notes.

### Where this departs from a ruling, or adds to one

1. **Memory checkpoints.** Ruling 3 of the second message asked for
   checkpoints during long imports if the import failed under 512 MB. It
   failed, but by refusal, not OOM kill. Checkpoints every 250 commits were
   measured not to change where it fails, so they are not proposed. The
   importer keeps the option, off by default, so the measurement can be
   repeated.
2. **The memory check passes while the import is refused** (accepted by the
   maintainer, and renamed for the property it tests:
   `UnderMemoryPressureAWriteIsRefusedCleanlyAndNothingAcknowledgedIsLost`). It
   records the refusal and fails only if an acknowledged commit is lost. A
   red check for a Varve limit the demo shows would block every pull request.
   The maintainer may prefer it red.
3. **`isPrerelease` is not kept.** The ruling's list of kept properties does
   not name it. The query derives prerelease from the version key, and the
   oracle from `NuGet.Versioning`.
4. **`vd:packageId`, a second derived term.** Without it S2 took 14 s to
   27 s on the full base. It is identity, not a flag (ADR 0010's amendment).
5. **S2's query is asked per package with constants**, joined by `UNION`,
   not with `VALUES`. With `VALUES`, Varve's planner starts from the wrong
   pattern (Varve issue draft 2). This shapes the query for the engine; it
   is said in `deps.rq` and here.
6. **S2's "outside the subset"** also covers a version of one of the N
   packages that the catalog had not published by the snapshot's upper
   bound, with that reason stated. There is nothing else to say about such a
   version.

### How the window's bound was found

1. Every page of `https://api.nuget.org/v3/catalog0/index.json` (23 462 on
   2026-11-03) was read, and every item whose id is among the top 3 000 kept:
   890 024 leaves.
2. Leaves were fetched and checked for a `deprecation` and a
   `vulnerabilities` member:
   - every subset leaf from 2019-01-01 to 2019-10-02, and from 2020-04-01 to
     2020-07-18;
   - 150 a month, with a fixed seed, from 2018-01 to 2022-12.
3. The first of each is recorded, with its page, commit and leaf, in ADR
   0008's amendment. The exploration ran as scripts in the session's
   scratch space, not in this repository. Its results are checkable by
   fetching the leaves named there.
4. The import rate was measured on a two-week trial. The same build, run
   again, gave the same hash, `1eb32c85…`.

### Choosing the sample lock file

Picked by SPARQL against the loaded snapshot, as the viewer:

```sparql
PREFIX nuget: <http://schema.nuget.org/schema#>
SELECT ?id ?v (GROUP_CONCAT(?r) AS ?reasons) WHERE {
  GRAPH ?g { ?l nuget:deprecation ?d ; nuget:id ?id ; nuget:version ?v ; nuget:isPrerelease false .
             OPTIONAL { ?d nuget:reasons ?r } } } GROUP BY ?id ?v ORDER BY ?id ?v
```

This gives AutoMapper 15.1.1 and 15.1.2, MySqlConnector 1.3.0 to 1.3.9 and
2.1.3. The same query on `nuget:vulnerability` gives MySqlConnector and
SixLabors.ImageSharp. A query for clean, listed release versions of widely
used packages gives Microsoft.Extensions.DependencyInjection 10.0.14,
Microsoft.Extensions.Logging 10.0.14, System.Text.Json 10.0.14 and Polly
8.9.0. Newtonsoft.Json 13.0.1 is in the top 3 000 with no leaf in the
window. Varve.JsonLd 0.1.0-preview.15 is not in the top 3 000.

### Varve issues, filed from this session's drafts

Filed by the maintainer on 2026-11-06 as
[Hafeok/Varve#227](https://github.com/Hafeok/Varve/issues/227) and
[Hafeok/Varve#228](https://github.com/Hafeok/Varve/issues/228); the drafts
are kept below as they were filed. Neither links Varve#222, which
measures about 36 bytes a commit and is a different thing (the maintainer's
ruling 3).

#### 1. Filed as Hafeok/Varve#227: under a memory limit the server refuses writes, and cannot reopen the dataset under the same limit; a checkpoint does not release memory

**Version.** Varve `0.1.0-preview.15`, image
`ghcr.io/hafeok/varve:v0.1.0-preview.15@sha256:f3c1651a…d706`, one `File`
dataset, OIDC.

**Reproduction.** In `Hafeok/varve-demo` at this pull request's head:

```bash
printf 'services:\n  varve:\n    mem_limit: 512m\n    memswap_limit: 512m\n' > mem512.yaml
docker compose -f compose.yaml -f mem512.yaml up -d mock-oauth2 varve setup
varve-demo import --snapshot <snapshot dir>
```

That is 3 000 commits of about 1 000 quads each (the base), then 1 731 small
ones; the dataset starts empty.

**What happens.**

- **The write is refused.** At commit 1 068, after about 1.41M quads, the
  server answers `503 unavailable`: "The default projection failed at
  position 1067 and must be rebuilt: Insufficient memory to continue the
  execution of the program." Resident memory peaked at 426 MB. The container
  is not OOM-killed.
- **The same with whole catalog leaves.** About 1 000 quads a commit: commit
  607 is refused with `400 operation-failed`, "Operation 29 (DROP) failed:
  Insufficient memory…".
- **Checkpoints do not help.** A checkpoint every 250 commits
  (`POST /manage/datasets/nuget/checkpoints`) changes nothing: the same
  commit is refused, with resident memory between 380 MB and 425 MB
  throughout.
- **The dataset does not reopen under the same limit.** After a restart its
  state cycles between `closed` and not ready for at least 2 minutes, with
  resident memory 270 MB to 427 MB.
- **Without the limit it reopens.** In about 10 s, at 680 MB to 705 MB, with
  every one of the 1 067 acknowledged commits present. Nothing acknowledged
  was lost in any run.

Without a limit, the same import (S1, about 3.05M quads in 4 731 commits)
takes resident memory to:

- 45 MB at start;
- 576 MB at the end of the import;
- 1.10 GB after the final checkpoint;
- 133 MB after a restart.

**Expected.** Either a bound a server can be configured to stay within while
it writes and reopens, or documentation of the memory a dataset needs,
for writing and for reopening, in terms of what is above its newest
checkpoint. Also: a checkpoint that releases the memory it makes
unnecessary.

**Why it matters to the demo.** S1 on a viewer's machine needs more than
1 GB for the server alone. The demo records this as a measured fact
(in CI, as the safety property
`UnderMemoryPressureAWriteIsRefusedCleanlyAndNothingAcknowledgedIsLost`);
it does not work around it.

#### 2. Filed as Hafeok/Varve#228: a `VALUES` binding does not drive an index lookup; a constant-object pattern is chosen first instead

**Version.** Varve `0.1.0-preview.15`, the dataset of S1 (515 772 graphs;
each version's leaf has `<urn:varve-demo:term:packageId>` and
`nuget:listed`).

**Reproduction** (as the viewer, `POST /datasets/nuget/sparql`):

```sparql
PREFIX nuget: <http://schema.nuget.org/schema#>
PREFIX vd: <urn:varve-demo:term:>
SELECT (COUNT(*) AS ?n) WHERE {
  VALUES ?id { "mysqlconnector" "newtonsoft.json" }
  GRAPH ?c { ?l vd:packageId ?id ; nuget:listed true }
}
```

| Pattern | Time |
|---|---|
| the query above | 4.38 s (215 rows) |
| one `VALUES` row | 1.94 s |
| the same pattern with the id as a constant | 2.7 ms |
| `VALUES ?id { … }` with `?l vd:packageId ?id` alone | 2.4 ms |
| `nuget:listed ?listed` with `FILTER(?listed)` in place of the constant | 8.7 s |

The full S2 query took 14 s to 27 s with `VALUES`, against 22 ms to 154 ms
with constants. The planner appears to start from the pattern with a
constant object (`nuget:listed true`, about 516 000 matches) rather than the
one a `VALUES` binding makes selective. The demo writes its query with
constants, per package, and says why in `deps.rq`.

**Expected.** A `VALUES` binding used as a bound term when it orders the
join, or a statistics-based choice that sees it is selective.

**Not a Varve issue: the oracle.** It is the demo's own and shares no code
with Varve.

### After the third rulings (2026-11-06)

- **Varve issues linked.** #227 is linked from:
  - ADR 0004's amendment;
  - the spec's list of Varve issues and its "not yet proven" seed for the
    evidence page;
  - the README.

  #228 is linked from `deps.rq` (so from the query as printed), ADR 0010's
  amendment and the spec's S2.
- **The memory check is renamed** for the safety property it tests.
- **The README states S1's memory, measured with `docker top` during a local
  run:**
  - the server about 1.0 GB (963 MB in CI);
  - the importer 388 MB;
  - the issuer 201 MB;
  - about 1.6 GB resident in all, and about 3.2 GB with the page cache
    Docker counts.
- **ADR 0010's amendment** records `vd:packageId` with its measured reason,
  and that the query returns to `VALUES` in the bump that adopts #228's fix
  (ruling `QueryReturnsToValuesWhenVarveFixes228`).
- **ADR 0011's amendment of 2026-11-06:** every publish from `main` opens a
  pull request re-pinning `compose.yaml`. Implemented as the `repin` job. It
  needs the repository setting "Allow GitHub Actions to create and approve
  pull requests", and its pull requests carry no CI run of their own, since
  GitHub does not start workflows for the workflow token's pull requests.
  The README states the interim pin.
- **Not bumped to `0.1.0-preview.16`**, as ruled.
