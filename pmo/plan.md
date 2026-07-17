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

- Current public version: `0.1.1-preview.1`, unsigned and restricted to non-production evaluation.
- Core implemented path: interactive Entra sign-in, multiple app-owned MSAL identities,
  subscription and Key Vault discovery, secret/key/certificate metadata indexing, search, explicit
  value retrieval, verified copy, encrypted optional offline access, workspaces, and read-only Azure
  behavior.
- Current installer defect: the advertised Start-menu shortcut does not explicitly reference the
  embedded icon, so Windows Search can display a blank document. The executable icon itself is
  correct.
- Major missing product work: workload identities, identity provisioning/RBAC, permission-aware
  discovery UI, governed writes, desktop redesign, tray operation, browser integration, CyberArk,
  and mobile applications.
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

- Maintain all 45 backlog stories in `pmo/backlog.md`.
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

**Status:** In progress, not committed or released

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

**Backlog coverage:** Secure first-run wizard; mandatory encryption; schema upgrade validation

### Scope

- Separate application unlock from Azure authentication in setup and UX language.
- Complete Windows Hello success, cancellation, unavailable, policy-disabled, and recovery paths.
- Keep SQLCipher metadata encryption mandatory with no plaintext mode.
- Keep offline values opt-in but always AES-GCM encrypted when retained.
- Implement and test forward-only migrations for every supported published schema.
- Define key rotation, backup, reinstall, device replacement, and unrecoverable-key behavior.

### Exit criteria

- Setup fails closed when required platform protection is unavailable.
- No supported path silently resets, replaces, or downgrades existing encrypted state.
- Migration and recovery tests cover every published schema and supported upgrade path.
- Independent security review approves algorithms, storage, permissions, and memory lifetime before
  GA.

## Phase 4 — Human and workload connection profiles

**Priority:** P1

**Backlog coverage:** Human and workload identity choices

### Scope

- Keep interactive Entra user authentication as the default desktop option.
- Add service-principal profiles using certificates or workload federation; client secrets require
  a separate approved security decision and protected-storage design.
- Add managed-identity profiles only when the running environment exposes a usable managed identity
  endpoint; an ordinary laptop must never imply that a listed identity can be used locally.
- Give each profile separate storage, rotation, revocation, audit, display, and token acquisition.
- Never inherit Azure CLI, Azure PowerShell, IDE, or terminal credential context.

### Exit criteria

- Setup clearly explains what each connection type can and cannot do.
- Workload profiles cannot inherit a human token cache or permissions silently.
- Each supported credential type passes contract, negative, redaction, rotation, and live Azure
  tests.

## Phase 5 — Workload identity discovery and governed provisioning

**Priority:** P1

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

### Exit criteria

- No identity or Azure role is created implicitly.
- Every mutation preview names tenant, subscription, resource group, identity, vault, role, scope,
  and expected effect without containing secrets.
- Insufficient directory, managed-identity, or RBAC permissions fail safely and explain the missing
  authorization.

## Phase 6 — Permission-aware Azure and Key Vault discovery

**Priority:** P1

**Backlog coverage:** Epic 3; discover vaults by selected access path; read-only policy UI

### Scope

- Run discovery separately for the selected human or workload connection.
- Add subscription and vault inclusion/exclusion before synchronization.
- Show management-plane resource visibility separately from secrets, keys, and certificates
  data-plane list/read/write permissions.
- Continue accessible results when another subscription, vault, or object type is inaccessible.
- Retrieve metadata only during discovery; never retrieve values implicitly.
- Show the identity and tenant responsible for every result and error.

### Exit criteria

- Users can search or discover all vaults visible to the selected access path.
- Results accurately explain visible-but-unreadable and readable-but-not-manageable cases.
- Partial failures do not hide accessible resources or leak sensitive Azure details.

## Phase 7 — Index, reconciliation, workspace, and migration completion

**Priority:** P1

**Backlog coverage:** Reconcile removed objects; complete workspace assignment; remaining schema work

### Scope

- Reconcile provider deletions and permission loss without silently destroying useful history.
- Support direct identity, tenant, subscription, and vault assignment to workspaces.
- Add editable per-workspace cache policies with secure defaults.
- Preserve favorites, recent activity, and audit references through sync and migrations.
- Complete supported database/cache schema migration, rollback, and recovery behavior.

### Exit criteria

- Removed and inaccessible objects have explicit, testable states.
- Workspace scope changes cannot expose cached values across identities or vaults.
- Upgrade/downgrade/reinstall behavior matches documented retention guarantees.

