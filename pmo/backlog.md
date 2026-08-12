# Vault Prospector Product Backlog

## Current delivery state

`0.3.0-preview.8` is the latest public, unsigned Windows Preview for non-production evaluation.
Its immutable source tag points to `2582a44e50155c80205370c6ec90b9d19eb7a006`; this repository
does not yet contain a dedicated Preview 8 release-evidence record, so Preview 6 remains the most
recent release with complete retained artifact/provenance evidence in this backlog.
The installed Start-menu/Search icon fix and the completed Phases 3–13 implementation are included
in the 0.2 Preview line and remain subject to the live, independent, Store, and GA validation
gates recorded below.
The separate missing installer-logo/red-X defect is tracked by GitHub Bug #62 and ADO AB#5799.
Its source correction is included in Preview 8; exact-package visual lifecycle evidence remains
open.
The 2026-08-11 installed-support investigation produced the current remediation batch: GitHub
Bugs #91–#93 mirror to ADO AB#7299, AB#7301, and AB#7303; ADO Story AB#7305 owns the discovered
subscription/account picker; and AB#7307–#7308 extend existing isolated-sync Bug #40 / AB#5573.
Source and automated tests now cover all-connected-identity background sync, the real Azure
subscription ID, dismissible error banners, and retained privacy-safe failure fields. These are
local implementation results, not installed-package closure evidence.
The implementation-first [execution plan](plan.md) governs sequencing. Release evidence remains in
the [release-readiness matrix](../docs/product/release-readiness.md), and the capability-level view
remains in the [roadmap](../docs/product/roadmap.md).

## Requested-feature implementation status

This table distinguishes code that exists in the current executable from requirements that are
only documented. A backlog entry does **not** mean the feature is implemented.

| Requested capability | Current status | What exists now | What is still missing |
| --- | --- | --- | --- |
| Normal Windows installer and update path | Implemented; installer-branding validation open | MSI, portable ZIP, immutable GitHub Preview releases, upgrade/repair/uninstall/rollback validation, and the Preview 8 installer-logo source correction | Exact-package installer visual lifecycle evidence, trusted signing, WinGet catalog acceptance, Chocolatey catalog acceptance |
| Interactive Entra user login | Implemented | MSAL public-client system-browser authentication with app-owned token caches | Full live tenant/guest/MFA/Conditional Access evidence |
| Local login/unlock and MFA boundary | Included in 0.2 Preview; validation open | Fail-closed app unlock and sensitive operations use Windows verification; recovery archives failed state after typed confirmation and fresh verification | Full live Windows Hello/recovery coverage and independent review |
| Mandatory local encryption | Implemented locally, review open | SQLCipher metadata and AES-GCM offline values with DPAPI keys; verified archive plus authenticated-journal all-or-rollback rotation engine; startup recovery; explicit verified per-archive retention/deletion UX; no plaintext toggle | User-exposed rotation only after independent review, live power-loss validation, supported cross-device decision remains resync |
| Isolation from Azure CLI/PowerShell terminal context | Implemented | App-owned MSAL accounts and caches; no terminal-context credential provider | Broader live multi-account validation and clearer active identity/tenant UI |
| Managed-identity authentication | Included in 0.2 Preview; validation open | Azure-host detection, profile UI, isolated credential flow, ARM-token validation, local disable/revoke controls, automated tests | Live Azure matrix, external assignment-revocation evidence, independent review |
| Service-principal authentication | Included in 0.2 Preview; validation open | Certificate and federated-token-file profiles, private-key/token isolation, validate-first rotation, local revocation/cache purge, redacted lifecycle events, automated tests | Live Azure matrix, external issuer-revocation evidence, independent review |
| List existing managed identities/SPNs | Current correction implemented locally; package validation open | Administration now offers subscriptions discovered across every ready interactive connection, labeled with subscription, tenant, and account; managed-identity discovery uses the real Azure subscription ID; explicit-consent Graph service-principal discovery and honest permission distinctions remain | Install the corrected package, repeat discovery across the three connected identities, complete inherited/deny/conditional RBAC analysis, live validation, and independent review |
| Create a managed identity/SPN during setup | Preview implemented locally | User-reachable deterministic non-mutating managed-identity and service-principal plans with exact optional Key Vault/role scope; no execution command | Security gate, fresh write authorization, confirmation, encrypted audit, rollback, governed creation/live tests |
| Discover accessible Key Vaults | Included in 0.2 Preview; validation open | Selected identity enumerates visible resources; explicit subscription/vault scope and per-vault observed permission display are user-accessible | Live human/workload Azure permission matrix and independent validation |
| Machine-managed enterprise access policy | Implemented locally, validation open | HKLM/ADMX policy for allowed tenants, providers, and identity types plus clipboard/offline-cache boundaries; service-layer enforcement, safe Settings status, package templates, and automated fail-closed tests | Governed Group Policy/Intune deployment, live Azure administrator matrix, diagnostics review, independent review, exact Store candidate |
| Read-only default | Implemented | Public/default builds expose no Key Vault mutation or Azure role-assignment path; all governed mutation code is dual-gated by an accepted-build release switch and exact machine policy | Independent policy/security validation |
| Optional governed write mode | Implemented internally; release-gated | Separate secret create/version, RSA-3072 software-key version, and self-signed certificate-policy operations with exact policy, fresh reauthentication/authorization, Windows verification, immutable preview, one-time confirmation, concurrency, and hash-chained value-free audit controls | Disposable live-Azure matrix, accepted ADR, independent review, and explicit release enablement |
| Create and manage Azure Key Vaults and keys | Main-roadmap follow-on; not in current delivery target | AB#6052 defines future Key Vault resource creation/editing, broader key lifecycle and rotation-policy management, and separately controlled destructive actions | Explicit product-owner advancement, Story decomposition, design and security approval, implementation, controlled Azure tests, and release evidence |
| Notification-area/background operation | Current correction implemented locally; package validation open | Explicit close behavior, lock-on-hide tray lifecycle, safe status, exit cleanup, and opt-in metadata-only background sync for every enabled ready connection; exact candidate minimize/restore and exact-public startup passed in RDP | Install the corrected package; prove all three connections continue refreshing; complete the sleep/session-lock/network/token-expiry matrix and independent validation |
| Password-manager UI research/redesign | Atlas released; validation open | Three complete lifecycle directions, product-owner selection of Atlas, production Atlas screen hierarchy, structural/rendered regressions, exact installed Windows 11 RDP walkthroughs, and exact-public secure-unlock rendering | Complete exact-public every-state keyboard/screen-reader and representative-user testing plus independent validation |
| Browser autofill/password-vault integration | Implemented locally, validation open | Toolbar-only Chromium/Firefox extension source, strict protocol, authenticated native host/broker, exact mappings, protected machine policy, desktop confirmation, fresh Windows verification, audit, MSI registration, tests | Signed extension distribution, independent review, live installed-browser/compromise/revocation/usability/AT evidence |
| Private-endpoint Key Vault connectivity | Not started; backlog only | ADO AB#6192–6225 define research, architecture, implementation, validation, and documentation work | Supported connectivity architecture, ADR, threat model, connector/profile/routing implementation, representative private-network tests, independent security validation, and support guidance |
| CyberArk source | Future roadmap; unsupported source prototype | Privilege Cloud ADR/threat model, isolated provider and automated tests remain in private source; the Windows release UI is disabled | Governed live tenant, product approval, independent review, and separate release evidence |
| iPhone/iOS and Android/Google apps | Future roadmap; source prototypes only | Shared search/retrieval and native-security source remains in the private repository | Separate mobile release plan, physical-device/accessibility matrices, signing, closed testing, privacy/data-safety, and store acceptance |

