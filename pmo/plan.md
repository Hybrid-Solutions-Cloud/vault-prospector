# Vault Prospector Implementation Plan

## Purpose

This is the authoritative execution plan for taking Vault Prospector from the current Windows
Preview to a complete, supportable General Availability release. It exists to prevent requirements
from being recorded as backlog items and then incorrectly reported as delivered features.

The canonical story inventory is [`backlog.md`](backlog.md). Release evidence is tracked separately
in the [release-readiness matrix](../docs/product/release-readiness.md).

## Delivery rules

A capability is **Delivered** only when all five conditions are true:

1. production source code implements the capability;
2. the user can reach and operate it through the application;
3. automated and appropriate live tests pass;
4. it is included in an installable, upgradeable public release; and
5. release evidence records the exact version and artifact tested.

Documentation, a backlog story, a prototype, a passing unit test without a user path, or a locally
built artifact does not by itself mean a feature is delivered.

Status terms used in PMO reporting:

- **Not started** — no production implementation exists.
- **Discovery** — research or technical validation is underway; no delivery claim is allowed.
- **In progress** — production implementation has started but is not released and verified.
- **At risk** — work is active but a named issue threatens scope, security, or delivery.
- **Blocked** — progress requires an external decision, credential, service, or platform change.
- **Implemented** — source and user path exist, but release verification may remain.
- **Delivered** — all five delivery conditions above are satisfied.
- **GA complete** — delivered and all production security, support, reliability, and compliance
  gates pass.

## Current product state

- Latest public version: `0.3.0-preview.8`, unsigned and restricted to non-production evaluation.
  The immutable tag points to `2582a44e50155c80205370c6ec90b9d19eb7a006`; a dedicated Preview 8
  release-evidence record is still missing, so the release-readiness matrix must retain that gap.
- Core implemented path: interactive Entra sign-in, multiple app-owned MSAL identities,
  subscription and Key Vault discovery, secret/key/certificate metadata indexing, search, explicit
  value retrieval, verified copy, encrypted optional offline access, workspaces, and read-only Azure
  behavior.
- Current installer state: the exact public MSI passed all 27 Windows lifecycle gates, including
  upgrade from `0.1.1-preview.1`, failed-upgrade rollback, repair, downgrade rejection, uninstall,
  and retained state.
- The 0.2 Preview includes workload profiles, read-only identity discovery/provisioning previews,
  permission-aware discovery, workspace/reconciliation completion, recovery-archive retention,
  notification-area operation, enterprise policy, browser/CyberArk validation paths, and four
  desktop concepts. Their named live, independent, usability, accessibility, and GA evidence
  remains open.
- Governed Azure mutation code is implemented internally for four separately controlled
  operations and remains default-denied behind both an accepted-build release switch and exact
  machine policy. Live disposable-Azure validation, independent review, and release enablement
  remain open. The product owner selected Atlas.
  Corrected exact-main candidate `0.3.0-ci.201` passed the installed Windows 11 RDP walkthrough
  across all eight production screens after the failed `0.3.0-ci.190` layout was replaced.
  Follow-up `0.3.0-ci.207` passed the complete Atlas secure-unlock startup and current-account RDP
  verification. Public `0.3.0-preview.3` passed independent artifact verification, installation,
  startup, and explicit RDP-prompt checks. Complete every-state exact-public
  usability/accessibility, live-provider, and independent-review evidence remain open. Mobile
  source/prototypes are implemented but are not distributed.
  CyberArk remains future-roadmap work, while browser integration remains a non-production path
  pending installed-browser validation and distribution review.
- Major GA work: signing, independent security review, complete live identity/accessibility test
  matrices, public package catalogs, feedback thresholds, and stability evidence.

## Priority model

| Priority | Meaning |
| --- | --- |
| P0 | Current release defect, security boundary, or work needed to make subsequent feature testing reliable |
| P1 | Core identity, discovery, authorization, and data-integrity capability required for the intended product |
| P2 | Major desktop experience or enterprise-source capability |
| P3 | Additional platform and ecosystem expansion |
| GA | Production trust, independent validation, operational readiness, and promotion evidence |

