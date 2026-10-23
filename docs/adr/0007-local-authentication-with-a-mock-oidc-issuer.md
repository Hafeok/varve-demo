# 0007 — Local authentication with a mock OIDC issuer

## Status

**Proposed — filed unaccepted by session 01 of #1, 2026-10-23.** Acceptance is
the maintainer's act on the pull request. Its decision set is
[`local-authentication-with-a-mock-oidc-issuer`](../decisions/local-authentication-with-a-mock-oidc-issuer.md).

## Context

The brief: "Varve.Server is OIDC-only with no API keys. The local compose setup
uses a mock OIDC issuer, never a bypass."

What Varve has, read at `v0.1.0-preview.11`:

- `Varve:Auth:Mode` is `Oidc` or `Anonymous`, with no default. Anonymous is
  reachable as `varve serve --anonymous`, as `--auth-mode Anonymous`, and as
  configuration (`Varve:Auth:Mode`, `VARVE__AUTH__MODE`); `Auth:Production`
  refuses it. In anonymous mode a commit has no agent.
- In OIDC mode the server reads `Varve:Auth:Authority` and
  `Varve:Auth:Audiences`, discovers keys at
  `{Authority}/.well-known/openid-configuration`, takes the subject from `sub`
  (or `oid`) and roles from `roles`.
- Permissions (`read`, `write`, `admin`) are **granted to roles by
  declarations** in the management plane, applied with `varve apply`; there
  are no fixed role names. The management plane has its own audience (default
  `api://varve-manage`), and bootstrap operators are named in
  `Varve:Manage:BootstrapOperators`.
- Varve tests against `navikt/mock-oauth2-server` and Zitadel. Its
  `.github/ci-images.yaml` pins mock-oauth2-server at upstream
  `ghcr.io/navikt/mock-oauth2-server:6.0.4`, digest
  `sha256:47374fe063995d9b6d39ebb6ea21eb5f765025bf0aa08b61b08b439eb91adaf0`,
  MIT; Varve's CI pulls a private mirror of it.

## Decision

1. **Never `--anonymous`**, nor `Auth:Mode` `Anonymous` in any spelling. The
   gate `ConfigurationGates.No_configuration_file_uses_anonymous_mode` refuses
   both in every configuration file.
2. **The issuer is `ghcr.io/navikt/mock-oauth2-server`**, upstream, at the tag
   and digest Varve's `.github/ci-images.yaml` pins:
   `6.0.4@sha256:47374fe063995d9b6d39ebb6ea21eb5f765025bf0aa08b61b08b439eb91adaf0`
   (checked against `ghcr.io` on 2026-10-23). Varve's CI uses a private mirror;
   the demo uses upstream, which a viewer can pull.
3. **`deploy/mock-oauth2.json` is committed** with one client per role, each by
   client credentials:
   - `varve-demo-importer`, which writes;
   - `varve-demo-viewer`, which reads;
   - `varve-demo-operator`, which applies the dataset's declarations and
     grants on the management plane's audience.
   The grants that give each role its permission are declarations applied with
   `varve apply` by the operator, as Varve documents; they are committed beside
   the issuer's configuration.
4. **The README says plainly that the issuer is a mock**, that its tokens prove
   nothing about anyone, and shows how to point the demo at a real issuer: set
   the authority and audience, create the three clients there, apply the same
   grants.

## Alternatives considered

- **Anonymous mode for local runs.** Rejected by the brief. It would also
  remove the agent from every commit, which S3's diff and the evidence about
  who wrote what depend on.
- **Zitadel by default.** A real OIDC provider, which Varve also tests against.
  Rejected as the default: it needs Postgres and a bootstrap, which costs most
  of S1's three-minute budget. It could be an optional compose profile later,
  under its own ADR.
- **Keycloak.** Rejected: its images are on `quay.io`, outside the allowed
  registries.
- **A token minted by a script with a local key.** Rejected: an issuer the demo
  wrote is a bypass with extra steps, and the brief asks for a mock issuer, not
  a homemade one.
- **Varve's private mirror of the image.** Rejected: viewers cannot pull it.

## Consequences

- Every step pays for a token request; it is local and small.
- The mock issuer will sign a token for any client that asks. The README says
  so, and nothing in the demo treats a token as evidence of identity.
- A bump re-checks Varve's pin for the issuer; the demo follows it when Varve
  moves, with this ADR amended.
- The grant declarations follow Varve's management-plane vocabulary, so a
  breaking change there is a bump's work.
