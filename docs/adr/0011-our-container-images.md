# 0011 — Our container images

## Status

**Proposed** — filed unaccepted by session 03 on #5, 2026-11-03. Its decision
set is [`our-container-images`](../decisions/our-container-images.md).

## Context

Slice 1a adds the first image of our own: the importer, which `compose.yaml`
starts after the server. Session 04 and later slices add more (the browser
page in slice 4). The brief and ADR 0003 point 8 say: we publish to GHCR
only; we pull only from `ghcr.io` and `public.ecr.aws`, pinned by digest.

A .NET image needs a base. Varve's own server image faces the same choice,
and its ADR 0111 point 3 makes one exception: the base is
`mcr.microsoft.com/dotnet/runtime-deps`, chiseled, "the one pull outside GHCR
and ECR Public", because MCR is the runtime's own distribution channel, has
no anonymous rate limit of the kind that bit its CI, and the chiseled image
has no shell and no package manager. Varve's image is a Native AOT binary
on that base, built per architecture, with a pull request's own build
pushed as `pr-<sha>` and the release attested with build provenance and an
SBOM.

The demo's hygiene gate
(`ConfigurationGates.Every_image_comes_from_an_allowed_registry_pinned_by_digest`)
refuses every image outside `ghcr.io/` and `public.ecr.aws/`, in every YAML
file and Dockerfile.

## Decision

1. **We publish our images to GHCR**, as `ghcr.io/hafeok/varve-demo-<name>`:
   in this slice, `ghcr.io/hafeok/varve-demo-importer`. `compose.yaml` pulls
   them from there by tag and digest, and never builds them.
2. **A published image's base is
   `mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled`, pinned by
   digest**, Varve's exception (its ADR 0111 point 3) for Varve's reasons.
   The image holds the Native AOT binary, runs as the non-root `app` user
   (uid 1654) on a read-only root filesystem, and writes only to a volume.
3. **MCR is pulled only by our image builds.** The gate allows
   `mcr.microsoft.com/dotnet/runtime-deps` only in a `FROM` line of a
   Dockerfile under `deploy/images/`, pinned by digest, and nowhere else: not
   in compose, not in a workflow's `image:`, not in a test. A test holds the
   gate to that, with a stray MCR reference outside those Dockerfiles that
   must still fail.
4. **linux-x64 only, for now.** The importer runs once per `docker compose up`
   on a viewer's machine and in CI; Varve's server is multi-architecture,
   and an arm64 importer is a later amendment when someone needs one.
5. **On a pull request, CI builds the image from the binary the build job
   published, tags it `pr-<sha>`, and pushes it**, as Varve does (its ADR 0111
   point 6). The step checks run against that tag, by the digest the push
   returned. A `pr-` tag is not a release and may be deleted.
6. **Publishing happens on `main`.** A push to `main` builds the image and
   tags it `main-<sha>`. **Every image CI pushes is attested**, a pull
   request's `pr-<sha>` included. Its build provenance is attested for the
   digest with `actions/attest-build-provenance`, which this ADR admits at the
   SHA Varve's `publish.yml` pins. Before a step uses the image, CI verifies it
   with
   `gh attestation verify oci://ghcr.io/hafeok/varve-demo-<name>@<digest> --owner hafeok`,
   as ADR 0002 requires for Varve's. The Varve image is verified the same way
   in the same job.
7. **`compose.yaml` names the image by tag and digest.** Its digest moves in
   a commit of its own after an image is published. Until the first
   publish from `main`, it names the `pr-<sha>` build of this slice's pull
   request, and says so in a comment.

## Alternatives considered

- **Build the importer in compose** (`build:`). Rejected: a viewer's
  `docker compose up` would then pull the .NET SDK image (from MCR) and
  compile, which costs most of S1's budget and puts MCR in the viewer's
  path.
- **Run the importer as a `dotnet tool` inside a GHCR-hosted SDK image.**
  Rejected: no SDK image is published on GHCR or ECR Public by Microsoft,
  and the tool would be restored from nuget.org at every start.
- **A base from `public.ecr.aws`** (a distroless or Ubuntu image there).
  Rejected for Varve's reason: a second vendor for the bytes the runtime's
  own channel ships, and a base that differs from Varve's own image.
- **`FROM scratch` with a statically linked binary.** Rejected for now:
  Native AOT on Linux links glibc dynamically by default, and the chiseled
  base is exactly the runtime dependencies, maintained by the runtime's
  owners.
- **Publish on every pull request as a release.** Rejected: a release is
  what `main` built; a `pr-` tag is a build of a proposal.

## Consequences

- The demo's images carry one pull from MCR, in our build only. A viewer
  pulls only from GHCR.
- The steps job needs `packages: write`, `attestations: write` and
  `id-token: write`. A pull request from a fork has none of them, so its
  steps job cannot push or attest, and fails at that step; the maintainer
  runs it from a branch of this repository instead.
- A new package on GHCR is private until the owner makes it public; the
  maintainer makes `varve-demo-importer` public once, and the README says
  that a clean machine needs it to be.
- The gate grows an allow-list of one name and one directory, with a test
  that the allowance is no wider.