No calendar promise is recorded until effort is estimated against actual available capacity. Work
is executed in the dependency order below; a phase may be split into multiple Preview releases.

## Phase 0 — PMO baseline and release-truth controls

**Priority:** P0

**Status:** In progress

### Scope

- Maintain every canonical backlog story in `pmo/backlog.md`.
- Assign every story an implementation status, source evidence, target phase, and acceptance test.
- Keep one implementation plan and one release-readiness matrix; do not create conflicting status
  documents.
- Report source implementation, live validation, and public release status separately.

### Deliverables

- Canonical `/pmo/backlog.md`.
- Canonical `/pmo/plan.md`.
- Traceability table covering every Partial and Not Started story.
- A standard full-status format showing completed, in-progress, not-started, blocked, validation,
  release, and next actions.

### Exit criteria

- Every backlog story maps to exactly one primary phase below.
- No story is described as implemented solely because documentation exists.
- Existing documentation links resolve to the canonical PMO files.

## Phase 1 — Installed icon correction and Preview refresh

**Priority:** P0

**Status:** Delivered in `0.2.0-preview.1`; exact public MSI lifecycle passed 27/27

### Scope

- Set the advertised MSI Start-menu shortcut's `Icon_` and `IconIndex` metadata explicitly.
- Validate the embedded MSI icon resource and shortcut row during CI and protected releases.
- Install the exact candidate on clean Windows and verify Start, Windows Search, taskbar, window,
  executable, and Installed Apps icon behavior.
- Upgrade from `0.1.1-preview.1`, verify application/state preservation, and publish a new immutable
  Preview version rather than replacing the current release.

### Exit criteria

- Windows Search and Start show the Vault Prospector icon after install and upgrade.
- Automated MSI metadata validation fails if the shortcut icon reference is absent or empty.
- The exact public MSI passes install, upgrade, repair, uninstall, and anonymous hash validation.

## Phase 2 — Interactive identity lifecycle

**Priority:** P1

**Status:** Included in `0.2.0-preview.1`; live multi-tenant validation remains open

**Backlog coverage:** Epic 2; remaining human-identity portions of Epic 9

### Scope

- Add explicit reauthentication for an interaction-required identity.
- Add disable/re-enable without deleting indexed metadata.
- Preserve app-owned MSAL cache isolation for each connection.
- Make active identity, tenant, and authentication state visible for discovery and retrieval.
- Complete identity removal and token-cache purge evidence.
- Exercise tenant consent, guest accounts, MFA, Conditional Access, passwordless/FIDO, cancellation,
  expiry, revocation, and account removal on real Entra tenants.

### Exit criteria

- All human-identity lifecycle actions are reachable and understandable in the UI.
- Another terminal, Azure CLI, Azure PowerShell, or IDE session cannot redirect the app's identity.
- Automated and live multi-tenant tests pass without token or identifier disclosure.

## Phase 3 — First-run unlock, recovery, and protected local state

**Priority:** P1

**Status:** Included in `0.2.0-preview.1`; the guided first-run unlock/identity workflow, recovery,
schema-v4 behavior, and an internal crash-recoverable all-or-rollback key-rotation engine plus
verified recovery-archive retention UX are implemented and automated tests pass, but rotation is
not user-exposed, live-validated, or independently reviewed

**Backlog coverage:** Secure first-run wizard; mandatory encryption; schema upgrade validation

### Scope

- Separate application unlock from Azure authentication in setup and UX language.
- Complete Windows Hello success, cancellation, unavailable, policy-disabled, and recovery paths.
- Keep SQLCipher metadata encryption mandatory with no plaintext mode.
- Keep offline values opt-in but always AES-GCM encrypted when retained.
- Implement and test forward-only migrations for every supported published schema.
- Define key rotation, backup, reinstall, device replacement, and unrecoverable-key behavior.