## Phase 8 — Governed write operations

**Priority:** P1, high risk

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

**Backlog coverage:** Epic 12

### Scope

- Research established password-manager and enterprise-vault patterns for onboarding, unlock,
  navigation, search, collections, source identity, reveal/copy, autofill, warnings, audit, and
  recovery.
- Produce research findings, task flows, wireframes, and at least one interactive prototype.
- Test prototypes with representative Windows users and assistive technologies.
- Select and implement the design while keeping identity/source/security state visible.

### Exit criteria

- The redesign is based on recorded research and usability evidence, not cosmetic preference.
- Core tasks are materially easier to discover and complete.
- Keyboard, Narrator, NVDA, High Contrast, scaling, text size, and target-size gates pass.

## Phase 10 — Notification-area and background operation

**Priority:** P2

**Backlog coverage:** Epic 11

### Scope

- Add explicit close choices: exit, minimize to notification area, or ask.
- Show locked, syncing, interaction-required, error, and offline states in the tray icon/menu.
- Clear revealed values and sensitive UI state when entering background mode.
- Permit metadata-only background synchronization under battery, network, policy, MFA, and
  Conditional Access constraints.
- Require foreground verification for reveal, copy, or offline caching.

### Exit criteria

- Background operation never leaves a value unlocked or bypasses user presence.
- Exit reliably clears app-owned clipboard content and stops background activity.
- Restart, sleep/resume, network change, token expiry, and interaction-required tests pass.

## Phase 11 — Browser integration and autofill

**Priority:** P2

**Backlog coverage:** Epic 13

### Scope

- Threat-model a signed browser extension and authenticated native-messaging host.
- Support only explicitly mapped values, approved origins, and defined field purposes.
- Require policy and local verification for sensitive fills.
- Research supported browser password-vault APIs without scraping browser credential databases.
- Define extension permissions, signing, updates, compromise response, and revocation.

### Exit criteria

- No arbitrary Azure value is offered to an unapproved origin or field.
- Origin, frame, tab, item mapping, identity, and user-presence checks are enforced and tested.
- Browser-vault interoperability uses supported APIs and explicit consent only.

## Phase 12 — CyberArk provider

**Priority:** P2

**Backlog coverage:** Epic 14

### Scope

- Select the supported CyberArk product/API and authentication methods through an ADR.
- Model accounts, safes, objects, permissions, versions, and audit semantics without forcing them
  into Azure-specific concepts.
- Isolate, encrypt, rotate, revoke, and remove provider credentials.
- Keep metadata synchronization separate from explicit verified value retrieval.

### Exit criteria

- Provider contract, integration, security, redaction, permission, failure, and live tests pass.
- Azure and CyberArk identities, objects, errors, and audit context remain visibly distinct.

## Phase 13 — iPhone/iOS and Android applications

**Priority:** P3

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

### Scope

- Complete Azure Artifact Signing Public Trust setup and timestamped Authenticode verification.
- Submit immutable packages to WinGet and Chocolatey; verify catalog installation and update.
- Execute the independent security-review plan and close all critical/high findings.
- Complete real Entra, Windows Hello, clipboard, accessibility, clean-machine, upgrade, recovery,
  and supported-Windows matrices against exact public artifacts.
- Maintain SBOM, checksums, Sigstore bundles, provenance, release notes, rollback, and vulnerability
  response for every release.

### Exit criteria

- Windows shows the trusted publisher on binaries and MSI.
- Direct, WinGet, and Chocolatey installation/update paths are supported and reproducible.
- No unresolved critical/high security, data-loss, authentication, authorization, encryption, or
  accessibility release blocker remains.

## Phase 15 — Preview reliability and GA promotion

**Priority:** GA

**Backlog coverage:** Epic 15

### Scope

- Operate the voluntary, privacy-safe feedback and private vulnerability channels.
- Triage every report and maintain sanitized weekly rollups.
- Meet the defined evaluator, task-attempt, Windows-build, install-path, completion-rate, upgrade,
  and response-time thresholds.
- Complete the final blocker-free stability window on the exact GA candidate.
- Produce the final named go/no-go decision and rollback plan.

### Exit criteria

- All Preview and GA gates are passed, not merely accepted as Preview risks.
- Production documentation, support, security, privacy, installation, recovery, and operations
  guidance agree with the exact artifact.
- The product owner approves GA with recorded evidence and no unresolved release blocker.

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
