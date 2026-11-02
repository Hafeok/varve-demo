---
set: conventions-taken-from-varve
namespace: varvedemo
adr: 0003
decisions:
  - key: SdkPinnedAsVarvePinsIt
    statement: "global.json pins the SDK as Varve does with rollForward latestFeature, and every project targets net10.0"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ShippedCodeIsAotCompatible
    statement: "Every project that is not a test project is AOT-compatible with the trim, AOT and single-file analyzers on and IL diagnostics as errors"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: MplTwoZero
    statement: "LICENSE is the MPL-2.0 text, NOTICE names Emil Okkels Klein, and every .cs file starts with the Exhibit A notice"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: StrictBuildSettings
    statement: "Warnings are errors, AnalysisLevel latest-recommended, EnforceCodeStyleInBuild, nullable on, implicit usings off, deterministic builds"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: CentralPackagesWithAdr
    statement: "Packages are centrally managed, every PackageVersion carries Adr, and no project file pins a version"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: AnalyzersBuildTimeOnly
    statement: "DecisionDriven.Analyzers is build-time only with ArchFamily VarveDemo and the ledger in docs/decisions"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: NamesStartWithVarveDemo
    statement: "Our names start with VarveDemo and never with Varve."
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ImagesGhcrAndEcrPublicByDigest
    statement: "Images are published to GHCR only and pulled only from ghcr.io or public.ecr.aws, pinned by digest"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: GatesAreTests
    statement: "Tests use xunit.v3 on Microsoft Testing Platform with TRX reports, and the repository gates are tests"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ActionsPinnedBySha
    statement: "GitHub Actions are pinned by full commit SHA at the SHAs Varve pins, admitting actions/checkout, actions/setup-dotnet and actions/upload-artifact"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: IssueFirstCommitsAndRecords
    statement: "Every commit is conventional and references its issue, and every AI-assisted session files a traceability record in docs/traceability"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: DeferredUntilReleases
    statement: "DCO sign-off, the agent-review gate and release descriptors are not adopted until the demo has releases"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
---

The rulings of [ADR 0003](../adr/0003-conventions-taken-from-varve.md), filed by session 01 of #1 on
2026-10-23 and accepted by the maintainer on 2026-10-26, on the foundation's
pull request #2. Every ruling carries `accepted-by` and `accepted-at`.