ADR-0011 defines and the internal engine implements a verified matched-state archive,
HMAC-authenticated journal/manifest, SQLCipher rekey, all-envelope re-encryption, staged
DPAPI-protected key publication, post-rotation validation, and startup rollback at every injected
crash boundary. Same-account recovery, reinstall retention, resynchronization on device/profile
replacement, and preserve/archive/reset behavior for unrecoverable keys are documented. User
exposure, live Windows power-loss testing, and independent review remain GA gates. Settings now
inventories only canonical app-generated recovery archives and permits one permanent deletion only
after exact `DELETE ARCHIVE` confirmation, fresh Windows verification, path/reparse validation, and
an active-rotation-journal check; automatic retention remains intentionally disabled.

First-run implementation note (2026-07-23): local Windows verification and protected repository
initialization complete before setup becomes available. An empty profile opens directly on the
Identities tab, explicitly distinguishes local unlock from Microsoft Entra authentication and
metadata-only synchronization, and labels the connection action for the selected authentication
method. Live Windows Hello, Entra policy, keyboard, screen-reader, independent-review, and
exact-release evidence remain open.

### Exit criteria

- Setup fails closed when required platform protection is unavailable.
- No supported path silently resets, replaces, or downgrades existing encrypted state.
- Migration and recovery tests cover every published schema and supported upgrade path.
- Independent security review approves algorithms, storage, permissions, and memory lifetime before
  GA.

## Phase 4 — Human and workload connection profiles

**Priority:** P1

**Status:** Certificate and federated service-principal plus detected-host managed-identity
profiles, validate-before-persist rotation, local revocation, fail-closed use, and machine-managed
provider/identity-type/tenant boundaries are included in `0.2.0-preview.1` with automated
validation; independent review and live Azure/administrator evidence remain open

**Backlog coverage:** Human and workload identity choices

### Scope

- Keep interactive Entra user authentication as the default desktop option.
- Add service-principal profiles using certificates or workload federation; client secrets require
  a separate approved security decision and protected-storage design.
- Add managed-identity profiles only when the running environment exposes a usable managed identity
  endpoint; an ordinary laptop must never imply that a listed identity can be used locally.
- Give each profile separate storage, rotation, revocation, audit, display, and token acquisition.
- Apply machine-managed provider, identity-type, and tenant allow-lists before governed network
  operations while preserving local disable, revoke, purge, and remove recovery paths.
- Never inherit Azure CLI, Azure PowerShell, IDE, or terminal credential context.

### Exit criteria

- Setup clearly explains what each connection type can and cannot do.
- Workload profiles cannot inherit a human token cache or permissions silently.
- Each supported credential type passes contract, negative, redaction, rotation, and live Azure
  tests.

## Phase 5 — Workload identity discovery and governed provisioning

**Priority:** P1

**Status:** Explicit-account identity discovery, fail-closed effective authorization evidence, and
user-reachable deterministic non-mutating plans are included in `0.2.0-preview.1`; governed
execution, independent review, and live evidence remain open

**Backlog coverage:** Discover and provision workload identities

### Scope

- After interactive administrator authentication, list only managed identities and service
  principals the user is authorized to view.
- Distinguish permission to view, attach/use, manage, and access Key Vault data.
- Add dry-run plans for creating a user-assigned managed identity or service principal.
- Add dry-run least-privilege Key Vault role assignments at an exact scope.
- Require explicit confirmation, fresh authorization when needed, audit records, and rollback
  guidance before any creation or assignment.
- Keep initial/default setup non-mutating.

Local implementation note (2026-07-23): an explicit candidate-plus-Key-Vault assessment now reads
the administrator's exact-resource caller permissions and the candidate's applicable
inherited/transitive role assignments, role definitions, exclusions, deny assignments, and
conditions. ARM redirects are disabled, pagination is trusted-host constrained and bounded, and
no mutation or data-plane request occurs. Conditional expressions, access-policy vaults,
unreadable deny sets, and potentially applicable group denies remain visibly unproven.
The exact locked local Release gate passes 218/218 tests with no known vulnerable NuGet packages
and zero build warnings/errors; see
`docs/release-evidence/workload-authorization-evidence-2026-07-23.md`.

### Exit criteria

- No identity or Azure role is created implicitly.
- Every mutation preview names tenant, subscription, resource group, identity, vault, role, scope,
  and expected effect without containing secrets.
