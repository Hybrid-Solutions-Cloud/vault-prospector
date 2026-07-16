# Roadmap

The roadmap is intentionally capability-based. Phases 0–3 form the `0.1.0-preview.2` Windows evaluation release; remaining hardening and platform phases stay open until their acceptance evidence exists.

## Phase 0 — Product and security foundation

Status: delivered in the 0.1 preview.

- Project charter.
- Requirements and glossary.
- Architecture baseline.
- Initial ADRs.
- Threat model.
- Research spikes.
- Repository standards.
- CI, formatting, unit-test, and dependency-scanning baseline.

## Phase 1 — Desktop metadata prototype

Status: delivered in the 0.1 preview.

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

Status: delivered in the 0.1 preview, with direct tenant/subscription workspace assignment tracked for a follow-up.

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

Status: delivered for evaluation in the 0.1 preview; independent security review and broader attack testing remain ongoing release-hardening work.

- Explicit per-item offline caching.
- Platform-backed local unlock.
- Cache lifetime policy.
- Cache purge workflows.
- Staleness indicators.
- Security review and attack testing.
- No cloud synchronization.

## Phase 4 — iPhone and Google mobile applications (coming soon)

These applications are coming soon after the Windows distribution path. No Apple App Store or Google Play release is included in the current Windows desktop preview, and each mobile release must satisfy its own security and store review gates.

- macOS validation.
- iOS application shell.
- Android application shell.
- Mobile-safe search and retrieval.
- Apple Keychain, Secure Enclave, and Android Keystore integration.
- Background refresh feasibility.
- Apple Password AutoFill and Android Autofill framework feasibility.
- Mobile offline cache policy.
- Apple App Store privacy, entitlement, signing, and review preparation.
- Google Play data-safety, signing, target-SDK, and review preparation.
- Separate mobile threat models and penetration testing before public distribution.

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
