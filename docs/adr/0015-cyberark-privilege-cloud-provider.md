# ADR-0015: Integrate CyberArk Privilege Cloud as a separate provider

**Status:** Proposed

**Date:** 2026-07-24

**Deciders:** Vault Prospector product owner, maintainers, CyberArk tenant owner, and independent
security reviewer

## Context

Epic 14 requires CyberArk accounts, safes, objects, permissions, versions, and audit semantics.
CyberArk has multiple products with different resource and authorization models. Conjur/Secrets
Manager exposes policy-controlled variables and hosts; it does not natively model the Privilege
Cloud safe, account, CPM, version, and safe-member concepts named in the backlog. Reusing the Azure
Key Vault contracts would also hide material provider differences.

CyberArk's official
[`ark-sdk-python`](https://github.com/cyberark/ark-sdk-python) implements Identity Security Platform
service-user authentication and Privilege Cloud services. Its Privilege Cloud client uses
`PasswordVault/API/` and exposes separate services and models for safes, safe members, accounts,
credential retrieval, and account secret versions. CyberArk's separate official
[`conjur-api-dotnet`](https://github.com/cyberark/conjur-api-dotnet) models Conjur accounts,
policies, hosts, and variables, confirming that it is a distinct integration target.

The provider must remain useful for encrypted offline metadata search while ensuring that a normal
sync never retrieves a value. A CyberArk service-user credential is itself a high-value secret and
cannot share Azure token caches, SQLCipher metadata rows, logs, or browser mappings.

## Proposed decision

Support **CyberArk Privilege Cloud Shared Services** only in the first CyberArk integration.

Authentication uses a dedicated CyberArk Identity service user and authorization application:

1. the user configures root URLs on the exact `*.id.cyberark.cloud` and
   `*.privilegecloud.cyberark.cloud` production domains;
2. Vault Prospector submits the service user and client credential through the CyberArk Identity
   client-credentials endpoint for the configured application;
3. it completes the documented application-authorization redirect exchange without following the
   redirect; and
4. it uses the resulting short-lived platform token only for that one validate, sync, or retrieve
   operation.

The first release does not support interactive CyberArk users, on-premises PVWA, custom domains,
SAML browser sessions, RADIUS, LDAP, CyberArk authentication, Conjur, Secrets Hub, Central
Credential Provider, or certificate-based service users. Each requires a separate ADR and live
matrix.

The implementation uses a dedicated `ICyberArkProvider` and explicit CyberArk domain records:

- profile and authentication state;
- safe and retention/OLAC metadata;
- account object and provider-specific secret type/status;
- secret version;
- direct safe-member permission evidence; and
- value-free local audit event.

These records are never converted into `ConnectedIdentity`, `VaultResource`, `VaultItem`, or Azure
permission claims. The desktop has a separate CyberArk destination with persistent profile, safe,
account, permission, version, and audit context.

The service-user client credential is stored in a separate per-profile file protected by Windows
DPAPI `CurrentUser` with profile-specific entropy. It is validated before first persistence or
replacement. SQLCipher stores only profile configuration and synchronized metadata. Removing a
profile removes its protected credential and synchronized metadata; value-free local audit remains
for investigation.

Metadata sync lists visible safes, direct service-user safe-member evidence, accounts, and version
metadata. It never calls password retrieval. Retrieval requires:

1. an enabled, validated `Ready` profile;
2. an explicitly selected account and optional version;
3. a non-sensitive business reason;
4. fresh Windows verification;
5. a durable value-free authorization audit before the provider request; and
6. a `show` or `copy` request to the exact selected account.

CyberArk remains authoritative for effective permission evaluation and server-side audit. A direct
safe-member record is evidence, not a complete effective-access calculation because group, role,
confirmation, ticketing, dual-control, and platform policies can alter the result.

## Options considered

### CyberArk Privilege Cloud Shared Services

| Dimension | Assessment |
| --- | --- |
| Backlog fit | Direct safes, accounts, versions, permissions, and audit semantics |
| Isolation | Dedicated provider, credential store, metadata tables, UI, and audit |
| Deployment | SaaS production domains only |
| Complexity | Medium-high; Identity and Privilege Cloud boundaries both apply |

**Benefits:** Directly satisfies the named enterprise concepts and uses documented REST semantics.

**Costs:** Requires a CyberArk tenant, service-user administration, tenant-specific policy, and
live validation unavailable in the repository.

### Conjur / Secrets Manager

| Dimension | Assessment |
| --- | --- |
| Backlog fit | Poor; variables and policy replace safes/accounts |
| Isolation | Could use the official .NET client |
| Deployment | SaaS, enterprise, and OSS variants |
| Complexity | High because the product model and acceptance criteria would change |

Rejected for this story. It may become a later provider, but cannot be labeled the required
Privilege Cloud integration.

### Reuse the Azure provider model

Rejected. Azure tenants/subscriptions/vaults/RBAC and CyberArk safes/accounts/member permissions are
not equivalent. A least-common-denominator adapter would produce misleading authorization state.

### Store the client credential in SQLCipher

Rejected. Provider credentials need separate ownership, removal, replay boundaries, and DPAPI
binding. Keeping the credential beside searchable metadata would unnecessarily couple compromise
and recovery domains.

## Consequences

- Users configure and operate CyberArk separately from Microsoft Entra and Azure.
- Service-user client credentials are non-exportable through Vault Prospector and bound to the
  current Windows account.
- Re-enabling a profile permits validation/sync but not retrieval until it returns to `Ready`.
- Sync can be expensive because version and permission evidence require provider-specific calls;
  request, page, item, body, timeout, and cancellation limits are mandatory.
- Immutable .NET strings are required by HTTP authorization headers and the current Avalonia value
  presentation path. Tokens and values are therefore scoped to one operation, never cached or
  logged, and disposable byte/character buffers are zeroed where the runtime permits. A future
  native/secure-buffer design may reduce this residual exposure.
- Live Privilege Cloud contract, least-privilege, confirmation/ticketing, token revocation,
  throttling, and audit correlation evidence remains a release gate.

## Action items

1. [x] Add separate provider/domain/application contracts and UI.
2. [x] Add DPAPI-isolated per-profile credential storage and SQLCipher schema v6.
3. [x] Add bounded authentication, discovery, permission, version, and retrieval behavior.
4. [x] Add contract, redaction, persistence, verification, and accessibility tests.
5. [ ] Validate against a governed non-production Privilege Cloud tenant.
6. [ ] Complete independent security review and dispose every finding.
7. [ ] Validate the exact signed release artifact before enabling supported production use.