- Insufficient directory, managed-identity, or RBAC permissions fail safely and explain the missing
  authorization.

## Phase 6 — Permission-aware Azure and Key Vault discovery

**Priority:** P1

**Status:** Included in `0.2.0-preview.1` for explicit subscription/vault scope, observed
permission display, and machine-managed tenant filtering before and after discovery; live Azure,
administrator-deployment, and independent validation remain open

**Backlog coverage:** Epic 3; discover vaults by selected access path; read-only policy UI

### Scope

- Run discovery separately for the selected human or workload connection.
- Add subscription and vault inclusion/exclusion before synchronization.
- Show management-plane resource visibility separately from secrets, keys, and certificates
  data-plane list/read/write permissions.
- Continue accessible results when another subscription, vault, or object type is inaccessible.
- Retrieve metadata only during discovery; never retrieve values implicitly.
- Show the identity and tenant responsible for every result and error.

Local implementation note (2026-07-23): the identity screen now lists discovered subscriptions and
vault access paths, persists explicit include/exclude choices, applies both scopes before provider
metadata enumeration, and retains excluded scope records so users can reverse the choice. Each
vault displays its identity/tenant/subscription route, management-plane visibility, observed
secret/key/certificate metadata-list outcomes, and makes clear that value-read authorization is
not probed during synchronization and writes are disabled by policy. Schema v4 adds a
backward-compatible vault-selection column. Live Azure permission matrices and independent
redaction/security validation remain release gates.

Local implementation note (2026-07-24): the application now reads a versioned machine policy from
`HKLM\SOFTWARE\Policies\Hybrid Solutions Cloud\Vault Prospector`. It constrains Azure tenants,
providers, identity types, clipboard use, and offline-value retention in application services,
passes tenant constraints into Azure discovery before enumeration, filters returned/persisted
metadata again, and keeps cleanup operations available. Invalid enabled policy fails closed. The
package includes ADMX/ADML templates and a deterministic read-only readiness check. Governed
Group Policy/Intune deployment, live allowed/denied provider matrices, independent review, and
exact signed-candidate validation remain open.

### Exit criteria

- Users can search or discover all vaults visible to the selected access path.
- Results accurately explain visible-but-unreadable and readable-but-not-manageable cases.
- Partial failures do not hide accessible resources or leak sensitive Azure details.

## Phase 7 — Index, reconciliation, workspace, and migration completion

**Priority:** P1

**Status:** Included in `0.2.0-preview.1` for reconciliation, complete workspace assignment,
per-workspace policy, and schema v4 migration; independent lifecycle validation remains open

**Backlog coverage:** Reconcile removed objects; complete workspace assignment; remaining schema work

### Scope

- Reconcile provider deletions and permission loss without silently destroying useful history.
- Support direct identity, tenant, subscription, and vault assignment to workspaces.
- Add editable per-workspace cache policies with secure defaults.
- Preserve favorites, recent activity, and audit references through sync and migrations.
- Complete supported database/cache schema migration, rollback, and recovery behavior.

Local implementation note (2026-07-23): complete discovery tombstones missing access paths and
items while partial discovery preserves prior results; explicit excluded scopes remain reversible.
Workspaces now accept identity, tenant, subscription, and vault links and expose an editable
encrypted-cache lifetime/enablement and clipboard policy while Windows verification remains
mandatory. Workspace deletion transactionally removes links, and schema v1–v4/future/corrupt
recovery paths have automated coverage. Upgrade/downgrade/reinstall and independent cache-boundary
validation remain release evidence gates.

### Exit criteria

- Removed and inaccessible objects have explicit, testable states.
- Workspace scope changes cannot expose cached values across identities or vaults.
- Upgrade/downgrade/reinstall behavior matches documented retention guarantees.

## Phase 8 — Governed write operations

**Priority:** P1, high risk

**Status:** Implemented internally and default-disabled; live Azure, independent review, accepted
ADR, and release-enable evidence remain open

**Backlog coverage:** Explicit write mode for secrets, keys, and certificates

### Scope