## Story status and plan mapping

Status snapshot: 2026-08-11. **Delivered** means present in the current public Preview unless the
note explicitly limits it to policy/process delivery. See [`plan.md`](plan.md) for phase scope and
exit criteria.

| ID | Story | Status | Primary plan phase |
| --- | --- | --- | --- |
| 1.1 | Scaffold solution | Delivered | Existing; validate in Phase 14 |
| 1.2 | Application shell | Delivered | Existing; redesign in Phase 9 |
| 2.1 | Connect an Azure identity | Delivered | Phase 2 live validation |
| 2.2 | Connect multiple identities | Delivered | Phase 2 live validation |
| 2.3 | Reauthentication | Included in Preview 8; validation open | Phase 2 |
| 2.4 | Disable and re-enable an identity | Included in Preview 8; validation open | Phase 2 |
| 3.1 | Discover subscriptions | Delivered | Phase 6 permission completion |
| 3.2 | Discover Key Vaults | Delivered basic path | Phase 6 permission completion |
| 3.3 | Map access paths | Included in 0.2 Preview; validation open | Phase 6 validation |
| 3.4 | Configure discovery inclusion | Included in 0.2 Preview; validation open | Phase 6 validation |
| 4.1 | Index secret metadata | Delivered | Phase 7 lifecycle completion |
| 4.2 | Search by name | Delivered | Phase 9 usability validation |
| 4.3 | Filter search | Delivered | Phase 9 usability validation |
| 4.4 | Reconcile removed provider objects | Included in Preview 8; validation open | Phase 7 |
| 5.1 | Reveal a secret | Delivered | Phases 3 and 14 validation |
| 5.2 | Secure copy | Delivered | Phases 3 and 14 validation |
| 5.3 | Mask values | Delivered | Phase 9 usability validation |
| 6.1 | Cache selected secret | Delivered | Phases 3 and 7 completion |
| 6.2 | Expire cached secret | Delivered | Phases 3 and 7 completion |
| 6.3 | Purge cache | Implemented locally, validation open | Phases 3 and 7 completion |
| 7.1 | Redacted diagnostics | Delivered | Phase 14 independent validation |
| 7.2 | Security policy | Delivered as policy/process | Phases 14 and 15 operation |
| 7.3 | Dependency scanning | Delivered | Phase 14 continuous operation |
| 7.4 | Schema upgrade validation | In progress locally | Phases 3 and 7 |
| 7.5 | Free trusted Windows distribution | In progress; MSIX implemented | Phase 14 |
| 7.6 | Complete workspace resource assignment | Included in 0.2 Preview; validation open | Phase 7 validation |
| 8.1 | Apple platform security validation | Implemented and merged; live/independent validation open | Phase 13 |
| 8.2 | iPhone/iOS application and App Store release | Implemented and merged; signing/store gates open | Phase 13 |
| 8.3 | Android application and Google Play release | Implemented and merged; signing/store gates open | Phase 13 |
| 8.4 | Mobile autofill feasibility | Native prototypes implemented; enabled live-framework evidence open | Phase 13 |
| 9.1 | Secure first-run wizard | Implemented locally, validation open | Phase 3 |
| 9.2 | Mandatory local encryption verification | Implemented; independent review open | Phases 3 and 14 |
| 9.3 | Isolated Azure authentication contexts | Implemented; live matrix open | Phases 2 and 14 |
| 9.4 | Human and workload identity choices | In progress locally | Phase 4 |
| 9.5 | Discover and provision workload identities | Discovery prototype locally | Phase 5 |
| 9.6 | Enforce machine-managed enterprise access policy | Implemented locally, validation open | Phases 4, 6, and 14 |
| 10.1 | Discover vaults by selected access path | Included in 0.2 Preview; validation open | Phase 6 validation |
| 10.2 | Read-only by default | Delivered | Phases 6 and 14 validation |
| 10.3 | Explicit write mode | Implemented internally; validation/release gate open | Phase 8 |
| 11.1 | Continue securely in the notification area | Included in 0.2 Preview; validation open | Phase 10 validation |
| 12.1 | Research password-manager interface patterns | In progress; research and 4 concepts complete | Phase 9 participant validation |
| 13.1 | Browser extension and native messaging feasibility | Implemented locally, validation open | Phase 11 |
| 13.2 | Browser password-vault interoperability | Research complete; private-store access prohibited | Phase 11 |
| 14.1 | CyberArk source integration | Future roadmap; unsupported source prototype | Future provider roadmap |
| 15.1 | Consent-based Preview feedback | Delivered as process | Phase 15 operation |
| 15.2 | Evidence-based GA feedback gate | In progress | Phase 15 |
| 15.3 | Operational support and lifecycle readiness | In progress | Phase 15 |
| 15.4 | Legal/privacy release controls and approval | In progress | Phases 14 and 15 |
| 16.1–16.15 | Governed private-endpoint Key Vault connectivity (ADO AB#6197–6225, odd IDs) | Not started; backlog only | Future private-connectivity roadmap |

## Story source and acceptance traceability

The status and phase table above remains canonical. This companion matrix supplies the source
boundary and acceptance proof required by Phase 0 for every story. A future or on-hold story points
to its governing design boundary until production source exists; that is evidence of non-delivery,
not implementation.

| ID | Current source or governing evidence | Acceptance proof required |
| --- | --- | --- |
| 1.1 | `VaultProspector.sln`, `Directory.Build.props`, `scripts/Build.ps1`, `.github/workflows/ci.yml` | Locked restore, formatting, dependency, supported-platform build, and all-project test gates on the exact release source. |
| 1.2 | `src/VaultProspector.App/App.axaml.cs`, `Views/MainWindow.axaml` | App/UI automation plus keyboard, scaling, contrast, screen-reader, lifecycle, and exact-candidate evidence. |
| 2.1 | `MsalIdentityProvider`, `IdentityService`, `MainViewModel` | Automated authentication boundaries and live tenant/consent/MFA/Conditional Access/cancel matrix. |
| 2.2 | App-owned MSAL account/cache implementation and identity UI | Multi-account/multi-tenant isolation, restart, removal, and live tool-context independence. |
| 2.3 | `IdentityService.ReauthenticateAsync`, `MsalIdentityProvider.ReauthenticateAsync`, identity UI | Ready/interaction-required/cancel/failure tests and live reauthentication against exact candidate. |
| 2.4 | Identity enable/disable services, repository state, identity UI | Disabled identity blocks sync/value access; re-enable restores only authorized behavior; live expiry/revocation matrix. |
| 3.1 | `AzureVaultProvider`, encrypted repository subscription records, Identities UI | Provider/partial-failure tests and live selected-identity subscription inventory. |
| 3.2 | `AzureVaultProvider`, vault/access records, Identities UI | Metadata-only enumeration, partial authorization failure, and live multi-vault matrix. |
| 3.3 | `VaultAccess`, search rows, Identities/Search source context | Tests and live evidence that identity, tenant, subscription, vault, and access state remain accurate. |
| 3.4 | Subscription/vault selection persistence and provider exclusion inputs | Include/exclude/re-enable/reconciliation tests plus live scoped synchronization. |
| 4.1 | Provider object discovery, encrypted metadata repository, and `VaultProspector.PerformanceProbe` | Secret/key/certificate metadata tests, no implicit value retrieval, controlled 50,000-object baseline, and representative-device/live-provider scale evidence. |
| 4.2 | `SearchService`, deterministic preferred-access query, Search UI, and performance probe | Search correctness and under-one-second controlled plus supported-device performance evidence. |
| 4.3 | `SearchRequest`, repository filters, Search UI | Combined filter correctness, empty/error states, keyboard and representative-user evidence. |
| 4.4 | Complete/partial discovery reconciliation in provider and repository | Tombstone/preserve/favorite/history/cache-reference tests and live permission-loss/removal evidence. |
| 5.1 | `SecretAccessService`, provider retrieval, Windows verification, reveal UI | Verification-before-retrieval, disposal/masking, live Windows Hello, Key Vault, accessibility, and audit evidence. |
| 5.2 | `AvaloniaClipboardService`, `SecretAccessService`, copy UI | Lease race/ownership/timeout/exit tests and live clipboard/history behavior. |
| 5.3 | Sensitive-presentation epoch, timed masking, lifecycle locks, Search UI | Timer/cancel/background/session-boundary tests and live task-switch/accessibility evidence. |
| 6.1 | `EncryptedFileValueStore`, cache policy, explicit cache command | Authenticated-envelope/storage/tamper/verification/policy tests and live retained-value inspection. |
| 6.2 | Envelope expiry and fingerprint validation | Clock-boundary, stale-source, tamper, restart, and live expiry evidence. |
| 6.3 | Item/identity/vault/workspace/all purge services and UI | Scope isolation, historical-access coverage, continuation, confirmation, restart, and filesystem cleanup evidence. |
| 7.1 | `RedactingDiagnosticSink` and centralized lifecycle/error events | Allowlist/redaction/adversarial tests plus independent log/support-bundle inspection. |
| 7.2 | `SECURITY.md`, threat models, read-only enforcement, release runbooks | Independent review, vulnerability-response exercise, policy operation, and exact-release sign-off. |
| 7.3 | `Test-VulnerablePackages.ps1`, locked packages, CI/release workflows | Continuous direct/transitive vulnerability and full-history secret scans on every release source. |
| 7.4 | Repository schema migrations, fail-closed initialization, rotation/recovery engine | Every published-schema upgrade, future/corrupt/wrong-key rejection, rollback, reinstall, and exact-candidate matrix. |
| 7.5 | Artifact-signing design and protected release workflow | Timestamped trusted signatures on every executable/library/MSI, clean-machine trust, and provenance match. |
| 7.6 | Workspace links, policy override, repository transactions, Workspaces UI | Every resource type, removal, scope-isolation, cache-policy, migration, and live workflow evidence. |
| 8.1 | Project charter, Phase 13 platform-security requirements | Apple threat model and live Keychain/Secure Enclave/LocalAuthentication/lifecycle/accessibility evidence. |
| 8.2 | Phase 13 iOS/store scope | Production iOS app, signed TestFlight/App Store artifacts, privacy review, store acceptance, and live matrix. |
| 8.3 | Phase 13 Android/store scope | Production Android app, signed closed-test/Play artifacts, data-safety review, store acceptance, and live matrix. |
| 8.4 | Phase 13 autofill scope | Platform eligibility prototypes, origin/mapping/user-presence threat model, and live framework evidence. |
| 9.1 | `MainViewModel.InitializeAsync`, first-run Identities workflow, local-data recovery UI | Automated setup boundaries plus live Windows Hello/Entra/keyboard/screen-reader/independent/exact-release evidence. |
| 9.2 | SQLCipher repository, AES-GCM store, DPAPI keys, ADR-0011 rotation/recovery | Cryptographic/tamper/crash/migration tests, live power-loss/reinstall, and independent review. |
| 9.3 | Explicit MSAL credentials and isolated app-owned caches | Automated cache/account isolation and live CLI/PowerShell/IDE/multi-tenant independence. |
| 9.4 | Typed workload credentials, host detection, identity lifecycle UI | Contract/negative/redaction/rotation/revocation tests plus live Azure and independent review. |
| 9.5 | `WorkloadIdentityDiscoveryService`, authorization evaluator, non-mutating plan UI | Permission/deny/condition tests, governed-write gate, live Azure least-privilege matrix, and independent review. |
| 9.6 | `EnterprisePolicySnapshot`, `WindowsRegistryEnterprisePolicy`, application-service enforcement, Settings status, ADMX/ADML, packaging, and `Test-EnterprisePolicyReadiness.ps1` | Parser/type/deny-before-network tests; governed Group Policy/Intune deployment; live allowed/denied Azure and CyberArk matrix; safe diagnostics inspection; independent review; exact signed-candidate validation. |
| 10.1 | Selected identity/scope provider flow and permission-aware vault UI | Human/workload live Azure visibility/list/read-deny matrix and independent redaction validation. |
| 10.2 | Read-only provider surface and policy-disabled UI | Static/behavioral proof of no mutation plus least-privilege and independent policy review. |
| 10.3 | ADR-0010, governed-write threat model, `GovernedAzureMutationService`, Azure provider commands, exact machine policy, SQLCipher schema-v7 hash-chained audit, and default-hidden Atlas controls | Accepted review plus live least-privilege/deny/concurrency/recovery validation, independent sign-off, and exact release proof. |
| 11.1 | Tray lifecycle in `App.axaml.cs`, background policy, Windows boundary monitor | Automated cancellation/metadata-only tests and installed tray/sleep/session/network/token/accessibility matrix. |
| 12.1 | `docs/design` research, four-concept React prototype, usability protocol | Representative participants, assistive-technology results, recorded selection, production implementation, exact-candidate validation. |
| 13.1 | `browser-extension`, BrowserProtocol/BrowserHost/Platform broker source, encrypted mapping/audit service and UI, MSI native-host registration, ADR-0014 and browser threat model | Signed extension/native host candidate, installed Chrome/Edge/Firefox matrix, independent review, update/compromise/revocation exercise, usability/AT evidence, and exact-release browser review. |
| 13.2 | Feasibility spike documents supported public extension/native-messaging APIs and prohibits browser credential-database access; no import/export/sync source exists | Explicit-consent product decision for any future supported handoff, live tests, privacy review, and browser distribution approval. |
| 14.1 | ADR-0015, CyberArk threat model, dedicated provider/contracts/UI, DPAPI credential store, SQLCipher schema v6, verified retrieval, fail-closed local revoke/remove, and value-free audit | Automated provider/application/platform/persistence/accessibility evidence; governed live tenant permission/failure/audit matrix; independent review; exact signed release. |
| 15.1 | `preview-feedback.md`, privacy notice, HCS-governed intake and triage process | Sanitized operational records proving notice, consent, privacy boundary, response targets, and escalation. |
| 15.2 | Readiness G-01 evidence and go/no-go process | Required build/install/upgrade coverage, report disposition, blocker closure, exact-candidate validation, and named approval. |
| 15.3 | Support lifecycle, operations runbook, readiness manifest/validator, Dependabot, scheduled monitor | Named backup operator, retained successful hosted runs, incident/withdrawal/recovery exercise, Store trust evidence, and exact-candidate review. |
| 15.4 | Deterministic lock inventory/notices, technical privacy statement, package/store metadata, CI drift gate, and packaged disclosure files | Exact signed-candidate SBOM/file and upstream-obligation reconciliation, approved public privacy URL and store declarations, diagnostics-package license disposition, and named legal/privacy approval. |
| 16.1–16.15 | ADO AB#6197–6225 and the future private-connectivity plan; no implementation, ADR, threat model, test, or deployment evidence exists in this repository | Research and select a supported architecture; define profiles, mappings, authentication, encryption, and health UX; implement governed connector/routing; validate representative private-network topologies and security boundaries; publish deployment/support guidance. |

## Open implementation and release-gate traceability

This table is the Phase 0 control for the story that was **Partial** when this audit began and the
remaining **Not started** story. It remains until their exact-artifact acceptance evidence closes
so a local source status cannot be mistaken for delivery.

| ID | Current evidence | Missing production or validation work | Primary phase | Acceptance evidence |
| --- | --- | --- | --- | --- |
| 9.1 | `MainViewModel.InitializeAsync`, the locked/local-data recovery surfaces, and the first-run Identities workflow enforce local verification before repository initialization and separate it from Microsoft sign-in. | Live Windows Hello outcomes, tenant consent/MFA/Conditional Access matrix, complete keyboard/screen-reader workflow, independent review, exact released artifact. | Phase 3; live gates in Phase 14 | Automated unlock/setup state tests; interactive Windows Hello and Entra matrix; keyboard/Narrator/NVDA task evidence; independent-review disposition; exact-artifact release record. |
| 10.3 | Four separate production commands, exact operation/vault policy, fresh reauthentication and effective data-action checks, Windows verification, immutable value-free previews, one-time confirmation/replay denial, optimistic secret-version concurrency, and SQLCipher schema-v7 hash-chained audit are implemented with automated negative tests. Controls remain hidden unless both release and machine-policy gates pass. | Disposable live-Azure least-privilege/deny/concurrency/recovery matrix, accepted ADR, independent review, explicit release enablement, and exact-artifact evidence. | Phase 8; release gates in Phase 14 | Accepted design review; live matrix; redacted audit evidence; independent sign-off; exact-artifact release record. |

## Epic 1 — Application foundation

### Story: Scaffold solution

As a contributor, I need a maintainable solution structure so that domain, application, provider, storage, and platform code can evolve independently.

Acceptance criteria:

- Projects compile on supported desktop development platforms.
- Dependency direction is enforced.
- Formatting and static-analysis rules run in CI.

Source evidence: `VaultProspector.sln`, `src/VaultProspector.App/VaultProspector.App.csproj`

Implementation status: Delivered in `0.1.1-preview.1` and refreshed in `0.2.0-preview.1`. The desktop source, tests,
locked dependency graphs, HCS-runner GitHub workflows, and self-contained packaging are migrated to
.NET 10 LTS. Exact PR validation ADO build `281` passes Windows, secret scan, Android, and native iOS;
merge-commit, clean-machine, signed-artifact, and supported-platform validation remain open in
Phase 14.

### Story: Application shell

As a user, I need a clear application shell so that I can navigate identities, workspaces, search, settings, and synchronization status.

Acceptance criteria:
- Main shell displays identities, workspaces, search, settings, and sync status.
- Core navigation paths are implemented and accessible.

Source evidence: `src/VaultProspector.App/Views/MainWindow.axaml`, `src/VaultProspector.App/ViewModels/MainWindowViewModel.cs`
Acceptance tests: `tests/VaultProspector.App.Tests/MainWindowViewModelTests.cs`

Implementation status: Delivered in `0.1.1-preview.1`; redesign and final accessibility validation remain open.

## Epic 2 — Identity and authentication

### Story: Connect an Azure identity

As a user, I need to sign in with Microsoft Entra ID so that Vault Prospector can access resources I am already authorized to use.

Acceptance criteria:

- Interactive authentication uses supported Microsoft identity libraries.
- Tokens are not logged.
- The identity is assigned a local identifier and friendly label.
- Removal purges the associated token cache entry.

Source evidence: `src/VaultProspector.Providers.Azure/MsalIdentityProvider.cs`
Acceptance tests: `tests/VaultProspector.Providers.Azure.Tests/MsalIdentityProviderTests.cs`

Implementation status: Delivered in `0.1.1-preview.1`; the broader live tenant matrix remains open.

### Story: Connect multiple identities

As a consultant, I need more than one Azure identity so that I can search employer, customer, personal, and demo environments.

Acceptance criteria:
- Multiple identities can be connected concurrently.
- Each identity retains its isolated token cache and context.

Source evidence: `src/VaultProspector.Providers.Azure/MsalIdentityProvider.cs`, `src/VaultProspector.Application/Services.cs`
Acceptance tests: `tests/VaultProspector.Providers.Azure.Tests/AuthenticationConfigurationTests.cs`

Implementation status: Delivered in `0.1.1-preview.1`; broader multi-account live validation remains open.

### Story: Reauthentication

As a user, I need clear interaction-required states so that I understand when a sync cannot proceed without signing in again.

Acceptance criteria:
- App detects and displays interaction-required states.
- Explicit reauthentication flow is provided for expired or invalid tokens.

Source evidence: `src/VaultProspector.Providers.Azure/MsalIdentityProvider.cs`

Implementation status: Implemented on `main` but not yet included in a public Preview.

### Story: Disable and reenable an identity (post-preview)

As a user, I need to suspend an identity without deleting its offline metadata and explicitly reauthenticate it when policy requires interaction.

Acceptance criteria:
- Identity can be disabled, pausing sync without deleting metadata.
- Re-enabling triggers authentication if required.

Source evidence: `src/VaultProspector.Providers.Azure/MsalIdentityProvider.cs`

Implementation status: Implemented on `main` but not yet included in a public Preview.

## Epic 3 — Azure discovery

### Story: Discover subscriptions

As a user, I need to see subscriptions available through each identity so that I can select the environments to index.

Acceptance criteria:
- App lists available subscriptions per identity.
- User can select specific subscriptions for discovery.

Source evidence: `src/VaultProspector.Providers.Azure/AzureVaultProvider.cs`

Implementation status: Basic discovery is delivered in `0.1.1-preview.1`. Explicit per-identity
subscription inclusion is implemented locally, persisted, user-accessible, and respected before
subsequent synchronization; it is included in `0.2.0-preview.1` and needs live validation.

### Story: Discover Key Vaults

As a user, I need the app to find Key Vault resources across selected subscriptions.

Acceptance criteria:
- App enumerates Key Vaults within selected subscriptions.
- Results distinguish between accessible and inaccessible vaults.

Source evidence: `src/VaultProspector.Providers.Azure/AzureVaultProvider.cs`

Implementation status: Basic discovery is delivered; permission-aware results remain open in Phase 6.

### Story: Map access paths

As a user, I need to know which connected identity can access a vault so that the app uses the correct authentication context.

Acceptance criteria:
- UI displays which identity grants access to each vault.
- App routes requests through the correct identity automatically.

Source evidence: `src/VaultProspector.Providers.Azure/AzureVaultProvider.cs`

Implementation status: Included in `0.2.0-preview.1`; validation remains open. The UI displays each vault's selected
identity, tenant, subscription, observed metadata-list permissions, explicit value-read
non-probing, and policy-disabled write state.

### Story: Configure discovery inclusion (post-preview)

As a user, I need to include or exclude subscriptions and vaults before subsequent synchronization so that the local index follows an explicit scope policy.

Acceptance criteria:
- Configuration allows explicit include/exclude rules for subscriptions and vaults.
- Sync engine respects these inclusion scopes.

Implementation status: Included in `0.2.0-preview.1`; validation remains open for both subscription and vault rules.
Rules are persisted per identity/access path and applied before provider metadata enumeration.
Excluded scope records are retained so the user can reverse a choice.

## Epic 4 — Index and search

### Story: Index secret metadata

As a user, I need secret names, tags, versions, dates, and vault context indexed without retrieving secret values.

Acceptance criteria:
- Metadata is synced and indexed locally.
- Sync operation never retrieves actual secret values.

Source evidence: `src/VaultProspector.Application/Services.cs`

Implementation status: Delivered in `0.1.1-preview.1`; removal reconciliation is implemented
locally. The controlled G-05 baseline now persists 50,000 synthetic metadata objects in 6.6
seconds and enforces startup/reopen, cancellation, memory, and storage limits. Live-provider,
representative-device, UI, and exact signed-candidate evidence remains open.

### Story: Search by name

As a user, I need instant name search across all selected vaults.

Acceptance criteria:
- Search is performed locally and provides instant results.
- Matches are found across all selected vaults.

Source evidence: `src/VaultProspector.Domain/Models.cs`,
`src/VaultProspector.App/ViewModels/MainViewModel.cs`

Implementation status: Delivered in `0.1.1-preview.1`. The controlled 50,000-object G-05 baseline
passes at 262 ms p95 and 275 ms maximum after remediation of repeated SQLCipher KDF work and
nondeterministic grouped access-path selection. Representative-device, populated-UI,
accessibility, and exact signed-candidate repetition remains open.

### Story: Filter search

As a user, I need filters for workspace, tenant, subscription, vault, identity, type, expiry, and staleness.

Acceptance criteria:
- Search results can be filtered by metadata properties.
- Filters correctly refine the displayed list.

Source evidence: `src/VaultProspector.Domain/Models.cs`

Implementation status: Delivered in `0.1.1-preview.1`; usability validation remains open in Phase 9.

### Story: Reconcile removed provider objects (post-preview)

As a user, I need incremental synchronization to tombstone objects that Azure no longer returns without erasing unrelated history.

Acceptance criteria:
- Removed provider objects are marked as tombstoned locally.
- Unrelated history and offline cache remain unaffected.

Implementation status: In progress locally and not yet released or validated.

## Epic 5 — Secure retrieval

### Story: Reveal a secret

As a user, I need to retrieve a secret value only when I choose to reveal it.

Acceptance criteria:
- Secret value is retrieved from Azure only upon explicit user action.
- UI indicates when a reveal operation is in progress.

Source evidence: `src/VaultProspector.Providers.Azure/AzureVaultProvider.cs`

Implementation status: Delivered in `0.1.1-preview.1`; final security and live validation remain open.

### Story: Secure copy

As a user, I need to copy a value and have the application clear it from the clipboard after a short interval.

Acceptance criteria:
- Copied secrets are placed on the clipboard.
- App clears clipboard automatically after a defined interval.

Source evidence: `src/VaultProspector.App/AvaloniaClipboardService.cs`

Implementation status: Delivered in `0.1.1-preview.1`; final platform validation remains open.

### Story: Mask values

As a user, I need secret values hidden by default to reduce shoulder-surfing risk.

Acceptance criteria:
- Values are visually masked/hidden by default in the UI.
- Toggle exists to unmask values locally when explicitly required.

Source evidence: `src/VaultProspector.Domain/SensitiveValue.cs`, `src/VaultProspector.App/Views/MainWindow.axaml`

Implementation status: Delivered in `0.1.1-preview.1`; final usability validation remains open.

## Epic 6 — Offline access

### Story: Cache selected secret

As a user, I need to opt a specific secret into encrypted offline storage.

Acceptance criteria:
- User can explicitly opt-in to cache a secret value.
- Cached value is stored with AES-GCM encryption.

Source evidence: `src/VaultProspector.Infrastructure/EncryptedFileValueStore.cs`

Implementation status: Delivered in `0.1.1-preview.1`; migration and independent-review gates remain open.

### Story: Expire cached secret

As a security-conscious user, I need cached values to expire automatically.

Acceptance criteria:
- Cached secrets have a defined expiry policy.
- Expired secrets are automatically purged or invalidated.

Source evidence: `src/VaultProspector.Infrastructure/EncryptedFileValueStore.cs`

Implementation status: Delivered in `0.1.1-preview.1`; migration and independent-review gates remain open.

### Story: Purge cache

As a user, I need to immediately purge cached values at multiple scopes.

Acceptance criteria:
- Manual purge action clears cached secrets at identity or workspace scope.
- Data is securely removed from local storage.

Source evidence: `src/VaultProspector.Infrastructure/EncryptedFileValueStore.cs`,
`src/VaultProspector.Application/Services.cs`,
`src/VaultProspector.App/ViewModels/MainViewModel.cs`

Acceptance tests: `tests/VaultProspector.Infrastructure.Tests/EncryptedPersistenceTests.cs`,
`tests/VaultProspector.Application.Tests/ApplicationServiceTests.cs`,
`tests/VaultProspector.App.Tests/OnboardingTests.cs`

Implementation status: Implemented locally for item, identity, vault, workspace, and global scopes;
final platform and exact-candidate validation remain open.

## Epic 7 — Security and governance

### Story: Redacted diagnostics

As a user, I need useful diagnostics that never include secret values or tokens.

Acceptance criteria:
- Diagnostic logs and reports redact all secrets and tokens automatically.
- Generated files are safe for external sharing.

Source evidence: `src/VaultProspector.Infrastructure/RedactingDiagnosticSink.cs`

Implementation status: Delivered in `0.1.1-preview.1`; independent validation remains open.

### Story: Security policy

As an administrator, I need policy to disable offline value caching.

Acceptance criteria:
- Administrator policy can disable offline value caching globally.
- App enforces policy by blocking cache opts-in and purging existing caches.

Implementation status: Delivered as documented policy and application enforcement; operational validation remains open.

### Story: Dependency scanning

As a maintainer, I need automated dependency and secret scanning in CI.

Acceptance criteria:
- CI pipeline blocks merges if vulnerabilities or checked-in secrets are found.
- Scans run on every pull request and release build.

Implementation status: Delivered in CI; continuous operation remains required through GA.

### Story: Schema upgrade validation (post-preview)

As a maintainer, I need forward-only encrypted database migrations tested against every previously published schema before an upgrade release.

Implementation status (2026-07-17): the internal version-1-to-2 migration is transactional and
tested. Startup rejects future versions, corrupt or wrong-key databases, incomplete current
schemas, and invalid foreign-key relationships without silent repair or plaintext fallback.
Every actually published schema must still be added to the upgrade matrix before its successor is
released; key rotation, backup/restore, and device replacement remain open under G-03.

### Story: Free trusted Windows distribution (post-preview)

As a Windows user, I need a publicly trusted installation path that does not require HCS to
purchase a code-signing certificate.

Acceptance criteria:
- The exact release source produces a reproducible MSIX with checksum, SBOM, and provenance.
- Microsoft Store accepts and signs the package after certification.
- The Store-signed package installs, launches, upgrades, and uninstalls on clean supported Windows.
- Direct MSI/ZIP downloads remain clearly labeled unsigned and retain integrity evidence.

Implementation status: MSIX packaging is implemented. Partner Center identity reservation,
certification, and clean-machine Store-package validation remain.

### Story: Complete workspace resource assignment (post-preview)

As a user, I need direct tenant and subscription assignment plus editable workspace-specific cache policy. The preview supports identity and vault assignment.

Acceptance criteria:
- Workspaces support direct assignment of tenants and subscriptions.
- Each workspace allows editable, separate cache policies.

Implementation status: Included in `0.2.0-preview.1`; validation remains open. Identity, tenant, subscription, and
vault links are user-accessible. Each workspace has an editable encrypted-cache enablement,
maximum lifetime, and clipboard policy; Windows verification remains mandatory. Workspace
deletion removes its links transactionally and purges workspace-scoped offline values through the
application workflow.

## Epic 8 — iPhone and Google mobile applications (coming soon)

These applications are coming soon after the Windows distribution path. They remain out of scope for the current Windows desktop preview until their separate mobile security and store acceptance criteria are complete.

### Story: Apple platform security validation

As a security reviewer, I need macOS and iOS Keychain, Secure Enclave, LocalAuthentication, background-state, and screenshot protections validated before an Apple build is distributed.

Acceptance criteria:
- Apple-specific platform security protections are implemented.
- Security review validates Keychain and LocalAuthentication compliance.

Implementation status: Implemented and merged; live and independent validation remains open.
The iOS host uses a device-only, access-controlled Keychain key, LocalAuthentication, encrypted
local storage, backup exclusion, local-only expiring pasteboard content, lifecycle locking,
background covering, and active-capture lockout. Windows reference-pack compilation and automated
session-policy tests and macOS simulator CI pass; physical-device enrollment/passcode/migration/reinstall
matrices, accessibility, and independent review remain mandatory.

### Story: iPhone/iOS application and App Store release (coming soon)

As an iOS user, I need a mobile-safe search and retrieval experience that passes a separate threat model, entitlement review, privacy declaration, signing, TestFlight validation, and App Store review.

Acceptance criteria:
- iOS application passes TestFlight and App Store reviews.
- Separate threat model and privacy reviews are approved.

Implementation status: Implemented and merged; signing and store gates remain open. The iOS
application provides native unlock, MSAL identity connection, synchronization, encrypted metadata
search, bounded reveal, and bounded clipboard copy. It includes a privacy manifest and unsigned
simulator CI path. Production Entra callback registration, signed archives, TestFlight,
physical-device validation, privacy review, and App Store acceptance remain open.

### Story: Android application and Google Play release (coming soon)

As an Android user, I need a mobile-safe experience using Android Keystore and BiometricPrompt that passes a separate threat model, data-safety declaration, target-SDK review, signing, closed testing, and Google Play review.

Acceptance criteria:
- Android application passes Google Play review.
- Separate threat model and data-safety reviews are approved.

Implementation status: Implemented and merged; signing and store gates remain open. The Android
application provides the same mobile workflow with Android Keystore, BiometricPrompt,
`FLAG_SECURE`, sensitive clipboard labeling, and explicit backup/data-transfer exclusions. Local
and exact-PR-head Release App Bundle builds pass. Protected upload signing,
closed testing, physical-device validation, data-safety review, and Google Play acceptance remain
open.

### Story: Mobile autofill feasibility

As a product owner, I need Apple Password AutoFill and Android Autofill framework capabilities validated without claiming that arbitrary Azure secrets can be exposed through unsupported credential-provider APIs.

Acceptance criteria:
- Autofill framework capabilities are tested on iOS and Android.
- Findings determine safe integration patterns for specific secrets.

Implementation status: Native feasibility prototypes are implemented; enabled live-framework
evidence remains open. Shared analysis accepts only explicit credential fields for one canonical,
default-port HTTPS origin. Final value release additionally requires an exact saved mapping,
foreground invocation, secret object, and fresh user verification. A package-disabled Android
`AutofillService` parses bounded `AssistStructure` input, never saves observed values, and returns
no dataset. An embedded Apple credential-provider extension validates service identifiers, always
requires interaction, shares no app database/Keychain group, and returns no credential. Forty-four
mobile policy/package tests, a Release Android App Bundle, and iOS reference-pack compilation pass.
Digital Asset Links/package-signature validation, encrypted one-record exchange, signed physical
devices, real-framework negative-origin tests, accessibility, and independent review remain
mandatory before either component can deliver a value. No arbitrary secret, key, certificate,
wildcard origin, background fill, import, or save behavior is offered.

## Epic 9 — Secure first-run setup and identity architecture (highest priority)

### Story: Secure first-run wizard

As a new user, I need setup to establish the local unlock boundary and Azure connection method before any vault metadata is stored.

Acceptance criteria:

- The wizard explains the difference between unlocking Vault Prospector and authenticating to Azure.
- Windows Hello or an equivalent platform-backed local verification mechanism protects high-risk local actions.
- Microsoft Entra authentication uses MSAL and the system browser or Windows broker; Vault Prospector never collects an Entra password or implements its own MFA.
- MFA, Conditional Access, passwordless credentials, and FIDO requirements remain enforced by Microsoft Entra and Windows.
- An organization's external identity provider is supported through its Microsoft Entra federation; direct support for another provider requires a separate connector and threat model.
- Setup fails closed if protected key storage or mandatory metadata encryption is unavailable.

Implementation status (2026-07-24): included in `0.2.0-preview.1`; validation remains open. Product-registration
sign-in, custom-registration fallback, extra Key Vault consent, legacy client-ID settings
migration, and redacted recovery messages are implemented. After local verification, a first run
opens directly to a three-step Identities workflow that distinguishes local unlock from Microsoft
sign-in and metadata-only synchronization; the connection action names the selected identity
method. Local unlock fails closed for canceled, unavailable, not-configured, policy-disabled, and
failed Windows verification. Starting fresh after a protected local-data failure requires typed
confirmation, fresh verification, complete-state archival, and restart. Automated tests cover
those boundaries. Runtime keyboard/screen-reader usability, live Windows Hello outcomes, tenant
consent/MFA/Conditional Access scenarios, and independent review remain release gates.

### Story: Mandatory local encryption verification

As a security reviewer, I need proof that local metadata and every retained secret value are encrypted at rest so that no setup path can silently create plaintext storage.

Acceptance criteria:

- Metadata encryption is always enabled and has no disable toggle.
- Offline secret-value caching remains opt-in, but every cached value is encrypted whenever caching is enabled.
- Independent review confirms algorithms, key generation, key wrapping, file permissions, memory lifetime, and failure behavior.

Source evidence: `src/VaultProspector.Infrastructure/EncryptedFileValueStore.cs`, `src/VaultProspector.Infrastructure/EncryptedSqliteMetadataRepository.cs`

Implementation status: Implemented; independent review and remaining live recovery validation remain open.

### Story: Isolated Azure authentication contexts

As a multi-tenant user, I need Vault Prospector authentication to remain independent from Azure CLI, Azure PowerShell, IDE, and terminal sessions so that changing context elsewhere cannot redirect the app.

Acceptance criteria:

- Each connected human identity has an explicit label, tenant context, account identifier, and isolated MSAL cache entry.
- The app does not read Azure CLI, Azure PowerShell, or developer-tool context files as an authentication source.
- Removal purges the app-owned token-cache entry without altering another tool's session.

Source evidence: `src/VaultProspector.Providers.Azure/MsalIdentityProvider.cs`

Implementation status: Implemented; the complete live tenant and tool-isolation matrix remains open.

### Story: Human and workload identity choices

As an administrator, I need setup to distinguish interactive Entra accounts, service principals, and managed identities so that each access path uses an appropriate security model.

Acceptance criteria:

- Interactive Entra user authentication is the default desktop connection method.
- Managed identity is offered only when Vault Prospector runs on a supported Azure compute resource that supplies that identity; an ordinary desktop does not claim a managed identity it cannot possess.
- Service-principal support is an advanced workload option with certificate or workload-federation credentials preferred over client secrets.
- Each identity type has separate threat-model, storage, rotation, revocation, and audit requirements.

Implementation status: In progress locally. Interactive sign-in remains the default. Managed
identity is offered only after Azure-host endpoint or IMDS detection; profile creation then proves
ARM token acquisition. Certificate service principals require canonical tenant/client GUIDs, a
40- or 64-character hexadecimal thumbprint, a currently valid certificate with an accessible
private key, and successful ARM token acquisition before persistence. Federated profiles require a
readable projected-token file and store only its canonical path. Certificate and federated
replacements are validated before publication. Local revocation persists a fail-closed state,
removes app-owned credential references, purges discovered-vault offline copies, and cannot be
bypassed with stale caller state. Client secrets are rejected, workload removal does not open human
MSAL caches, encrypted persistence and negative tests pass, and fixed lifecycle events pass through
central redaction. Live Azure credential/issuer revocation evidence and independent review remain
incomplete.

### Story: Discover and provision workload identities (advanced administration)

As an authorized Azure administrator, I need setup to list eligible user-assigned managed identities or service principals and optionally prepare a new workload identity without granting broader access than requested.

Acceptance criteria:

- Listing identities occurs only after interactive authentication and only within subscriptions or directories the user is authorized to read.
- The UI distinguishes permission to view an identity, permission to attach or use it, and its Key Vault data-plane permissions.
- Creation is unavailable unless the signed-in account has the required managed-identity or application-management role.
- The initial/default setup never creates identities or Azure role assignments.

Source evidence: `src/VaultProspector.Providers.Azure/WorkloadIdentityDiscoveryService.cs`,
`src/VaultProspector.Providers.Azure/AzureAuthorizationEvidenceEvaluator.cs`,
`src/VaultProspector.App/ViewModels/MainViewModel.cs`,
`docs/adr/0013-report-effective-azure-authorization-evidence.md`, and
`docs/release-evidence/workload-authorization-evidence-2026-07-23.md`.

Implementation status: In progress locally. Managed-identity discovery honors an exact subscription
and returns application DTOs rather than SDK resources. Service-principal discovery requires an
enabled interactive identity and a separate explicit delegated `Application.Read.All` consent
action; Graph pagination is HTTPS-host constrained, redirect-disabled, bounded, and tested. The
Administration tab now performs an explicit read-only assessment for one candidate and exact Key
Vault. Administrator capabilities use exact-resource caller permissions; candidate data access
uses applicable inherited/transitive role assignments, role definitions, action exclusions, deny
assignments, child-scope behavior, and conditions. Conditional, access-policy, unreadable-deny, and
possible group-deny cases fail closed as unproven. Deterministic non-mutating managed-identity and
service-principal previews validate tenant/subscription/resource names and exact matching Key Vault
and role-definition types/scopes. There is deliberately no execution command. Independent review,
fresh write authorization, confirmation, encrypted audit, rollback, and live Azure tests remain
open.

## Epic 10 — Vault discovery and governed write operations

### Story: Discover vaults by selected access path

As a user, I need Vault Prospector to discover every Azure Key Vault visible to the selected identity and clearly report which vaults allow metadata listing, value reads, or future writes.

Acceptance criteria:

- Discovery runs separately for each selected human or workload identity.
- Results distinguish management-plane resource visibility from data-plane permissions for secrets, keys, and certificates.
- No secret values are retrieved during discovery or metadata synchronization.

Source evidence: `src/VaultProspector.Providers.Azure/AzureVaultProvider.cs`

Implementation status: Included in `0.2.0-preview.1`; validation remains open for human and configured workload
profiles. The UI separates management visibility from observed secret/key/certificate metadata
listing, never probes values during discovery, and states that writes are disabled. Live Azure
permission matrices and independent validation remain open.

### Story: Read-only by default

As a security-conscious user, I need every new connection and workspace to begin in read-only mode so that installing or connecting Vault Prospector cannot change Azure resources.

Acceptance criteria:

- The default product requests and uses only the permissions required for discovery, metadata listing, and explicitly requested value retrieval.
- Existing broad permissions on the signed-in Entra account do not automatically enable write controls.

Source evidence: `src/VaultProspector.Application/Services.cs`

Implementation status: Delivered in `0.1.1-preview.1`; final policy and security validation remain open.

### Story: Explicit write mode for secrets, keys, and certificates (future, high risk)

As an authorized operator, I need a separately enabled mode for supported create or update operations so that approved changes can be made without weakening the default read-only product.

Acceptance criteria:

- Supported operations are defined individually; there is no generic unrestricted write toggle.
- Enabling write mode requires policy approval, local verification, fresh Azure authorization when required, and a prominent elevated-state indicator.
- Every mutation shows identity, tenant, subscription, vault, object, operation, and expected effect before confirmation.
- Independent threat modeling and security review are complete before public release.

Implementation status: Design gate in progress. Proposed ADR-0010 and the governed-write threat
model define separate secret/key/certificate operation boundaries and explicitly reject the
previous unused generic-toggle placeholder. No Azure mutation code or public controls are enabled.
Internal and independent security review, production implementation, live Azure evidence, and
signed-candidate validation remain open.

## Epic 11 — Taskbar and background operation

### Story: Continue securely in the notification area

As a Windows user, I need an option for Vault Prospector to remain available from the taskbar notification area after I close the main window.

Acceptance criteria:

- The close action is configurable as exit, minimize to tray, or ask; it is never ambiguous.
- The tray icon clearly shows locked, syncing, interaction-required, error, and offline states.
- Background mode locks revealed values, clears sensitive UI state, and cannot reveal, copy, or cache a secret without foreground user verification.
- Exit actually terminates the process and clears temporary sensitive state.

Implementation status: Included in `0.2.0-preview.1`; validation remains open. Close behavior is explicitly Ask, Exit,
or Lock to notification area. Backgrounding cancels active work, invalidates sensitive
presentation, masks values, locks foreground access, and hides the taskbar entry. The tray exposes
state plus Show/Exit. Opt-in background work invokes metadata discovery only while hidden and
network-available. Production Windows handlers immediately lock on every session transition and
on suspend/resume, with automated mapping and sensitive-state tests. Live installed lifecycle,
sleep/resume, session transitions, battery, network, token expiry, and assistive-technology
validation remain open.

## Epic 12 — Desktop UI research and refinement

### Story: Research password-manager interface patterns

As a product designer, I need structured research into established password-manager and credential-vault interfaces so that Vault Prospector adopts understandable patterns without copying unsafe assumptions.

Acceptance criteria:

- Research covers onboarding, unlock, item lists, search, collections, identity context, security warnings, reveal/copy, autofill, audit history, and recovery.
- The review includes keyboard navigation, screen readers, color contrast, reduced motion, and high-risk confirmation patterns.
- Findings produce annotated workflows and prototypes for user testing before implementation.
- Security state and source identity remain visible even when simplifying the interface.

Implementation status: In progress. Comparative research, internal task analysis, four distinct
interactive concepts (Source-first, Search-first, Guided tasks, and Operations console), and an
eight-participant usability/accessibility protocol are complete. All concepts cover setup, search,
secret reveal, and settings. Representative-user sessions, assistive-technology evidence, final
selection, and production implementation remain open.

## Epic 13 — Browser and password-vault integration (research first)

### Story: Browser extension and native messaging feasibility

As a user, I want Vault Prospector to populate approved browser fields so that I can use selected credentials without manually copying them.

Acceptance criteria:

- Research covers Chromium and Firefox extension models, native messaging, extension signing, update security, permissions, and enterprise deployment.
- No arbitrary Azure secret is offered for autofill without an explicit mapping to an allowed origin and field purpose.

Implementation status: Implemented locally with automated protocol, host, broker, mapping, policy,
audit, UI, and installer validation. Signed packages, installed live-browser evidence, independent
review, compromise/revocation exercise, and representative-user/accessibility evidence remain.

### Story: Browser password-vault interoperability

As a user, I need the feasibility of integrating with browser password vaults assessed so that credentials are not duplicated or imported unsafely.

Acceptance criteria:

- The research documents supported browser APIs and prohibited/private storage access.
- Import, export, synchronization, and one-way handoff options are evaluated separately.
- Any prototype requires explicit consent, origin binding, policy control, local verification, and minimal value exposure.

Implementation status: Research complete. Public settings APIs do not expose a supported saved-
credential inventory, so private database access, scraping, and implicit import/export/sync are
prohibited. No browser password-vault interoperability beyond explicit one-time mapped fill is
implemented.

## Epic 14 — CyberArk provider (future roadmap)

### Story: CyberArk source integration

As an enterprise user, I need CyberArk available as a separately configured source so that I can discover and retrieve authorized objects without weakening Azure Key Vault isolation.

Acceptance criteria:

- An ADR selects the supported CyberArk product/API and authentication methods.
- CyberArk accounts, safes, objects, permissions, versions, and audit semantics map explicitly rather than being forced into an Azure-specific model.
- Provider credentials are isolated, encrypted, removable, and never logged.
- Contract, integration, security, and redaction tests cover the provider before release.

Implementation status: Unsupported future source prototype. The selected Privilege Cloud
integration has isolated profiles, credentials, metadata, provider models, and automated tests, but
the Windows release UI is disabled. It is not a current Windows acceptance dependency. A governed
tenant, explicit product approval, independent review, and separate release evidence are required
before it can enter a supported release.

## Epic 15 — Preview feedback and GA promotion

### Story: Consent-based Preview feedback

As a Preview evaluator, I need a clearly public, voluntary feedback channel that excludes sensitive
data so that I can report product outcomes without hidden telemetry or unsafe disclosure.

Acceptance criteria:

- HCS-governed standard Bug and Feature intake collect reproducible failures and structured
  core-task experience feedback using native issue types/fields and reserved workflow labels.
- The public notice makes submission an explicit publication action, requires
  synthetic/non-production use, and prohibits credentials, tokens, identifiers, Azure
  resource/object names, secret values, and unreviewed diagnostics or screenshots.
- The application does not submit telemetry, diagnostics, or issues automatically.

Implementation status: Delivered as a governed process; ongoing operation remains required.

### Story: Evidence-based GA feedback gate

As a release approver, I need traceable feedback, triage, upgrade, and exact-candidate evidence so
that GA is based on observed reliability rather than an undocumented judgment.

Acceptance criteria:

- The release owner triages every report and publishes a weekly sanitized rollup during Preview.
- G-01 records supported Windows and distribution-path coverage for the release workflows.
- Every failure has a disposition; no security-sensitive or release-blocking defect remains open.
- The exact candidate passes the full automated and clean-machine suite after the last
  release-blocking change, followed by a named go/no-go decision.

Implementation status: In progress; upgrade, report-disposition, exact-candidate, and go/no-go
evidence remain open. No arbitrary evaluator quota or waiting period applies.

### Story: Operational support and lifecycle readiness

As a release approver, I need ownership, maintenance, monitoring, response, and end-of-support
controls so that a public release remains supportable after publication.

Acceptance criteria:

- Support, security, and release owners plus non-contractual response targets are explicit.
- Current/superseded/withdrawn/end-of-support states and immutable replacement rules are published.
- Desktop/mobile NuGet and browser/design npm dependencies receive scheduled update proposals
  without automatic merging; pinned GitHub Actions and third-party pipeline tools require governed
  version review.
- A scheduled monitor checks runtime lifecycle, known dependency vulnerabilities, and public
  release/support endpoints and retains machine-readable evidence.
- Incident, withdrawal, recovery, credential/signing rotation, and communication procedures are
  exercised against the exact candidate.

Implementation status (2026-07-24): source policy and automation are implemented locally. The
integrated validator passes all 34 contract checks with desktop and mobile pinned to .NET 10 LTS
and its recorded 2028-11-14 support date. A named backup operator, successful hosted-monitor
history, complete runbook exercise, Microsoft Store trust evidence, and exact-candidate review
remain open.

### Story: Legal/privacy release controls and approval

As a release approver, I need each candidate's license, third-party, privacy, retention, telemetry,
and package/store statements to be complete and reviewed so that distribution obligations and user
expectations are explicit.

Acceptance criteria:

- CI fails when committed NuGet/npm lock files diverge from the generated component inventory and
  third-party notice.
- Windows distributables contain the product license, privacy statement, and third-party notice.
- Apple, Google, WinGet, and Chocolatey metadata and unresolved declarations are reconciled to the
  exact signed artifacts.
- A named legal/privacy reviewer records approval, exceptions, corrective dates, and artifact
  hashes; automated validation is not treated as legal approval.

Implementation status: In progress. Deterministic source inventory/notices, technical disclosures,
package/store draft metadata, a CI contract, and Windows package embedding are implemented and
locally verified. Exact-candidate review, public privacy publication, store declarations, one
unknown upstream license disposition, and named approval remain open.

## Epic 16 — Governed private-endpoint Key Vault connectivity (future roadmap)

This scope is canonical but not implemented. ADO Epic AB#6192 owns Features AB#6193–6196 and the
fifteen User Stories below. Their acceptance criteria remain open; an ADO record is not evidence
that the capability exists in source or in a deployed package.

| Story | ADO ID |
| --- | ---: |
| Document enterprise private-endpoint Key Vault topologies | AB#6197 |
| Evaluate Azure Bastion connectivity for Key Vault HTTPS traffic | AB#6199 |
| Evaluate VNet-hosted connector and delegated execution alternatives | AB#6201 |
| Select the private-connectivity architecture | AB#6203 |
| Define private connectivity profiles and resource mappings | AB#6205 |
| Design private-connectivity setup and health UX | AB#6207 |
| Define connector authentication and end-to-end protection | AB#6209 |
| Add governed private connectivity profiles | AB#6211 |
| Implement the approved private-network connector | AB#6213 |
| Route discovery through the selected private path | AB#6215 |
| Route authorized Key Vault operations through the selected private path | AB#6217 |
| Expose private-path health and actionable diagnostics | AB#6219 |
| Test representative private Key Vault network topologies | AB#6221 |
| Validate private-connectivity security boundaries | AB#6223 |
| Publish private Key Vault deployment and support guidance | AB#6225 |

Implementation status: Not started. No repository implementation, ADR, threat model, topology
diagram, lab evidence, or packaged validation currently supports a delivery claim.
