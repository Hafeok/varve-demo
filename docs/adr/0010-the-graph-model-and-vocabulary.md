# 0010 — The graph model and vocabulary

## Status

**Proposed** — filed unaccepted by session 03 on #5, 2026-11-03. Its decision
set is [`the-graph-model-and-vocabulary`](../decisions/the-graph-model-and-vocabulary.md).

## Context

ADR 0008 fixes the shape of the dataset: one named graph per package version,
`<urn:varve-demo:pkg/{id}/{version}>`, replaced by each details leaf and
dropped by a delete leaf, and ADR 0004 adds `<urn:varve-demo:catalog-log>`.
ADR 0006 says the leaves are read by `Varve.JsonLd`. What none of them says
is which terms the quads use, what the demo adds of its own, and how S2 orders
versions to answer "what should I move to".

What the catalog is, read from it on 2026-11-03 (#5):

- Every leaf fetched while finding the window (30 406 of them, from 2018 to
  2026) carries its `@context` **inline**, as a JSON object. Four distinct
  contexts occur. None names a remote context document. ADR 0006 point 2
  expected remote contexts; the snapshot records every remote context a leaf
  names, and with today's catalog that list is empty.
- The inline context maps the leaf's members onto
  `http://schema.nuget.org/schema#` (by `@vocab`) and
  `http://schema.nuget.org/catalog#` (the `catalog:` prefix), and types the
  timestamps as `xsd:dateTime`. A details leaf carries `id`, `version`
  (normalised), `verbatimVersion`, `listed`, `isPrerelease`, `published`, its
  `dependencyGroups`, and, when the package owner or the advisory database
  set them, `deprecation` (`reasons`, `message`, `alternatePackage` with
  `id` and `range`) and `vulnerabilities` (`advisoryUrl`, `severity`,
  `gitHubDatabaseKey`).
- Most nodes in a leaf have an `@id`; some do not, and become blank nodes.

**Versions.** S2 suggests "the lowest listed version above it with no known
vulnerability". NuGet orders versions by SemVer 2.0 with its own
extensions: one to four numeric parts, a prerelease after its release's
numbers but below the release, labels compared one by one, a label of
digits numerically and below one with letters, letters case-insensitively,
fewer labels first when all shared ones are equal, and build metadata
ignored. SPARQL compares strings by code point and has no NuGet ordering, so
`"10.0.0" < "9.0.0"` as strings. A version must be ordered by something
SPARQL can compare.

## Decision

1. **The catalog's own terms.** A leaf's quads are what `Varve.JsonLd`
   produces from the leaf with its own `@context`: no renaming, no mapping,
   no term of ours in their place. A viewer's query uses
   `http://schema.nuget.org/schema#` and `http://schema.nuget.org/catalog#`
   as the catalog defines them, and so does every query in this repository.
2. **One leaf's quads, in its version's graph.** `Varve.JsonLd` emits a leaf
   into the default graph; the importer writes every quad into
   `<urn:varve-demo:pkg/{id}/{version}>` (ADR 0008). Blank node labels are
   made unique per leaf within an update (`_:l{n}_{label}`), so two leaves in
   one catalog commit never share a node.
3. **A namespace of our own, for what is derived: `urn:varve-demo:term:`.**
   It holds the demo's terms and nothing the catalog states. It is a `urn:`
   because the demo is not in a position to mint dereferenceable IRIs:
   `w3id.org` needs a registered redirect the demo has not asked for, and a
   term that resolves nowhere should not look as if it does. `varve-demo` is
   not a registered URN namespace (RFC 8141); the README says so, as it does
   for ADR 0008's graph names. The terms:
   - `<urn:varve-demo:term:versionKey>`: the sortable version key below, on
     the leaf's node, in its version's graph.
4. **The catalog log** (ADR 0004) uses the catalog's terms too: each retained
   catalog commit is a node `<urn:varve-demo:catalog-commit:{commitId}>` in
   `<urn:varve-demo:catalog-log>` with `catalog:commitId` (a plain string)
   and `catalog:commitTimeStamp` (an `xsd:dateTime`), the values the
   catalog page gives.
5. **The version key is computed by the importer, by NuGet's SemVer 2.0
   rules**, from the leaf's normalised `version`, and asserted as a plain
   string whose code-point order is NuGet's version order:
   - the four numeric parts, missing ones as 0, each as ten digits, joined
     by `.`;
   - then `~` for a release, or `-` and the labels for a prerelease, so a
     prerelease sorts below its release (`-` is below `~`);
   - each label as `0` and ten digits when it is an integer that fits a
     32-bit signed integer, otherwise `1` and the label in upper case, so a
     numeric label sorts below an alphanumeric one and letters compare
     without case;
   - labels joined by `!`, which is below every character a label may hold,
     so fewer labels sort first when the shared ones are equal;
   - build metadata dropped.

   S2 orders by this key with `ORDER BY` and compares with `>`.
6. **The independent check of the key.** The S2 oracle (`VarveDemo.Oracle`,
   test-only) orders versions with **`NuGet.Versioning`**, NuGet's own
   implementation, which this ADR admits as a test-only package. CI checks
   that the importer's key and `NuGet.Versioning`'s `VersionComparer.Default`
   agree on the order of **every version in the snapshot**, and the test
   fails on the first pair they disagree on. The key is ours; the order it
   must match is NuGet's.
7. **No other derived data.** Whether a version is deprecated, vulnerable,
   listed or a prerelease is read from the catalog's own terms by the query.
   A derived flag would put the demo's interpretation where a viewer expects
   the catalog's.

## Alternatives considered

- **Map the catalog onto a vocabulary of our own** (or schema.org). Rejected:
  the demo would be showing its own modelling, and a viewer comparing a quad
  with the leaf on nuget.org could not match them term for term.
- **Order versions in the client**, after the query. Rejected: S2's answer
  would then be part SPARQL and part demo code, and the step is meant to show
  the query answering the question.
- **Pad the version string in SPARQL** with `STRBEFORE`, `REPLACE` and
  friends. Rejected: NuGet's rules (four parts, numeric versus alphanumeric
  labels, case) make the expression long and hard to check, and it would run
  on every query instead of once per leaf.