- Define supported mutations individually; do not add a generic unrestricted write toggle.
- Keep every new connection and workspace read-only by default, regardless of the account's broad
  Azure permissions.
- Require administrator policy, capable identity, fresh Azure authorization where required, local
  verification, prominent elevated state, and an exact operation preview.
- Produce audit-friendly records without values and provide failure/rollback guidance.

### Exit criteria

- Installing or connecting the app cannot change Azure resources by default.
- Every supported mutation has authorization, concurrency, recovery, redaction, audit, and live
  integration tests.
- Independent security review approves the design before public enablement.

## Phase 9 — Desktop UI research and redesign

**Priority:** P2

**Status:** In progress; comparative research, task analysis, four interactive concepts, and the
representative-user protocol are complete; participant evidence and production selection remain
open

**Backlog coverage:** Epic 12

### Scope

- Research established password-manager and enterprise-vault patterns for onboarding, unlock,
  navigation, search, collections, source identity, reveal/copy, autofill, warnings, audit, and
  recovery.
- Produce research findings, task flows, wireframes, and at least one interactive prototype.
- Design and deliver 4 distinct UI mockup versions (covering setup, search, secret reveal, and settings) for user alignment.
- Test prototypes with representative Windows users and assistive technologies.
- Select and implement the design while keeping identity/source/security state visible.

Progress note (2026-07-23): official password-manager, Windows design/accessibility, WCAG, and Key
Vault patterns were synthesized in `docs/design/desktop-ui-research-2026-07-23.md`. The local React
prototype delivers Source-first, Search-first, Guided tasks, and Operations console concepts, each
covering setup, search, reveal, and settings. All 16 combinations build and run without browser
console errors, and the prototype reflows without horizontal document overflow at 390 pixels. The
recorded initial hypothesis favors Source-first, but it is not a selection decision: the
eight-participant protocol and assistive-technology matrix remain mandatory before production
redesign.

### Exit criteria

- The redesign is based on recorded research, 4 delivered mockup versions, and usability evidence, not cosmetic preference.
- Core tasks are materially easier to discover and complete.
- Keyboard, Narrator, NVDA, High Contrast, scaling, text size, and target-size gates pass.

## Phase 10 — Notification-area and background operation

**Priority:** P2

**Status:** Included in `0.2.0-preview.1` for explicit close behavior, notification-area lifecycle,
immediate foreground lock, and opt-in metadata-only sync; live lifecycle validation remains open

**Backlog coverage:** Epic 11

### Scope

- Add explicit close choices: exit, minimize to notification area, or ask.
- Show locked, syncing, interaction-required, error, and offline states in the tray icon/menu.
- Clear revealed values and sensitive UI state when entering background mode.
- Permit metadata-only background synchronization under battery, network, policy, MFA, and
  Conditional Access constraints.
- Require foreground verification for reveal, copy, or offline caching.

Local implementation note (2026-07-23): Settings persists Ask, Exit, or Lock-to-notification-area
close behavior. Ask presents explicit in-app choices. Backgrounding cancels active work, advances a
sensitive-presentation generation, hides the window and taskbar entry, masks values, and returns to
the locked screen. The tray menu reports Locked, Syncing, Action required, Azure interaction
required, Offline, or Ready and provides Show/Exit. Opt-in 15-minute background work calls only the
metadata synchronization service while hidden and network-available; cancellation checks prevent a
provider result from reaching clipboard/cache/presentation after background lock. A disposable
Windows system-event monitor now locks and invalidates sensitive presentation on every session
transition and on both suspend and resume; ordinary power-status changes do not create disruptive
locks. Live installed sleep/resume, session-transition, battery, network, token-expiry, tray, and
assistive-technology behavior remain required evidence.

### Exit criteria

- Background operation never leaves a value unlocked or bypasses user presence.
- Exit reliably clears app-owned clipboard content and stops background activity.
- Restart, sleep/resume, network change, token expiry, and interaction-required tests pass.

## Phase 11 — Browser integration and autofill

**Priority:** P2

**Status:** Included in `0.2.0-preview.1`; live browser, distribution, and independent validation open

