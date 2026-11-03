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

## Report

**In progress.** This record is filed before CI has run the step checks:
the importer image's push to GHCR was refused (`denied`), and the
maintainer is changing the permission. CI's numbers and the final report
are added when the checks run. The local numbers are in ADR 0008's
amendment and in the pull request.

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

### Varve issue drafts

Drafts only: the planning chat files them. None links Varve#222, which
measures about 36 bytes a commit and is a different thing (the maintainer's
ruling 3).

#### 1. Under a memory limit the server refuses writes, and cannot reopen the dataset under the same limit; a checkpoint does not release memory

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
(`ImportUnderAMemoryLimit` in CI); it does not work around it.

#### 2. A `VALUES` binding does not drive an index lookup; a constant-object pattern is chosen first instead

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
