# varve-demo

A runnable demonstration of [Varve](https://github.com/Hafeok/Varve), a
.NET-native, event-sourced RDF/SPARQL store, built to answer one objection:
*"why trust a database that isn't battle-tested?"*

The answer is evidence you can check without trusting us. In Varve the only
durable truth is an append-only log with a frozen format and a hash chain, and
every index is a projection rebuilt from it. So the demo does not ask you to
believe that. It crashes the server with `kill -9` in the middle of a write
loop, again and again, and shows every acknowledged commit still there. It
runs the W3C suites and an Oxigraph differential against the released server,
and publishes the numbers from CI. You can rerun every one of them.

Then it shows what that design buys, on a dataset that is itself an
append-only log, the NuGet catalog:
- time travel and diffs between positions;
- a live change feed;
- the same engine answering the same query in the browser, offline;
- the database as files you can clone.

## Status: S1 and S2 are built, not yet passing

S1 and S2 are built, and their checks pass on a local run. They become
`passing` when CI runs them end to end against a released Varve. That waits
on the importer image being published to GHCR, which `compose.yaml` pins by
digest; `compose.yaml` lands with that digest. Every other step is
`pending`.

## Run it

You need Docker. From a clone of this repository:

```bash
docker compose up
```

That starts four services and stops one of them when it is done:

| Service | What it does | On your machine |
|---|---|---|
| `mock-oauth2` | the OIDC issuer, a **mock** that signs a token for anyone who asks | `localhost:8090` |
| `varve` | the released Varve server, OIDC only; the dataset is on the volume `varve-data` | `localhost:8080` |
| `setup` | applies [`deploy/nuget.trig`](deploy/nuget.trig) (the dataset and its grants) as the bootstrap operator, once, then exits | |
| `importer` | replays the pinned snapshot over HTTP, one Varve commit per catalog commit, prints the rate, takes the final checkpoint, prints the first answer, then exits | |

Then ask S2's question of a lock file with the demo's tool:

```bash
dotnet run --project src/VarveDemo.Tool -- deps --lock samples/packages.lock.json
```

`varve-demo` ([ADR 0012](docs/adr/0012-the-demo-tool.md)) is a .NET tool. It
is not on nuget.org yet: it is published there from the demo's first
release. Until then, run it from a clone as above, or pack it and install it
from a local folder, as CI does.

**What each client may do.** Each long-running client holds only what it
needs ([ADR 0007](docs/adr/0007-local-authentication-with-a-mock-oidc-issuer.md)
and its amendment):

- `varve-demo-importer` writes the dataset;
- `varve-demo-viewer` reads it;
- `varve-demo-checkpointer` is the dataset's admin, for the checkpoint, and
  nothing more;
- `varve-demo-operator` creates the dataset and is used once, by `setup`.

The importer process holds the importer's and the checkpointer's
credentials. With the mock issuer these are client ids with no real secret.

**What history is loaded.** The snapshot covers the top 3 000 packages by
downloads, pinned by hash ([`snapshot/manifest.json`](snapshot/manifest.json)),
in two parts ([ADR 0008](docs/adr/0008-the-dataset-and-its-mapping.md),
amendment of 2026-11-03):

- **the base**: every version of those packages as the catalog stood on
  **2026-10-20**, loaded one Varve commit per package;
- **the window**: every catalog commit from 2026-10-20 to 2026-11-03,
  replayed one Varve commit each, as the catalog made them.

History before 2026-10-20 is not replayed, and time travel starts there.

```bash
varve-demo import --since 2019-09-23
```

That replays the catalog from any date, with no base. From the first
deprecation data, on 2019-09-23, it took 63.7 minutes on a cloud machine:
33 minutes to build that history from nuget.org, then 237 766 catalog
commits replayed in 29 minutes. The server peaked at 4.5 GB of memory.

**What is stored.** Varve holds the catalog's advisory state of each
version: its id, version, listing, deprecation and vulnerabilities, in the
catalog's own terms. It does not hold the whole catalog; dependency groups
and the rest of each leaf are read and dropped
([ADR 0010](docs/adr/0010-the-graph-model-and-vocabulary.md), amendment of
2026-11-03).

**Names.** The catalog's JSON-LD is read by Varve's own reader,
`Varve.JsonLd`, with the catalog's own terms. The few names the demo adds
are in `urn:varve-demo:`, which is not a registered URN namespace: they name
things in this dataset and resolve nowhere
([ADR 0010](docs/adr/0010-the-graph-model-and-vocabulary.md)).

**Images.** Every image is pulled from `ghcr.io` by tag and digest. The
importer's image, `ghcr.io/hafeok/varve-demo-importer`, is built by this
repository's CI. Its base is the chiseled .NET `runtime-deps` image from MCR,
the same exception Varve's own image makes
([ADR 0011](docs/adr/0011-our-container-images.md)). A clean machine can pull
it only once the package is public on GHCR.

## The script

Targets are proposals until the maintainer accepts the specification
([`docs/spec/script.md`](docs/spec/script.md)). Each step's check is the CI
test that runs it end to end.

<!-- spec-table:begin -->
| Step | Title | Target | Check | State |
|---|---|---|---|---|
| S1 | Zero to query | ≤ 3 min from `docker compose up` to the first answer, image pulls excluded | S1_ZeroToQuery | pending |
| S2 | Deprecated and vulnerable dependencies | ≤ 2 s per query | S2_DeprecatedAndVulnerableDependencies | pending |
| S3 | Time travel and diff | ≤ 2 s per as-of query or diff | S3_TimeTravelAndDiff | pending |
| S4 | Live change feed | ≤ 5 s from a commit landing to the subscriber printing it | S4_LiveChangeFeed | pending |
| S5 | The same log in the browser | set by ADR 0005's measurement | S5_SameLogInTheBrowser | pending |
| S6 | Crash, restart, verify | ≤ 30 s per crash cycle, at least 50 cycles per CI run | S6_CrashRestartVerify | pending |
| S7 | The database is files you can clone | ≤ 1 min from `git clone` to an answer | S7_DatabaseIsFilesYouCanClone | pending |
| E | The evidence page | regenerated by every CI run on `main` and every Varve bump, in ≤ 30 min of CI | E_EvidencePage | pending |
<!-- spec-table:end -->

## The rules

- **No faking.** Every number and answer comes from a real run, in front of you
  or in a linked CI run. A replay says it is a replay.
- **Released Varve only.** Packages from nuget.org and the image
  `ghcr.io/hafeok/varve`, pinned by version and digest, its attestation verified
  before use. Never a fork, a patch or a build of Varve.
- **Public API only.** Varve's HTTP protocol, client and reader packages, and
  its documented command line.
- **Always authenticated.** OIDC bearer tokens on every request, never an
  anonymous mode. Locally the issuer is
  [mock-oauth2-server](https://github.com/navikt/mock-oauth2-server), a
  **mock**: it signs a token for anyone who asks. Pointing the demo at a real
  issuer means setting the authority and audience, creating the importer,
  viewer and operator clients there, and applying the same grants
  ([ADR 0007](docs/adr/0007-local-authentication-with-a-mock-oidc-issuer.md)).
- **Reproducible.** Every step runs from a snapshot pinned by hash. The one
  exception is the live tail of the change feed; offline, it replays and says
  so.
- **Gaps are shown.** Where Varve lacks something a step needs, the step shows
  the gap and links the Varve issue.
- **Images come only from `ghcr.io` and `public.ecr.aws`**, pinned by digest.

## Build order

Small vertical slices, each ending in steps that run end to end:

1. **Slice 1**: S1, S2, S6, and the browser measurement of
   [ADR 0005](docs/adr/0005-the-browser-step-is-gated-by-a-measurement.md).
2. **Slice 2**: S3 and S4.
3. **Slice 3**: S7 and the evidence page E.
4. **Slice 4**: S5, if the measurement keeps it in the script.

## Decisions

The [brief](docs/brief.md) is the authority. The decisions that implement it
are ADRs ([index](docs/adr/README.md)), each with a decision set in
[`docs/decisions/`](docs/decisions/):

- [0001 Record architecture decisions](docs/adr/0001-record-architecture-decisions.md)
- [0002 Consume Varve only as released](docs/adr/0002-consume-varve-only-as-released.md)
- [0003 Conventions taken from Varve](docs/adr/0003-conventions-taken-from-varve.md)
- [0004 The time-travel clock](docs/adr/0004-the-time-travel-clock.md)
- [0005 The browser step is gated by a measurement](docs/adr/0005-the-browser-step-is-gated-by-a-measurement.md)
- [0006 JSON-LD ingestion through `Varve.JsonLd`](docs/adr/0006-json-ld-ingestion-through-varve-jsonld.md)
- [0007 Local authentication with a mock OIDC issuer](docs/adr/0007-local-authentication-with-a-mock-oidc-issuer.md)
- [0008 The dataset and its mapping](docs/adr/0008-the-dataset-and-its-mapping.md)
- [0009 S4's subscription path](docs/adr/0009-s4s-subscription-path.md)
- [0010 The graph model and vocabulary](docs/adr/0010-the-graph-model-and-vocabulary.md) (proposed)
- [0011 Our container images](docs/adr/0011-our-container-images.md) (proposed)
- [0012 The demo tool](docs/adr/0012-the-demo-tool.md) (proposed)

How the work is done is in [AGENTS.md](AGENTS.md).

## Build

You need the .NET SDK that [`global.json`](global.json) names (10.0.401, or a
later 10.0 feature band).

```bash
dotnet build VarveDemo.slnx -c Release
dotnet test --solution VarveDemo.slnx -c Release
```

The tests are the repository's gates. They check that this README's table
matches the specification, that every dependency cites an ADR, that the ADRs
and their decision sets agree, the licence headers, that no configuration
uses anonymous mode, and that every image is pinned by digest from an allowed
registry.

## Licence

[MPL-2.0](LICENSE). See [NOTICE](NOTICE).

Developed with AI assistance under human review.