**Backlog coverage:** Epic 13

### Scope

- Threat-model a signed browser extension and authenticated native-messaging host.
- Support only explicitly mapped values, approved origins, and defined field purposes.
- Require policy and local verification for sensitive fills.
- Research supported browser password-vault APIs without scraping browser credential databases.
- Define extension permissions, signing, updates, compromise response, and revocation.

### Current implementation

- ADR-0014, the browser threat model, and the feasibility spike define toolbar-only, one-shot fill
  and prohibit private browser credential-database access.
- Chromium and Firefox MV3 sources use no persistent host permissions or content scripts and
  validate the live tab, frame, document, focused element, origin, and field purpose before fill.
- A bounded strict protocol, exact extension identities, authenticated native host/current-user
  broker, process verification, encrypted mappings, value-free audit, protected fail-closed
  machine policy, visible desktop confirmation, and fresh Windows verification are implemented.
- The MSI packages the native host and registers exact HKLM Chrome, Edge, and Firefox native-host
  manifests. Automated source and package validation exists.
- Signed extension packages, browser distribution review, exact installed live-browser evidence,
  independent security review, compromise/revocation exercise, and representative-user/AT
  validation remain open.

### Exit criteria

- No arbitrary Azure value is offered to an unapproved origin or field.
- Origin, frame, tab, item mapping, identity, and user-presence checks are enforced and tested.
- Browser-vault interoperability uses supported APIs and explicit consent only.

## Future private-connectivity roadmap — private-endpoint Key Vaults

**Priority:** P4

**Status:** Not started; canonical backlog and ADO hierarchy only

**Backlog coverage:** Epic 16; ADO AB#6192–6225

### Scope

- Document representative private-endpoint/DNS/topology constraints and evaluate supported Azure
  Bastion, VNet-hosted connector, and delegated-execution alternatives.
- Select the architecture through an approved ADR and threat model before implementation begins.
- Define governed connectivity profiles, resource mappings, connector authentication,
  end-to-end protection, setup, health, and actionable diagnostics.
- Route discovery and separately authorized Key Vault operations only through the selected,
  policy-constrained private path.
- Validate representative topologies and security boundaries, then publish deployment and support
  guidance with exact tested constraints.

### Exit criteria

- An approved ADR and threat model identify the supported architecture and rejected alternatives.
- Connector/profile/routing source and tests enforce identity, tenant, subscription, vault,
  operation, network, and failure-isolation boundaries.
- Representative private-network and adverse-path evidence passes against an exact packaged
  candidate, followed by independent security review and support approval.

No implementation, design decision, lab evidence, or release claim currently exists in this
repository. The ADO hierarchy is planning scope only.

## Future provider roadmap — CyberArk

**Priority:** P4

**Status:** Unsupported source prototype; Windows release UI disabled

**Backlog coverage:** Epic 14

### Future scope

- Select the supported CyberArk product/API and authentication methods through an ADR.
- Model accounts, safes, objects, permissions, versions, and audit semantics without forcing them
  into Azure-specific concepts.
- Isolate, encrypt, rotate, revoke, and remove provider credentials.
- Keep metadata synchronization separate from explicit verified value retrieval.

### Exit criteria

- Provider contract, integration, security, redaction, permission, failure, and live tests pass.
- Azure and CyberArk identities, objects, errors, and audit context remain visibly distinct.

### Current evidence

- ADR-0015 selects Privilege Cloud Shared Services and Identity service-user authentication.
- A dedicated provider, DPAPI credential store, SQLCipher schema v6, application service, and
  CyberArk desktop destination implement profile lifecycle, metadata sync/search, version and
  direct-permission evidence, explicit verified reveal/copy, fail-closed local revocation, removal,
  and value-free audit.
- Bounded/off-origin/oversize/redaction/provider-contract, credential replay/removal, schema,
  cross-profile rollback, verification, audit-failure disposal, and accessibility tests are present.
- Governed live tenant, explicit product approval, independent security review, and separate
  release evidence are required before this work may enter a supported release.
