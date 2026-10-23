---
set: consume-varve-only-as-released
namespace: varvedemo
adr: 0002
decisions:
  - key: PackagesFromNugetOrgOnly
    statement: "Packages come from nuget.org only, through nuget.config with clear and source mapping; a Varve package is one whose authors is decision-driven-design"
  - key: ServerImageByTagAndDigest
    statement: "The server is ghcr.io/hafeok/varve pinned by the release tag and its digest, and CI verifies its attestation with gh attestation verify before use"
  - key: OneVarvePin
    statement: "VarveVersion in Directory.Packages.props is the one Varve pin, every Varve PackageVersion uses it, and the compose image tag equals v$(VarveVersion)"
  - key: PinIsTheLatestRelease
    statement: "The pin is the latest Varve release, resolved from nuget.org and the registry, never from memory"
  - key: BumpIsOnePullRequest
    statement: "A bump to a new Varve release is one pull request that reruns every check, re-checks the spec facts, reruns the ADR 0005 measurement and regenerates the evidence page"
  - key: GapIsAVarveIssue
    statement: "A gap in Varve becomes a Varve issue with a reproduction, linked from the step that shows it, and is never worked around"
---

The rulings of [ADR 0002](../adr/0002-consume-varve-only-as-released.md), filed unaccepted by session 01 of
#1 on 2026-10-23. Every citation is `CS0618` until the maintainer accepts them by
adding `accepted-by` and `accepted-at`, which a session never writes.
