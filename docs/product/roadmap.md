# Roadmap

The roadmap is intentionally capability-based. Dates should not be assigned until the feasibility spikes and initial staffing are complete.

## Phase 0 — Product and security foundation

- Project charter.
- Requirements and glossary.
- Architecture baseline.
- Initial ADRs.
- Threat model.
- Research spikes.
- Repository standards.
- CI, formatting, unit-test, and dependency-scanning baseline.

## Phase 1 — Desktop metadata prototype

- Windows desktop shell.
- Microsoft Entra interactive sign-in.
- One connected identity.
- Subscription discovery.
- Key Vault discovery.
- Secret metadata indexing.
- Local encrypted database.
- Basic name search.
- No secret values stored locally.

## Phase 2 — Multi-identity and multi-tenant MVP

- Multiple connected identities.
- Guest and resource-tenant handling.
- Identity-to-resource access mapping.
- Workspaces.
- Search filters.
- Keys and certificates metadata.
- Favorites.
- Sync status and error reporting.
- Secure on-demand secret retrieval.
- Secure clipboard clearing.

## Phase 3 — Offline cache preview

- Explicit per-item offline caching.
- Platform-backed local unlock.
- Cache lifetime policy.
- Cache purge workflows.
- Staleness indicators.
- Security review and attack testing.
- No cloud synchronization.

## Phase 4 — macOS and iOS

- macOS validation.
- iOS application shell.
- Mobile-safe search and retrieval.
- Platform keychain integration.
- Background refresh feasibility.
- Password AutoFill feasibility.
- Mobile offline cache policy.

## Phase 5 — Enterprise controls

- Configuration policy.
- Offline-cache disablement.
- Allowed-tenant and allowed-provider policy.
- Managed configuration.
- Audit-friendly local access history.
- Exportable diagnostics without sensitive data.
- Signed releases and supply-chain hardening.

## Phase 6 — Provider ecosystem

Potential providers, subject to separate ADRs:

- HashiCorp Vault.
- GitHub Actions secrets metadata.
- 1Password Connect.
- Bitwarden Secrets Manager.
- AWS Secrets Manager.
- Google Secret Manager.
- Kubernetes secrets through approved cluster access.

Provider expansion must not weaken the Azure security model or create a lowest-common-denominator abstraction.