- PR #11 merged exact verified head `31a4f391` after CI run `30069509556` passed both required
  jobs; this establishes source/CI evidence only and does not close the external gates.

## Future mobile roadmap — iPhone/iOS and Android applications

**Priority:** P4

**Status:** Source prototypes implemented; independently gated and not part of Windows delivery

**Backlog coverage:** Epic 8

### Scope

- Build mobile-safe search and retrieval clients using shared contracts where appropriate.
- Use Keychain/Secure Enclave/LocalAuthentication on Apple platforms and Android
  Keystore/BiometricPrompt on Android.
- Validate screenshot, background-state, clipboard, backup, device migration, and biometric
  recovery boundaries.
- Research Apple Password AutoFill and Android Autofill framework eligibility without promising
  unsupported exposure of arbitrary Azure values.
- Prepare signing, privacy/data-safety declarations, TestFlight/closed testing, and store review.

### Exit criteria

- Each platform passes its own threat model, accessibility, lifecycle, secure-storage, signing, and
  store-readiness gates.
- Mobile release status is reported independently from Windows delivery.

## Phase 14 — Distribution, trust, and independent validation

**Priority:** GA

**Status:** In progress; Store trust and independent/live validation gates remain open

### Scope

- Enforce startup/reopen, encrypted metadata sync, search, cancellation, memory, storage, and
  large-estate targets in controlled automation, then repeat them on representative devices and
  the exact packaged candidate.
- Build a reproducible MSIX, submit it through the free Microsoft Store path, and verify the
  Microsoft-signed package on clean Windows.
- Submit immutable packages to WinGet and Chocolatey; verify catalog installation and update.
- Execute the independent security-review plan and close all critical/high findings.
- Complete real Entra, Windows Hello, clipboard, accessibility, clean-machine, upgrade, recovery,
  and supported-Windows matrices against exact public artifacts.
- Maintain SBOM, checksums, Sigstore bundles, provenance, release notes, rollback, and vulnerability
  response for every release.

Toolchain progress note (2026-07-25): the desktop solution, tests, lock files, GitHub Actions on HCS
runners, self-contained application, MSI, ZIP, MSIX, WinGet manifests, and Chocolatey package are on
.NET 10 LTS. Exact `main` ADO build 284 passed 370 Windows/shared tests, 44 mobile tests, native iOS,
Android packaging, and all integrated gates. Release build 287 produced and Key Vault-signed the
public candidate; the exact public MSI then passed all 27 clean Windows lifecycle gates. Trusted
Store certification, independent/live validation, and catalog acceptance remain open. Those ADO
builds are retained historical evidence; ADO no longer owns delivery automation.

Progress note (2026-07-25): G-09 source controls now generate a deterministic 245-record integrated
NuGet/npm inventory and third-party notice, fail CI on lock-file or disclosure drift, document
package/store declarations, and embed the repository license, privacy statement, and notice in
Windows ZIP/MSI payloads. G-09 remains in progress: exact signed-candidate SBOM/file
reconciliation, the diagnostics-package license disposition, a public privacy URL, Apple/Google
declaration review, and named human approval are external decision gates.

### Exit criteria

- Microsoft Store installs a trusted, certified MSIX; direct MSI/ZIP downloads remain explicitly
  unsigned.
- Direct, WinGet, and Chocolatey installation/update paths are supported and reproducible.
- No unresolved critical/high security, data-loss, authentication, authorization, encryption, or
  accessibility release blocker remains.

## Phase 15 — Preview reliability and GA promotion

**Priority:** GA

**Status:** In progress; operational automation and lifecycle policy are included in
`0.2.0-preview.1`, while retained hosted history, backup ownership, exercises, exact-candidate
workflow coverage, and go/no-go evidence remain open

**Backlog coverage:** Epic 15

### Scope

- Operate the voluntary, privacy-safe feedback and private vulnerability channels.
- Maintain the legal/privacy inventory, packaged disclosures, public privacy statement, and
  package/store declarations against each exact candidate.
- Triage every report and maintain sanitized weekly rollups.
- Operate weekly dependency, vulnerability, runtime-lifecycle, public-release, and support-channel
  monitoring with retained machine-readable evidence.
