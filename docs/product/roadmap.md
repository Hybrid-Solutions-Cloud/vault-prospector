# Roadmap

The roadmap is intentionally capability-based. Phases 0–3 form the published `0.1.1-preview.1` Windows evaluation release; remaining hardening and platform phases stay open until their acceptance evidence exists.

Release promotion is controlled by the evidence-based [Preview and GA readiness matrix](release-readiness.md). A phase marked delivered here describes implemented capability; it does not override an incomplete release gate.

## Near-term sequencing

The next work is ordered by security dependency rather than visual novelty. Secure onboarding, identity boundaries, encryption guarantees, and independent review come before write operations or browser autofill.

| Horizon | Initiative | Status | Dependency or trade-off |
| --- | --- | --- | --- |
| Now | Security hardening and secure first-run setup | In progress | A verified local unlock now leads directly to a guided identity setup that separates Windows protection, Microsoft authentication, and metadata-only sync; runtime usability, Windows Hello recovery, tenant-policy, exact-release, and independent security evidence remain. |
| Now | Encrypted local-data recovery and migration | In progress | Missing/wrong keys, corruption, incomplete/future schemas, v1-to-v4 migration, and internal all-or-rollback key rotation fail closed under tests. Canonical recovery archives are inventoried and can be explicitly deleted only after typed confirmation and fresh Windows verification. Rotation user exposure, live power-loss/reinstall validation, and independent evidence remain under G-03; device/profile replacement intentionally resynchronizes. |
| Now | Preview feedback and reliability cycle | In progress | Governed public intake, an explicit submission notice, privacy boundaries, triage cadence, and measurable G-01 thresholds are defined; collection, upgrade coverage, blocker closure, and stability windows still require evidence. |
| Now | Desktop UI and password-manager interface research | In progress | Comparative research and four interactive concepts are complete; participant evidence, selection, production implementation, and assistive-technology validation remain. |
| Next | Taskbar background operation and metadata synchronization | In progress locally | Lock-on-hide notification-area lifecycle, session/suspend/resume boundary locking, and opt-in metadata-only synchronization are implemented; installed Windows lifecycle and policy evidence remain. |
| Next | Identity-source expansion and read-only/write-mode policy | In progress locally | Workload profiles, discovery, dry-run provisioning plans, and permission-aware read-only discovery are implemented locally. Governed mutations remain gated behind independent security review. |
| Next | CyberArk provider integration | Implemented locally, validation open | Privilege Cloud provider, isolated credential/metadata/UI boundaries, and automated tests are present; governed live tenant, independent review, and signed exact-artifact evidence remain. |
| Next | Browser extension and explicit one-time fill | In progress locally | Origin/frame/purpose binding, authenticated native messaging, protected machine policy, mappings, confirmation, verification, audit, and MSI host registration are implemented; signed distribution, live installed-browser, independent-review, compromise/revocation, usability, and AT gates remain. Private browser password-store access is prohibited. |
| Parallel | iPhone/iOS and Android/Google Play applications | Coming soon | Mobile delivery continues, but does not bypass the same security and store-review gates. |

Identity planning follows current Microsoft platform boundaries:

- Windows Web Account Manager can integrate desktop sign-in with accounts known to Windows and support Windows Hello, Conditional Access, and FIDO credentials through MSAL ([Microsoft desktop WAM guidance](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-acquire-token-wam)).
- Managed identity tokens are supplied to workloads running on supported Azure compute; an ordinary Windows desktop should use an interactive Entra account or a separately configured workload credential instead ([managed identity overview](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview)).
- Listing or creating user-assigned managed identities and assigning Azure roles require separate management permissions; visibility of an identity does not imply permission to use it or access Key Vault data ([managed identity administration](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/manage-user-assigned-managed-identities-azure-portal), [Key Vault RBAC roles](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-migration)).

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

- Secure first-run wizard for local unlock and Azure connection setup.
- Windows account/WAM sign-in feasibility with Windows Hello, MFA, Conditional Access, and FIDO support delegated to the platform and identity provider.
- Explicit human and workload identity profiles with token caches isolated from Azure CLI, Azure PowerShell, developer tools, and other terminal sessions.
- Exact-scope read-only workload authorization evidence that preserves inherited grants, deny and
  condition uncertainty, and the distinction between static evidence and runtime access.
- Read-only access mode by default, with separately governed and visibly elevated write capabilities.
- Security review, attack testing, and encryption-at-rest verification before expanding write or unattended access.
- Configuration policy.
- Offline-cache disablement.
- Allowed-tenant and allowed-provider policy.
- Managed configuration.
- Audit-friendly local access history.
- Exportable diagnostics without sensitive data.
- Signed releases and supply-chain hardening.

## Phase 6 — Provider ecosystem

Potential providers, subject to separate ADRs:

- CyberArk Privilege Cloud is implemented locally under ADR-0015; release validation remains open.
- HashiCorp Vault.
- GitHub Actions secrets metadata.
- 1Password Connect.
- Bitwarden Secrets Manager.
- AWS Secrets Manager.
- Google Secret Manager.
- Kubernetes secrets through approved cluster access.

Provider expansion must not weaken the Azure security model or create a lowest-common-denominator abstraction.

## Phase 7 — Desktop experience and browser integration

- Research established password-manager and credential-vault interfaces, including accessibility and high-risk-action patterns.
- Refine onboarding, identity selection, vault discovery, search, and security-state visibility.
- Optional taskbar notification-area operation with an explicit locked background state.
- Browser-extension and native-messaging feasibility for Chromium and Firefox families.
- Browser password-vault interoperability research without importing or exposing credentials by default.
- Origin-bound autofill prototypes that require explicit item mapping, user action, policy approval, and local verification for sensitive values.
- Separate browser-extension threat model, permission review, signing, update, and compromise-response plan before distribution.

## Phase 8 — Preview learning and GA promotion

Status: in progress. The process is operational; real evaluator and reliability evidence remains.

- Voluntary HCS-governed public intake with an explicit publication notice and sensitive-data exclusions.
- Private security reporting separated from public product feedback.
- Business-day triage and weekly sanitized evidence rollups.
- Measurable evaluator, task-completion, Windows-build, install-path, upgrade, blocker, and stability thresholds.
- Formal G-01 decision only after [all feedback-cycle criteria](preview-feedback.md) are evidenced.
- GA promotion only after every remaining [release-readiness gate](release-readiness.md) passes.
