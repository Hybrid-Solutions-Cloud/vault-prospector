# Roadmap

The roadmap is intentionally capability-based. The published `0.3.0-preview.17` Windows evaluation
release includes the integrated Windows implementation; live-provider, independent-review,
trusted-signing, and GA acceptance evidence remain open as named below. Mobile is a separate
post-Windows roadmap and does not block Windows GA.

Release promotion is controlled by the evidence-based [Preview and GA readiness matrix](release-readiness.md). A phase marked delivered here describes implemented capability; it does not override an incomplete release gate.

## Near-term sequencing

The next work is ordered by security dependency rather than visual novelty. Secure onboarding, identity boundaries, encryption guarantees, and independent review come before write operations or browser autofill.

| Horizon | Initiative | Status | Dependency or trade-off |
| --- | --- | --- | --- |
| Now | Security hardening and secure first-run setup | In progress | A verified local unlock now leads directly to a guided identity setup that separates Windows protection, Microsoft authentication, and metadata-only sync; runtime usability, Windows Hello recovery, tenant-policy, exact-release, and independent security evidence remain. |
| Now | Encrypted local-data recovery and migration | In progress | Missing/wrong keys, corruption, incomplete/future schemas, v1-to-v4 migration, and internal all-or-rollback key rotation fail closed under tests. Canonical recovery archives are inventoried and can be explicitly deleted only after typed confirmation and fresh Windows verification. Rotation user exposure, live power-loss/reinstall validation, and independent evidence remain under G-03; device/profile replacement intentionally resynchronizes. |
| Now | Preview feedback and reliability cycle | In progress | Governed public intake, an explicit submission notice, privacy boundaries, triage cadence, upgrade coverage, issue disposition, and a named G-01 decision are required; no arbitrary elapsed-time or evaluator quota blocks promotion. |
| Now | Legal and privacy release readiness | In progress | Deterministic component inventory/notices, technical privacy disclosures, package/store draft metadata, CI drift checks, and Windows package embedding are implemented. Exact-candidate legal review, a public privacy URL, store declarations, and named approval remain. |
| Now | Desktop UI and password-manager interface research | In progress | Comparative research and four interactive concepts are complete; participant evidence, selection, production implementation, and assistive-technology validation remain. |
| Now | Performance and large-estate validation | In progress | A controlled 10-identity, 200-vault, 50,000-object encrypted baseline passes sync, search, initialization/reopen, cancellation, memory, and storage targets. Representative devices, packaged-app startup, live provider conditions, populated UI/AT responsiveness, and exact signed-candidate repetition remain. |
| Next | Taskbar background operation and metadata synchronization | In progress locally | Lock-on-hide notification-area lifecycle, session/suspend/resume boundary locking, and opt-in metadata-only synchronization are implemented; installed Windows lifecycle and policy evidence remain. |
| Next | Identity-source expansion and read-only/write-mode policy | Implemented internally; validation gated | Workload profiles, discovery, dry-run provisioning plans, permission-aware read-only discovery, and four separately governed Key Vault mutation operations are implemented. Mutations remain default-hidden behind accepted-build and exact machine-policy gates until live Azure and independent review pass. |
| Post-GA | CyberArk provider integration | Source prototype implemented; unsupported | Privilege Cloud provider, isolated credential/metadata boundaries, and automated tests remain in private source. The Windows release UI is disabled until after Windows GA and a governed test tenant, product decision, independent review, and separate release evidence exist. |
| Next | Browser extension and explicit one-time fill | In progress locally | Origin/frame/purpose binding, authenticated native messaging, protected machine policy, mappings, confirmation, verification, audit, and MSI host registration are implemented; signed distribution, live installed-browser, independent-review, compromise/revocation, usability, and AT gates remain. Private browser password-store access is prohibited. |
| Future roadmap | iPhone/iOS and Android/Google Play applications | Source prototypes implemented; not released | Mobile work has its own future roadmap and release gates. Mobile signing, physical-device testing, and store acceptance do not block Windows GA. |
| Final pre-GA | Trusted Windows executable signing | Planned | Complete after all Windows code, testing, and release evidence are final. Signing is the last GA promotion task and does not block producing the explicitly unsigned manual-test Preview. |

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

## Future roadmap — iPhone and Google mobile applications

These applications are roadmap work, not part of the Windows GA release. No Apple App Store or
Google Play release is included in the current Windows desktop preview, and each mobile release must
satisfy its own security and store review gates.

- macOS validation.
- iOS application shell — implemented and merged.
- Android application shell — implemented and merged.
- Mobile-safe search and retrieval — implemented and merged.
- Apple Keychain/LocalAuthentication and Android Keystore/BiometricPrompt integration —
  implemented and merged; physical-device validation open.
- Background refresh feasibility.
- Apple Password AutoFill and Android Autofill framework feasibility.
- Mobile offline cache policy — secure default disables value caching.
- Apple App Store privacy, entitlement, signing, and review preparation — source baseline present;
  signing and review open.
- Google Play data-safety, signing, target-SDK, and review preparation — source baseline present;
  signing and review open.
- Separate mobile threat models and penetration testing before public distribution.

## Phase 5 — Enterprise controls

- Secure first-run wizard for local unlock and Azure connection setup.
- Windows account/WAM sign-in feasibility with Windows Hello, MFA, Conditional Access, and FIDO support delegated to the platform and identity provider.
- Explicit human and workload identity profiles with token caches isolated from Azure CLI, Azure PowerShell, developer tools, and other terminal sessions.
- Exact-scope read-only workload authorization evidence that preserves inherited grants, deny and
  condition uncertainty, and the distinction between static evidence and runtime access.
- Read-only access mode by default, with separately governed and visibly elevated write capabilities.
- Security review, attack testing, and encryption-at-rest verification before expanding write or unattended access.
- Machine-managed configuration policy is implemented locally through versioned HKLM policy and
  packaged ADMX/ADML templates. It constrains allowed tenants, providers, identity types, clipboard
  use, and offline-cache retention; governed deployment and independent/live validation remain
  open.
- Audit-friendly local access history.
- Exportable diagnostics without sensitive data.
- Signed releases and supply-chain hardening as the final task before Windows GA promotion.

## Phase 6 — Provider ecosystem

Potential providers, subject to separate ADRs:

- CyberArk Privilege Cloud remains an unsupported future provider under ADR-0015. Its source is not
  evidence that it is part of the Windows release.
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

Status: in progress. Feedback and operational-readiness processes are implemented; real hosted,
exercise, evaluator, and reliability evidence remains.

- Voluntary HCS-governed public intake with an explicit publication notice and sensitive-data exclusions.
- Private security reporting separated from public product feedback.
- Business-day triage and weekly sanitized evidence rollups.
- Weekly dependency proposals plus vulnerability, runtime-EOS, public-release, and support-channel
  monitoring with retained JSON evidence.
- Published Preview supersedence/withdrawal and future GA end-of-support rules, named primary
  support/security ownership, and documented credential/signing controls.
- Evidence that supported install, upgrade, and core workflows pass on the exact candidate, all
  reports are triaged and dispositioned, and no known release or security blocker remains.
- Formal G-01 decision only after [all feedback-cycle criteria](preview-feedback.md) are evidenced.
- GA promotion only after every remaining [release-readiness gate](release-readiness.md) passes.
