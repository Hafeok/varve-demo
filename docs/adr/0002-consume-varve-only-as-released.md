# 0002 — Consume Varve only as released

## Status

**Proposed — filed unaccepted by session 01 of #1, 2026-10-23.** Acceptance is
the maintainer's act on the pull request. Its decision set is
[`consume-varve-only-as-released`](../decisions/consume-varve-only-as-released.md).

## Context

The brief: the demo "consumes Varve only as published packages and container
images from the decision-driven-design feed. It never forks, vendors or patches
Varve", and "nothing demo-specific goes into Varve". The argument the demo makes
is that *the thing you can download* deserves trust. A demo built against a
branch, a local build or a patched copy proves something about software nobody
else can get.

What "the decision-driven-design feed" is, read on 2026-10-23: there is no
separate feed. Varve's packages are on nuget.org, and their nuspec `authors`
field is `decision-driven-design` (for example `Varve.Protocol.Client` and
`Varve.JsonLd` at `0.1.0-preview.11`). Varve's own `nuget.config` has
nuget.org as its one source. The server image is published by Varve's
`publish.yml` to `ghcr.io/hafeok/varve`, tagged with the release tag
(`v0.1.0-preview.11`; no `latest` for a prerelease), and attested with build
provenance and an SPDX SBOM, which the same workflow verifies with
`gh attestation verify --owner hafeok`.

The latest release on 2026-10-23 is `0.1.0-preview.11` (nuget.org, published
2026-10-21). Its image index is
`ghcr.io/hafeok/varve:v0.1.0-preview.11@sha256:4325d1eb29014f31e411038ddb10a11c5913964ae793329d2aee7e815824d7f4`.

## Decision

1. **Packages come from nuget.org only.** `nuget.config` clears every source,
   adds nuget.org, and maps `*` to it, so a second source added later cannot
   silently redirect a package id. A Varve package is one whose `authors` is
   `decision-driven-design`.
2. **The server is `ghcr.io/hafeok/varve`, pinned by tag and digest**, the tag
   being the release's. CI verifies the digest's attestation before any step
   uses the image: `gh attestation verify oci://ghcr.io/hafeok/varve@<digest>
   --owner hafeok`, for the SLSA provenance predicate. A failed verification
   fails the run.
3. **One pin.** `VarveVersion` in `Directory.Packages.props` is the Varve
   release, and every Varve `PackageVersion` uses it. The compose image's tag
   must equal `v$(VarveVersion)`; the gate
   `ConfigurationGates.Compose_image_tag_matches_the_varve_pin` holds them
   together, skipped with its reason until `compose.yaml` exists. The digest
   beside the tag is resolved from the registry with the bump.
4. **The pin is the latest release**, resolved from nuget.org and the registry
   when it is set, never from memory: `0.1.0-preview.11` today.
5. **A bump is one pull request.** It changes `VarveVersion`, the image tag and
   digest, reruns every check, re-checks the facts the specification rests on
   (`docs/spec/script.md`, "Facts about Varve"), reruns ADR 0005's measurement,
   and regenerates the evidence page. A bump that turns a step red is not
   merged by relaxing the step.
6. **A gap in Varve becomes a Varve issue with a reproduction**, linked from the
   step that shows the gap. The demo waits or shows the gap; it never works
   around it.

## Alternatives considered

- **A git submodule or a source build of Varve.** Rejected by the brief, and
  for the argument's sake: the viewer would be shown something they cannot
  download.
- **Floating versions** (`0.1.0-*`, the image's newest tag). Rejected: a step
  would change under a viewer with no commit here to say why, and C5
  (reproducible from a pinned snapshot) would not hold.
- **Pin by tag only.** Rejected: a tag can be moved, a digest cannot. The tag
  stays for the reader and the gate; the digest is what is pulled.
- **Pin each Varve package separately.** Rejected: Varve releases its packages
  together at one version, and two Varve versions in one process is a state
  nobody has tested.
- **Skip attestation verification**, trusting GHCR and the digest. Rejected:
  the digest proves the image has not changed since we pinned it, not that
  Varve's release workflow built it. The attestation is the evidence a viewer
  can check themselves, which is the brief's point.
- **Use Varve's private CI mirror** (`ghcr.io/hafeok/varve-ci/...`). Rejected:
  it is private, and a demo a viewer cannot reproduce is not evidence.

## Consequences

- The demo trails Varve by at least one release for anything it needs: a
  feature lands in Varve, ships, and only then appears here.
- CI needs `gh` and a token able to read attestations (`attestations: read`),
  which is the workflow's `GITHUB_TOKEN`.
- `nuget.config` makes adding any non-nuget.org package an ADR, deliberately.
- The register carries Varve packages before any project references them; the
  pin has one place from the first commit.