- **A `w3id.org/varve-demo/` namespace.** Rejected for now: it needs a
  registration, and an unregistered `w3id.org` IRI would claim a resolution
  that does not exist. A later ADR can move to one; the key would change by a
  new snapshot.
- **Use `NuGet.Versioning` in the importer too.** Rejected: the oracle exists
  to be independent of the path it checks. If the importer and the oracle
  used one comparer, a bug in it would agree with itself.

## Consequences

- Every query in the repository spells the catalog's IRIs, and S2's query
  shows a viewer the catalog's own vocabulary.
- One derived quad per leaf. The manifest's quad count includes it.
- The version key is part of the snapshot's mapping: a change to it changes
  the quads, and so the tool version in the manifest.
- `NuGet.Versioning` enters as a test-only package, and the gate that the
  oracle references no Varve assembly also keeps it out of the importer.

## Amendment, 2026-11-03: the store holds the catalog's advisory state

Filed unaccepted by session 03, on #5, at the maintainer's ruling, when the
base (ADR 0008's amendment) made the whole-leaf model too large. The whole-leaf
model gave the window about 377 quads per version, mostly dependency groups
and package entries.
512 390 base versions are about 190 million quads, about 80 minutes at the
measured rate.

**One graph model, for base and window alike.** Every leaf is still read in
full by `Varve.JsonLd`, with its own context (point 1). Of the quads it
produces, the importer keeps these, and only these
(`src/VarveDemo.Importer/AdvisoryModel.cs`):

| Node | Kept |
|---|---|
| the leaf (the version) | `nuget:id`, `nuget:version`, `nuget:listed`, `nuget:deprecation`, `nuget:vulnerability` |
| its deprecation | `nuget:reasons`, `nuget:alternatePackage` |
| the alternate package | `nuget:id`, `nuget:range` |
| each vulnerability | `nuget:advisoryUrl`, `nuget:severity` |

The importer adds the version key (point 5) and the package id lower-cased
(below). **Everything else is dropped**:
dependency groups and their dependencies, package entries, `rdf:type`,
`isPrerelease` (the version key says it), descriptions, summaries, authors,
owners, tags, icons, URLs, licences, release notes, sizes, hashes,
`published`, `created`, `lastEdited`, and the leaf's own `catalog:commitId`
and `catalog:commitTimeStamp`.

**What this means.** The store holds the catalog's **advisory state** of each
version, not the whole catalog: what S2 to S4 read, in the catalog's own
terms. Point 1 still holds for what is kept: no term is renamed, and nothing
of ours replaces a catalog term. A viewer who wants a dropped property reads
the leaf, whose URL is the version's node.

**The snapshot's base files** hold each base leaf pruned to the members this
model reads, by the rule the manifest records (`base.leafRule`): `@context`,
`@id`, `id`, `version`, `listed`, `deprecation`, `vulnerabilities`. Window
leaves stay whole. Either way the importer reads the leaf with
`Varve.JsonLd` and keeps the quads above, so a pruned leaf and a whole one of
the same version give the same quads.

**The oracle reads the same fields**, from the snapshot's JSON: `id`,
`version`, `listed`, `deprecation` (`reasons`, `alternatePackage.id`,
`alternatePackage.range`) and `vulnerabilities` (`advisoryUrl`,
`severity`). It takes prerelease from `NuGet.Versioning`, and the query
takes it from the version key.

**The base in the catalog log.** Each base commit asserts
`<urn:varve-demo:base:{id}>` in `<urn:varve-demo:catalog-log>`, with
`<urn:varve-demo:term:basePackage>` (the id) and
`<urn:varve-demo:term:baseAsOf>` (T0 as `xsd:dateTime`). These are two more
derived terms in point 3's namespace.

**The package id, lower-cased: `<urn:varve-demo:term:packageId>`.** With the
base loaded, S2's search for "the lowest clean version above it" must find a
package's versions among 515 772 version graphs. The catalog's `nuget:id`
keeps each version's own casing, which varies between versions of one
package, so no exact match finds them all. A filter on the graph name scans
every version: S2 took 14 s to 27 s, past its 2 s target and up to Varve's
read limit. The importer therefore asserts, on each leaf, the id lower-cased,
and the query finds a package's versions by that literal. It is identity,
like the graph name it mirrors, not a flag on the catalog's state, so point 7
stands. With it, S2's query answers in 117 ms to 154 ms on the full base.

Its rulings are `LeafProjectedToWhatTheStepsRead`, `BaseStateInTheCatalogLog`
and `PackageIdForLookup`, filed unaccepted.
