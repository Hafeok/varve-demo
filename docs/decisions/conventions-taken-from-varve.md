---
set: conventions-taken-from-varve
namespace: varvedemo
adr: 0003
decisions:
  - key: SdkPinnedAsVarvePinsIt
    statement: "global.json pins the SDK as Varve does with rollForward latestFeature, and every project targets net10.0"
  - key: ShippedCodeIsAotCompatible
    statement: "Every project that is not a test project is AOT-compatible with the trim, AOT and single-file analyzers on and IL diagnostics as errors"
  - key: MplTwoZero
    statement: "LICENSE is the MPL-2.0 text, NOTICE names Emil Okkels Klein, and every .cs file starts with the Exhibit A notice"
  - key: StrictBuildSettings
    statement: "Warnings are errors, AnalysisLevel latest-recommended, EnforceCodeStyleInBuild, nullable on, implicit usings off, deterministic builds"
  - key: CentralPackagesWithAdr
    statement: "Packages are centrally managed, every PackageVersion carries Adr, and no project file pins a version"
  - key: AnalyzersBuildTimeOnly
    statement: "DecisionDriven.Analyzers is build-time only with ArchFamily VarveDemo and the ledger in docs/decisions"
  - key: NamesStartWithVarveDemo
    statement: "Our names start with VarveDemo and never with Varve."
  - key: ImagesGhcrAndEcrPublicByDigest
    statement: "Images are published to GHCR only and pulled only from ghcr.io or public.ecr.aws, pinned by digest"
  - key: GatesAreTests
    statement: "Tests use xunit.v3 on Microsoft Testing Platform with TRX reports, and the repository gates are tests"
  - key: ActionsPinnedBySha
    statement: "GitHub Actions are pinned by full commit SHA at the SHAs Varve pins, admitting actions/checkout, actions/setup-dotnet and actions/upload-artifact"
  - key: IssueFirstCommitsAndRecords
    statement: "Every commit is conventional and references its issue, and every AI-assisted session files a traceability record in docs/traceability"
  - key: DeferredUntilReleases
    statement: "DCO sign-off, the agent-review gate and release descriptors are not adopted until the demo has releases"
---

The rulings of [ADR 0003](../adr/0003-conventions-taken-from-varve.md), filed unaccepted by session 01 of
#1 on 2026-10-23. Every citation is `CS0618` until the maintainer accepts them by
adding `accepted-by` and `accepted-at`, which a session never writes.