- Maintain named support/security ownership, immutable supersedence and end-of-support policy,
  credential/signing rotation controls, and an exercised incident/withdrawal/recovery runbook.
- Complete supported Windows-build, install-path, upgrade, report-disposition, and response
  evidence.
- Run the full suite on the exact candidate after the last release-blocking change.
- Produce the final named go/no-go decision and rollback plan.

### Exit criteria

- All Preview and GA gates are passed, not merely accepted as Preview risks.
- Production documentation, support, security, privacy, installation, recovery, and operations
  guidance agree with the exact artifact.
- The product owner approves GA with recorded evidence and no unresolved release blocker.

Progress note (2026-07-24): a machine-readable operational-readiness contract, fail-closed
PowerShell validator, weekly Dependabot coverage, scheduled vulnerability/runtime/public-endpoint
monitor, and support/EOS policy are implemented. The integrated contract now passes all 34 checks
with both desktop and mobile pinned to .NET 10 LTS and its recorded 2028-11-14 support date. The
earlier local baseline also passed all three public endpoints. G-08 remains In progress pending a
backup operator, retained hosted runs, the complete exercise, and Microsoft Store trust evidence.

## Backlog-to-plan traceability

| Backlog area | Primary phase |
| --- | --- |
| Application foundation | Existing implementation; validate in Phases 9 and 14 |
| Identity and authentication | Phases 2–5 |
| Azure discovery | Phase 6 |
| Index and search | Phase 7; existing search validated in Phases 9 and 14 |
| Secure retrieval | Existing implementation; validate in Phases 3 and 14 |
| Offline access | Phases 3 and 7 |
| Security and governance | Phases 0, 3, 8, 14, and 15 |
| iPhone and Android | Phase 13 |
| Secure first-run and identity architecture | Phases 2–5 |
| Vault discovery and governed writes | Phases 6 and 8 |
| Taskbar/background operation | Phase 10 |
| Desktop UI research/refinement | Phase 9 |
| Browser integration | Phase 11 |
| Private-endpoint Key Vault connectivity | Future private-connectivity roadmap |
| CyberArk | Phase 12 |
| Preview feedback and GA | Phase 15 |

## Release strategy

- Use small immutable Preview increments; never replace assets under an existing tag.
- Each increment must name the user-visible capability delivered and the incomplete work remaining.
- Installer versions must always upgrade every supported earlier public version.
- Release engineering supports feature delivery; it is not a substitute for feature delivery.
- WinGet and Chocolatey packages must reference the exact public MSI hash.
- Stable/GA releases remain blocked until trusted signing and every GA gate pass.

## Required status report

Every status request must return all of the following:

1. current public version and download status;
2. branch, latest pushed commit, and CI state;
3. current phase and exact production files changed;
4. features implemented but not yet released;
5. features delivered in the current release;
6. validation completed, failed, and still pending;
7. blockers and external dependencies;
8. every remaining phase with its status;
9. next concrete action and its completion condition; and
10. an explicit statement when work changed documentation only.

## Change control

- Product-owner requests are added first to `pmo/backlog.md`, then mapped into this plan.
- Adding scope requires naming the phase affected and the work displaced or delayed.
- Security-sensitive design changes require threat-model review before implementation.
- Azure mutations, identity creation, RBAC assignment, external publication, and store submission
  retain explicit approval and audit requirements.
- Completed work is never marked Delivered until the exact public artifact is verified.

## Cross-phase implementation review — 2026-07-23

The accumulated Phase 3–10 worktree received a security, correctness, performance, and
maintainability review. Findings in identity revocation/purge, persisted authentication errors,
remote/local JSON bounds, Graph pagination trust, tray state, and Windows rotation recovery were
remediated with regression coverage. The exact locked local Release gate passes 254/254 tests with
zero warnings/errors and no known vulnerable direct or transitive NuGet packages. Internal review
does not satisfy the independent security, live Azure/Windows, accessibility, usability, or exact
packaged-candidate gates. Evidence:
`docs/release-evidence/cross-phase-security-correctness-review-2026-07-23.md`.
