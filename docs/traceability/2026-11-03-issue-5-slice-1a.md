# Session 03 — slice 1a: snapshot, importer, S1, S2 (#5)

- **Issue:** [#5, Slice 1a: snapshot, importer, S1, S2](https://github.com/Hafeok/varve-demo/issues/5)
- **Pull request:** [#6](https://github.com/Hafeok/varve-demo/pull/6), based on
  the bump's pull request [#4](https://github.com/Hafeok/varve-demo/pull/4)
- **Date:** 2026-11-03
- **Tool:** Claude Code 2.1.317, in a cloud session
- **Model:** `claude-opus-5-5`, as the session's metadata reports it
- **Branch:** `claude/hopeful-ramanujan-v3a1ex-slice-1a`. The session was given
  one branch; the maintainer chose this second one for the slice when asked.

## The prompt

The session's prompt is recorded verbatim in the bump's record,
[`2026-11-03-issue-3-bump-varve-to-preview-15.md`](2026-11-03-issue-3-bump-varve-to-preview-15.md),
since one session carried both pull requests. It is not repeated here.

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

None filed; none needed by this slice, beyond those already filed:

- **Varve#222 (resident memory grows per commit)** is the one Varve
  behaviour this slice measured against. The server's resident memory rose
  from about 45 MB to about 770 MB over the 1 731-commit import, locally
  and in CI. That is consistent with #222. Nothing here works around it, and
  the pull request links it.
