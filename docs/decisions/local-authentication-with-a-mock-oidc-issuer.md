---
set: local-authentication-with-a-mock-oidc-issuer
namespace: varvedemo
adr: 0007
decisions:
  - key: NeverAnonymous
    statement: "No configuration uses anonymous mode, by --anonymous or by Auth Mode Anonymous"
  - key: MockOauth2ServerUpstream
    statement: "The issuer is ghcr.io/navikt/mock-oauth2-server from upstream, at the tag and digest Varve pins in its ci-images list"
  - key: ClientPerRole
    statement: "deploy/mock-oauth2.json holds one client-credentials client per role: importer writes, viewer reads, operator applies declarations and grants"
  - key: ReadmeSaysItIsAMock
    statement: "The README says the issuer is a mock and shows how to point the demo at a real issuer"
---

The rulings of [ADR 0007](../adr/0007-local-authentication-with-a-mock-oidc-issuer.md), filed unaccepted by session 01 of
#1 on 2026-10-23. Every citation is `CS0618` until the maintainer accepts them by
adding `accepted-by` and `accepted-at`, which a session never writes.
