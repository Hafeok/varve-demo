# 0007 — Local authentication with a mock OIDC issuer

## Status

**Accepted — by the maintainer on 2026-10-26, on the foundation's pull request
[#2](https://github.com/Hafeok/varve-demo/pull/2).** Filed by session 01 of
#1, 2026-10-23. Its decision set is
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
4. **The operator client is a bootstrap operator**
   (`Varve:Manage:BootstrapOperators`, Varve's `src/Varve.Server/Settings.cs:225`).
   Its tokens are for the management plane's audience (`Auth:Management:Audience`,
   `api://varve-manage` by default; a token for both planes is refused by
   both, `src/Varve.Server/ServerHost.cs:376-381, 459-470`). It needs two
   permissions, checked against Varve's source:
   - **`varve:server-admin`**, to create the dataset by applying its
     declaration (`src/Varve.Protocol/Endpoints/ManagementEndpoints.cs:679-692`);
     only an operator holds it (`src/Varve.Server/Permissions.cs:69`);
   - **`varve:admin` on the dataset**, for the checkpoints the importer takes
     through it (ADR 0004): `POST /manage/datasets/{name}/checkpoints` demands
     `DatasetPermissions.Admin` (`src/Varve.Protocol/Endpoints/AdminEndpoints.cs:247`,
     reached from `ManagementEndpoints.cs:389-398`), as does reading the
     checkpoint list at `/manage/datasets/{name}/status`
     (`src/Varve.Protocol/Endpoints/StatusEndpoint.cs:29`). An operator passes
     that check on every dataset (`src/Varve.Management/GrantTable.cs:87-94`).

   So the operator role covers both, and no extra grant is declared for it. An
   `admin` grant on the one dataset would be enough for the checkpoints alone,
   if the operator role is ever narrowed.
5. **Every write in the log carries a real agent: the subject of the token
   that made it.** Over HTTP, Varve sets a commit's agent from the caller's
   token (`src/Varve.Protocol/Endpoints/Writes.cs:274`): the issuer, `#`, and
   the percent-encoded subject (`TokenIdentity.AgentOf`,
   `src/Varve.Server/Identity.cs:26-40`; Varve's decision
   `AgentIsIssuerAndSubjectIri`, its ADR 0094). In anonymous mode a commit has
   none (`NoAgent`, `Identity.cs:65-69`). So never anonymous (point 1) is what
   makes every commit name who made it, and this ruling says so on its own.
   Even with OIDC on, `AgentOf` gives no agent when a token has no `iss` or no
   subject claim, when the issuer contains `#`, or when the result is not a
   valid IRI. So every client in `deploy/mock-oauth2.json` is given a `sub`,
   and the issuer's URL has no fragment. No write path that records no agent is used:
   Varve's embedded bulk load is one (ADR 0008). A client reads the agent on
   the commit's feed record (`agent <term>`, Varve's `docs/spec/change-feed.md`
   §2.1); S1 and S6 check it.
6. **The README says plainly that the issuer is a mock**, that its tokens prove
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

## Amendment, 2026-11-03: re-checked against `v0.1.0-preview.15`

Filed unaccepted by session 03, on #3, the bump to `0.1.0-preview.15`.

- `Identity.cs`, `Writes.cs`, `Permissions.cs`, `Settings.cs`,
  `GrantTable.cs` and `AdminEndpoints.cs` are unchanged, so point 5's account
  of the agent and point 4's of the permissions hold.
- `ServerHost.cs` changed, but the lines point 4 cites (376-381, 415,
  459-470) are the same code at the same lines.
- In `ManagementEndpoints.cs`, the apply authorisation point 4 cites
  (679-692) is now at 783-796, unchanged; the checkpoint route (389-398) is
  now at 390-399.
- The status endpoint's `admin` check is at `StatusEndpoint.cs:30`, as it was
  at `v0.1.0-preview.11`; point 4's "29" was off by one when written.
- Varve's `.github/ci-images.yaml` still pins
  `ghcr.io/navikt/mock-oauth2-server:6.0.4` at the digest point 2 names.

Point 5's case of a token under OIDC that yields no agent is filed as
[Hafeok/Varve#162](https://github.com/Hafeok/Varve/issues/162).

## Amendment, 2026-11-03: least privilege, and a checkpointer client

Filed unaccepted by session 03, on #5 (slice 1a). It narrows point 4.

**What changes.** Point 4 has the bootstrap operator, which holds
`server-admin`, take the importer's checkpoints. A sceptical viewer reading
`compose.yaml` should find that each long-running service holds only what it
needs. So:

1. **The operator is used once.** `varve-demo-operator` stays the bootstrap
   operator, and only the one-shot `setup` service uses it, with
   `varve apply deploy/nuget.trig`. That declares the dataset and its
   grants. No long-running service holds the operator's credentials.
2. **A fourth client, `varve-demo-checkpointer`**, holds `admin` on the
   dataset `nuget` alone, by a grant in that declaration to its role
   `varve-demo.checkpointer`. It is not an operator, and its tokens are for
   the management plane's audience only. The importer takes ADR 0004's
   checkpoints through it.
3. **The importer client holds `write`** on every graph of `nuget` (its role
   `varve-demo.importer`). The viewer client holds `read`. Neither holds
   `admin`.

**Why a separate client, not `admin` on the importer client.**

- **One audience per client.** Varve refuses a token whose `aud` names both
  planes (point 4, `ServerHost.cs:376-381, 459-470`). The importer's data
  writes need the data plane's audience; a checkpoint needs the management
  plane's. The mock issuer maps claims by a single request parameter, so
  one audience per client is the plain way to give each token exactly one
  audience.
- **What each token can do is what its client is for.** An importer token
  can write the dataset and nothing on the management plane. A checkpointer
  token can reach the dataset's management resources and nothing on the
  data plane. Measured against `v0.1.0-preview.15` on 2026-11-03:
  - a checkpointer token posting an update to `/datasets/nuget/sparql` is
    `401`;
  - a viewer token posting the same update is `403`;
  - a checkpointer token posting to `/manage/datasets/nuget/checkpoints` is
    `201`.
- **The agent stays the writer's.** Every data commit is made by the
  importer's token, so point 5's agent is the importer's subject. A
  checkpoint is not a commit.

The importer process holds two client credentials, the importer's and the
checkpointer's, and the README says so. With the mock issuer they are client
ids with no real secret; with a real issuer they are two registrations.

ADR 0004 point 7 says the checkpoints are taken "through the operator
client, in the operator's role". Its own amendment of this date points here.
Its ruling `ImporterTakesCheckpointsAsOperator` is accepted, and this
narrows who "the operator" is to a dataset admin. If the maintainer reads
that as a change of meaning rather than a refinement, ADR 0001 point 4 asks
for a superseding ADR instead, and this amendment becomes its draft.
