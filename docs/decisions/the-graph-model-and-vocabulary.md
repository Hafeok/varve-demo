---
set: the-graph-model-and-vocabulary
namespace: varvedemo
adr: 0010
decisions:
  - key: CatalogTermsAsVarveJsonLdExpandsThem
    statement: "A leaf's quads are what Varve.JsonLd produces from it with its own context, with no renaming and no term of ours in their place"
  - key: LeafQuadsInItsVersionGraph
    statement: "The importer writes every quad of a leaf into the leaf's version graph, with blank node labels made unique per leaf within an update"
  - key: DerivedTermsInAUrnNamespace
    statement: "Terms the demo derives live in urn:varve-demo:term:, an unregistered URN namespace the README names as such, never in an unregistered w3id.org namespace"
  - key: CatalogLogUsesCatalogTerms
    statement: "Each retained catalog commit is urn:varve-demo:catalog-commit:commitId in urn:varve-demo:catalog-log with catalog:commitId and catalog:commitTimeStamp"
  - key: SortableVersionKey
    statement: "The importer asserts urn:varve-demo:term:versionKey on each leaf, a string whose code-point order is NuGet's SemVer 2.0 version order, computed by the importer"
  - key: OracleOrdersWithNuGetVersioning
    statement: "The S2 oracle orders versions with NuGet.Versioning, a test-only package, and CI checks that it and the version key agree on every version in the snapshot"
  - key: NoOtherDerivedData
    statement: "Deprecation, vulnerability, listing and prerelease status are read by queries from the catalog's own terms, never from a derived flag"
  - key: LeafProjectedToWhatTheStepsRead
    statement: "Every leaf is read whole by Varve.JsonLd and the store keeps only the catalog's advisory state: id, version, listed, deprecation with reasons and alternate package and range, vulnerabilities with advisory URL and severity, and the version key; base leaves may be stored pruned to those members by the rule the manifest records, and the oracle reads the same fields"
  - key: BaseStateInTheCatalogLog
    statement: "Each base commit asserts urn:varve-demo:base:id in urn:varve-demo:catalog-log with vd:basePackage and vd:baseAsOf T0, so the time index places the base at T0"
---

The rulings of [ADR 0010](../adr/0010-the-graph-model-and-vocabulary.md), filed
unaccepted by session 03 on #5, 2026-11-03. Every citation is `CS0618` until
the maintainer accepts them.

`LeafProjectedToWhatTheStepsRead` and `BaseStateInTheCatalogLog` are the
2026-11-03 amendment's rulings, filed unaccepted by session 03 on #5.
