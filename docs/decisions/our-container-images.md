---
set: our-container-images
namespace: varvedemo
adr: 0011
decisions:
  - key: PublishedToGhcrPulledByCompose
    statement: "Our images are published to ghcr.io/hafeok/varve-demo-name and compose pulls them by tag and digest, never building them"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-11-06T13:17:23Z
  - key: ChiseledRuntimeDepsBaseByDigest
    statement: "A published image's base is mcr.microsoft.com/dotnet/runtime-deps 10.0-noble-chiseled pinned by digest, Varve's ADR 0111 exception, with a Native AOT binary run as the non-root app user"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-11-06T13:17:23Z
  - key: McrOnlyInOurImageDockerfiles
    statement: "The gate allows mcr.microsoft.com/dotnet/runtime-deps only in a FROM line of a Dockerfile under deploy/images, pinned by digest, and a test shows a stray reference elsewhere still fails"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-11-06T13:17:23Z
  - key: LinuxX64Only
    statement: "Our images are built for linux-x64 only until an amendment adds another architecture"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-11-06T13:17:23Z
  - key: PullRequestImageTaggedPrSha
    statement: "A pull request's CI builds the image from the published binary, pushes it as pr-sha, and runs the step checks against that tag by digest"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-11-06T13:17:23Z
  - key: PublishOnMainAttested
    statement: "A push to main publishes main-sha; every image CI pushes is attested with build provenance and verified before a step uses it, as ADR 0002 requires for Varve's"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-11-06T13:17:23Z
  - key: ComposePinMovesInItsOwnCommit
    statement: "compose.yaml names our image by tag and digest, and the digest moves in a commit of its own after an image is published"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-11-06T13:17:23Z
  - key: PublishOnMainOpensARepinPullRequest
    statement: "Every publish of an image from main opens a pull request that re-pins compose.yaml's digest to it; a person merges it, and a push changing only compose.yaml opens none"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-11-06T13:17:23Z
---

The rulings of [ADR 0011](../adr/0011-our-container-images.md), filed
unaccepted by session 03 on #5, 2026-11-03. Every citation is `CS0618` until
the maintainer accepts them.

`PublishOnMainOpensARepinPullRequest` is the 2026-11-06 amendment's ruling,
filed unaccepted by session 03 on #5.
