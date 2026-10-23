# 0003 — Conventions taken from Varve

## Status

**Proposed — filed unaccepted by session 01 of #1, 2026-10-23.** Acceptance is
the maintainer's act on the pull request. Its decision set is
[`conventions-taken-from-varve`](../decisions/conventions-taken-from-varve.md).

## Context

The brief: "Same conventions as Varve where they apply: current LTS .NET,
Native AOT-compatible host, MPL-2.0, DecisionDriven.Analyzers, ADRs for
decisions and new dependencies, and README prose that says only 'developed with
AI assistance under human review' without naming a product."

"Where they apply" needs a list. Varve is a library family with releases, a
trunk ruleset, a DCO gate, an agent-review gate and release descriptors. The
demo is one repository with no releases yet, consumed by people who run it, not
link it. Some conventions transfer as they are; some are about a published
package and have nothing to bind to yet. Taking a convention that has nothing
to act on is scaffolding, which both briefs forbid.

What Varve does, read from its repository at `v0.1.0-preview.11`: `global.json`
pins SDK `10.0.401` with `rollForward: latestFeature`; `Directory.Build.props`
sets `net10.0`, warnings as errors, `AnalysisLevel` `latest-recommended`,
`EnforceCodeStyleInBuild`, nullable on, implicit usings off and deterministic
builds; IL-prefixed diagnostics are errors in `.editorconfig`; packages are
centrally managed with `Adr="NNNN"`; tests are xunit.v3 on Microsoft Testing
Platform with TRX reports; every `.cs` file carries the MPL-2.0 Exhibit A
notice; every session files a traceability record.

## Decision

1. **SDK and target.** `global.json` pins the SDK as Varve does, `10.0.401`
   with `rollForward: latestFeature` and no prereleases, and the test runner is
   Microsoft Testing Platform. Every project targets `net10.0`, the current LTS.
2. **AOT.** Everything that ships is AOT-compatible: every project that is not a
   test project has `IsAotCompatible` and the trim, AOT and single-file
   analyzers on (`Directory.Build.targets`), and the IL-prefixed diagnostics are
   errors in `.editorconfig`, never in `NoWarn`. The demo's host and tools are
   published as Native AOT where the step allows.
3. **MPL-2.0.** `LICENSE` is the MPL-2.0 text, the same bytes as Varve's and as
   Mozilla publishes it; `NOTICE` names Emil Okkels Klein as the copyright
   holder; every `.cs` file starts with the Exhibit A notice. Gates:
   `LicenceGates.Licence_is_the_mpl_2_0_text`,
   `LicenceGates.Every_cs_file_starts_with_the_exhibit_a_notice`.
4. **Build settings.** Warnings are errors, `latest-recommended`,
   `EnforceCodeStyleInBuild`, nullable on, implicit usings off, deterministic
   builds. `.editorconfig` is Varve's with its Varve-only rules removed (the
   hot-path allow-list and the `VARVE` rule prefix).
5. **Central package management**, with `Adr="NNNN"` on every `PackageVersion`
   and no version in any project file. Gates:
   `DependencyGates.Every_package_version_cites_an_existing_adr`,
   `DependencyGates.No_project_pins_its_own_version`. This ADR admits the
   build- and test-time packages: `DecisionDriven.Analyzers`, `xunit.v3` and
   `Microsoft.Testing.Extensions.TrxReport`, each at its latest version on
   nuget.org when added.
6. **`DecisionDriven.Analyzers`** is build-time only (`PrivateAssets="all"`,
   analyzers and build assets only), with `ArchFamily` `VarveDemo` and the
   ledger in `docs/decisions` (`DdLedgerDirectory`).
7. **Names start with `VarveDemo`, never `Varve.`** A `Varve.` name is a claim
   to be part of Varve, and would collide with its namespaces in a viewer's
   head and in an IDE.
8. **Images.** We publish images to GHCR only. We pull images only from
   `ghcr.io` or `public.ecr.aws`, pinned by digest. Gate:
   `ConfigurationGates.Every_image_comes_from_an_allowed_registry_pinned_by_digest`.
9. **Tests.** xunit.v3 on Microsoft Testing Platform, with TRX reports uploaded
   by CI. The repository's gates are tests, in `tests/VarveDemo.Spec.Tests`, so
   `dotnet test` is the one command that says whether the tree is sound.
10. **GitHub Actions** are pinned by full commit SHA, at the SHAs Varve's
    workflows pin, with the version in a comment. This ADR admits
    `actions/checkout`, `actions/setup-dotnet` and `actions/upload-artifact`.
11. **Traceability.** Issue-first commits with `Refs #N` (a closing keyword
    only as a trailer line of its own), conventional commit form, and a
    traceability record per session in `docs/traceability/`, named
    `YYYY-MM-DD-issue-N-<slug>.md`, with the prompt, the tool and model named
    exactly, the report and the issue. Prose names no product.
12. **Not adopted yet**, each to be revisited when the demo has releases:
    - **DCO sign-off.** Varve's DCO certifies who may contribute code to a
      library others link. The demo has one contributor of record and no
      external contributions yet; a gate with nothing to distinguish is
      ceremony.
    - **The agent-review gate.** Varve's is bound to its trunk ruleset, its
      gates App and its release flow, none of which exist here. Human review on
      every pull request is the review until then.
    - **Release descriptors.** The demo has no releases to describe. When it
      publishes an image, a descriptor per release is the natural form.

## Alternatives considered

- **Take every Varve convention wholesale.** Rejected: the DCO, agent-review
  and release gates would be wired to nothing and go green trivially, which is
  scaffolding that looks like assurance.
- **Lighter conventions** (warnings as warnings, implicit usings, no
  analyzers). Rejected: the demo is evidence that Varve's way of building
  software is sound; a demo built more loosely than the thing it shows
  undercuts that.
- **A different licence** (MIT, Apache-2.0). Rejected: the brief names MPL-2.0,
  and one licence across Varve and its demo makes reuse between them simple.
- **xunit v2 or NUnit, on VSTest.** Rejected: Varve's tests are xunit.v3 on
  Microsoft Testing Platform; one shape across both repositories means one set
  of habits.
- **Gates as scripts** (`eng/*.cs` file-based apps, as Varve has). Rejected for
  now: the prompt asks that the gates run as tests, and as tests they run in
  every `dotnet test` with no second entry point and report through TRX. If a
  gate needs git history or the network, a script is the better home and an
  amendment says so.
- **Pull images from Docker Hub or MCR.** Rejected by the brief: only
  `ghcr.io` and `public.ecr.aws`.

## Consequences

- A contributor needs SDK `10.0.401` or a later feature band of 10.0.
- `DecisionDriven.Analyzers` is a prerelease; a bump can bring breaking rule
  changes, and the commit that bumps it says which.
- The demo's own code faces the same trimming discipline as Varve; anything
  needing reflection (a JSON serialiser without a source generator, say) is a
  design question, not a `NoWarn`.
- The three deferred conventions are a known gap, listed here so the first
  release is the trigger to revisit them.
