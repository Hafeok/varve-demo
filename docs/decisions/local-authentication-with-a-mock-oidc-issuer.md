---
set: local-authentication-with-a-mock-oidc-issuer
namespace: varvedemo
adr: 0007
decisions:
  - key: NeverAnonymous
    statement: "No configuration uses anonymous mode, by --anonymous or by Auth Mode Anonymous"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: MockOauth2ServerUpstream
    statement: "The issuer is ghcr.io/navikt/mock-oauth2-server from upstream, at the tag and digest Varve pins in its ci-images list"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ClientPerRole
    statement: "deploy/mock-oauth2.json holds one client-credentials client per role: importer writes, viewer reads, operator applies declarations and grants"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: OperatorClientIsABootstrapOperator
    statement: "The operator client is a bootstrap operator on the management audience, which gives it server-admin to create the dataset and admin on it for the checkpoints and status the importer reads"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: EveryWriteCarriesItsAgent
    statement: "Every write in the log carries a real agent, the subject of the token that made it, and no write path that records no agent is used"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: ReadmeSaysItIsAMock
    statement: "The README says the issuer is a mock and shows how to point the demo at a real issuer"
    accepted-by: mailto:emil@okkels-klein.dk
    accepted-at: 2026-10-26T11:49:23Z
  - key: CheckpointsByADatasetAdminClient
    statement: "Checkpoints are taken by varve-demo-checkpointer, which holds admin on the dataset alone with management-audience tokens; the bootstrap operator is used only by a one-shot setup service"
---

The rulings of [ADR 0007](../adr/0007-local-authentication-with-a-mock-oidc-issuer.md), filed by session 01 of #1 on
2026-10-23 and accepted by the maintainer on 2026-10-26, on the foundation's
pull request #2. Every ruling filed then carries `accepted-by` and `accepted-at`.

`CheckpointsByADatasetAdminClient` is the 2026-11-03 amendment's ruling, filed
unaccepted by session 03 on #5.
