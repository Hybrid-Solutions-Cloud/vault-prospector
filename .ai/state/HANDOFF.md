# Session handoff

## Goal and recovery update — 2026-07-23

- Durable repository goal: `.ai/state/GOAL.md`.
- Canonical objective: fully implement every backlog item in dependency order, complete production
  workflows and automated/live validation, synchronize all documentation and release evidence, and
  reach a verified GA artifact.
- `main` and `origin/main` point to `20da2be` (`feat: complete Phase 2 - Interactive Identity
  Lifecycle`). Its GitHub CI run failed at formatting before tests.
- The worktree contains uncommitted Phase 3–7 work covering local unlock/recovery, workload identity
  profiles, schema v3, subscription exclusion plumbing, object reconciliation, and an installer-icon
  follow-up.
- Recovery work on 2026-07-23 restored clean formatting and a Release build with zero warnings or
  errors. All 111 existing automated tests pass locally. New Phase 3–7 behavior still requires
  dedicated regression, security, UI, migration, and live tests before any delivery claim.
- `pmo/backlog.md` and `pmo/plan.md` were corrected so implemented/released capabilities are not reset
  to Not started and unfinished local work is not labeled Delivered.
- HCS governance MCP reports no registered repo named `vault-prospector` and cannot resolve this
  local path. Continue using HCS standards/tools when applicable, but do not claim drift validation
  until registry/path resolution is fixed.

### Immediate next actions

1. Complete Phase 3 key-rotation design/implementation and live Windows unlock/recovery validation.
2. Complete Phase 4 workload-profile validation, availability detection, credential isolation,
   negative tests, and live Azure evidence before exposing it as supported.
3. Keep each later phase behind the plan's security, authorization, testing, and release gates.

### Phase 3 progress — 2026-07-23

- First run now completes local verification before opening directly on Identities. The guided
  sequence separates Windows local unlock, Microsoft-controlled authentication, and metadata-only
  synchronization; the connection action names the selected authentication method.
- Focused application tests pass 71/71. The synchronized locked Release gate passes dependency
  vulnerability inspection, formatting, a zero-warning/error build, and all 236/236 tests.
- Replaced the direct database/key deletion prototype with typed `RESET` confirmation, fresh Windows
  verification, complete local-state archival, and mandatory restart.
- Windows verification now distinguishes verified, canceled, unavailable, not configured,
  disabled-by-policy, and failed outcomes; every non-verified outcome remains locked.
- Accepted ADR `docs/adr/0009-preserve-and-archive-failed-local-state.md`; cross-device
  backup/restore is explicitly unsupported, while same-account failed state remains preserved.
- Added application, platform, UI, schema, persistence, cancellation, and reconciliation
  regressions. Release build passes with zero warnings/errors and all 126 tests pass locally.
- Implemented an internal, non-user-exposed all-or-rollback local encryption rotation engine:
  verified matched-state archive, HMAC-authenticated journal/manifest, SQLCipher rekey, offline
  envelope re-encryption, staged DPAPI key publication, validation, and startup rollback.
- Failure injection covers all nine published checkpoints plus a crash inside key publication.
  Tampered journals/archives fail closed. Filtered rotation tests pass 13/13; Platform tests pass
  21/21. Evidence: `docs/release-evidence/local-encryption-rotation-2026-07-23.md`.
- Settings now inventories canonical reset, pre-rotation, and failed-rotation archives and permits
  exact selected-archive deletion only after `DELETE ARCHIVE`, fresh Windows verification,
  containment/reparse checks, and confirmation that no rotation journal is active. Focused
  application/platform/UI coverage passes 14/14, including failure to write the pre-delete audit.
- A captured rotation rerun found transient Windows denial during atomic journal replacement.
  Bounded cancellation-aware retry now covers only transient I/O/access failures; permanent
  failures remain fail-closed. The fresh filtered crash-boundary suite passes 13/13 in 1m12s.
- The exact post-remediation `scripts/Build.ps1 -Configuration Release` gate passes locked restore,
  direct/transitive vulnerability inspection, formatting, zero-warning/error Release build, and
  all 210/210 tests with coverage artifacts for all seven projects.
- Still open: independent review, live Windows forced-termination/power-loss/reinstall evidence,
  and only then a verified user-presence rotation command.

### Phase 4 progress — 2026-07-23

- Interactive Entra remains the default; managed identity is omitted unless an Azure host endpoint
  or IMDS is detected.
- Managed-identity and certificate service-principal profiles validate ARM token acquisition before
  encrypted persistence. Client secrets are rejected.
- Service-principal tenant/client IDs and certificate thumbprints are canonicalized; certificates
  must be currently valid and expose a private key.
- Workload removal never opens or mutates human MSAL caches; workload reauthentication uses
  application ARM scope rather than interactive delegated scope.
- Automated contract, negative, persistence, endpoint-detection, UI, and token-cache isolation
  coverage passes. Release build has zero warnings/errors and all 136 tests pass locally.
- Federated service-principal profiles now store only a canonical readable projected-token path and
  use `WorkloadIdentityCredential`; token content never enters app persistence or human MSAL caches.
- Certificate and federated replacements validate ARM token acquisition before persistence.
  Explicit local revocation fails closed, removes credential references, purges discovered-vault
  offline copies, and requires external issuer revocation for compromised credentials.
- Synchronization now re-reads persisted identity state and all online value operations reject
  disabled/revoked/non-ready identities. Fixed identity lifecycle events use centralized redaction.
- ADR-0012 records credential ownership, isolation, rotation, and revocation decisions.
- Release build passes with zero warnings/errors. Targeted Phase 4 suites pass: Application 39,
  Azure provider 13, App 60, Infrastructure 36. Full-suite verification remains to be rerun.
- Still open: live managed-identity/certificate/federated tenant and issuer-revocation evidence,
  plus independent security/redaction review.

### Phase 5 progress — 2026-07-23

- Replaced the stub that ignored `subscriptionId` and returned `null` for dry runs.
- Managed-identity discovery binds to the exact requested subscription and selected ready
  interactive identity. Service-principal discovery now uses a separate explicit delegated
  `Application.Read.All` consent action and the app-owned MSAL account.
- Microsoft Graph discovery accepts only HTTPS `graph.microsoft.com` pages, disables redirects,
  enforces page/item bounds, binds returned interactive auth to the selected home account, and has
  positive/negative pagination tests.
- The new Administration tab distinguishes confirmed visibility from unproven attach/use,
  management, Key Vault, and role-assignment rights.
- Deterministic managed-identity and service-principal previews validate tenant/subscription GUIDs,
  resource names, exact Key Vault and role-definition resource types/same-subscription scope, and
  declare `PerformsMutations=false`. No execution command exists.
- Removed the unused Azure Authorization package introduced by the stub and regenerated locked
  dependency graphs.
- Locked restore, formatting, Release build with zero warnings/errors, and all 141 tests pass.
- Added explicit candidate-plus-Key-Vault authorization assessment. The selected administrator's
  exact-resource caller permissions are separate from the candidate's inherited/transitive role
  grants, action exclusions, deny assignments, child-scope behavior, and conditions.
- HTTPS ARM endpoints and next links are host constrained and bounded. Conditions, access-policy
  vaults, unreadable deny sets, and possible group-deny applicability fail closed as unproven.
  No candidate credential, data-plane value, role write, or other Azure mutation is used.
- ADR-0013 records the static-evidence/runtime distinction. Focused provider and app tests cover
  inherited allow, deny precedence, conditional grants, unavailable deny visibility, untrusted
  pagination, access-policy mode, and the user-reachable selection workflow.
- The exact `scripts/Build.ps1 -Configuration Release` gate passes locked restore, direct/transitive
  NuGet vulnerability inspection, formatting, a zero-warning/error Release build, and 218/218
  tests with coverage for all seven projects. Evidence:
  `docs/release-evidence/workload-authorization-evidence-2026-07-23.md`.
- Still open: Phase 8 independent review, fresh write authorization,
  confirmed/encrypted-audit execution, rollback, and live Azure tests.

### Phase 6 progress — 2026-07-23

- Added per-identity subscription and per-access-path vault inclusion controls to the identity UI.
  Choices persist in encrypted metadata and are applied before provider metadata enumeration.
- Excluded vault/access records are retained so users can reverse the choice; complete
  synchronization tombstones excluded indexed objects without deleting history.
- Vault access paths now show identity, tenant, subscription, management visibility, observed
  secret/key/certificate metadata-list outcomes, explicit value-read non-probing, and
  policy-disabled writes.
- Schema v4 adds backward-compatible vault selection, with v3 migration coverage. Workload identity
  hydration from search resolution was corrected for schema-v3 fields.
- Formatting and all 148 automated tests pass locally after this slice.
- Still open: live Azure permission matrices, independent redaction/security review, and release
  evidence.

### Phase 7 progress — 2026-07-23

- Workspaces now support user-reachable identity, tenant, subscription, and vault assignment.
- The selected workspace has editable encrypted-cache enablement/lifetime and clipboard policy;
  Windows verification remains mandatory and cannot be disabled.
- Copy/cache commands enforce the selected workspace override. Workspace deletion transactionally
  removes links after the application purges workspace-scoped offline values.
- Complete discovery tombstones missing/excluded objects; partial failures preserve prior state.
  Scope records remain available for reversal.
- Automated workspace resource-type, policy, search-scope, deletion, and schema migration coverage
  is present. Live upgrade/downgrade/reinstall and independent cache-boundary validation remain
  open release evidence.

### Phase 8 gate — 2026-07-23

- Added proposed ADR-0010 and `docs/security/governed-write-threat-model.md`.
- Removed the unused generic `IsWriteModeEnabled` property because the charter forbids an
  unrestricted write toggle.
- Azure mutation code remains absent/read-only while the required threat-model and independent
  security review gate is open. The proposed operation set and policy/authorization/verification/
  preview/concurrency/audit/recovery boundaries are documented.

### Phase 9 progress — 2026-07-23

- Added official-source comparative research, explicit limitations, task-flow synthesis, and an
  eight-participant usability/accessibility study protocol under `docs/design/`.
- Delivered one interactive React prototype with four switchable concepts: Source-first,
  Search-first, Guided tasks, and Operations console. Each contains Setup, Search, Secret reveal,
  and Settings, for 16 inspected combinations.
- Native `npm run build` passes; headless Chrome exercised all combinations with zero console
  errors, and a 390-pixel viewport had no horizontal document overflow. Four reference screenshots
  are checked in.
- Initial hypothesis favors Source-first, but no final selection claim is made. Representative
  Windows users, Narrator/NVDA/keyboard/High Contrast evidence, selection, and production
  implementation remain open.

### Phase 10 progress — 2026-07-23

- Added persisted Ask, Exit, and Lock-to-notification-area close behavior plus an explicit close
  choice overlay.
- Notification-area menu provides Show/Exit and reports Locked, Syncing, Action required, Azure
  interaction required, Offline, or Ready.
- Backgrounding cancels active work, advances the sensitive-presentation epoch, masks values,
  locks foreground access, and hides both window and taskbar entry.
- Opt-in 15-minute background work invokes only metadata synchronization while hidden and
  network-available. Post-provider cancellation checks prevent clipboard/cache/presentation
  release after background cancellation.
- A disposable production Windows monitor locks on every session switch and on suspend/resume,
  marshals the lock to the UI thread, and detaches its static handlers during shutdown. The lock
  cancels active work, invalidates sensitive presentation, closes any close prompt, masks the
  preview, and requires foreground unlock again. Ordinary power-status changes do not lock.
- The exact Release gate passes locked restore, dependency-vulnerability inspection, formatting,
  zero-warning/error build, and all 231 tests. Live installed tray, sleep/resume, Windows session
  transitions, battery/network transitions, token expiry, and accessibility evidence remain open.

### Cross-phase code-review closure — 2026-07-23

- Completed a security, correctness, performance, and maintainability review of the accumulated
  Phase 3–10 worktree.
- Revocation now persists revoked state before cleanup, attempts provider removal and every
  historical associated vault purge independently, and reports residual failures without restoring
  access.
- Added non-destructive identity-scoped offline-value purge to the production Identities UI.
- Persisted sync errors no longer copy authentication exception messages.
- Added response/file bounds for Graph, ARM, settings, protected-value, and authenticated rotation
  JSON; Graph next links require HTTPS on the default `graph.microsoft.com` port.
- Tray status preserves locked and operational context.
- The first exact gate run exposed a transient Windows directory-move denial during injected
  `OfflineKeyPublished` crash recovery. Bounded transient retry plus non-cancelable post-move
  promotion/rollback fixed that recovery boundary; all nine injected crash checkpoints then passed.
- Final exact `scripts/Build.ps1 -Configuration Release` passes locked restore, direct/transitive
  vulnerability inspection, formatting, zero-warning/error Release build, and **254/254** tests:
  Domain 4, Application 53, Infrastructure 50, Platform 37, Azure provider 27, App 82, Security 1.
- Evidence:
  `docs/release-evidence/cross-phase-security-correctness-review-2026-07-23.md`.
- Internal review does not approve the independent security gate. Live Azure/Windows,
  representative-user/accessibility, exact packaged-candidate, and external governance drift
  evidence remain open.
- Commit `41aa0c5` was pushed to `main` with an HCS-minted GitHub App token. CI run `30062290743`
  passed restore, formatting, build, all tests, and PowerShell parsing but then exposed an MSI
  shortcut-icon mismatch and five Gitleaks false positives from one synthetic certificate
  thumbprint fixture.
- Local follow-up removes the obsolete MSI executable-icon row, binds the shortcut to the embedded
  `.ico`, constructs the fixture at runtime, and constrains the unavoidable historical exception
  by exact value/file/commit. Rebuilt MSI upgrade/icon checks pass, pinned Gitleaks v8.30.0 reports
  no leaks across 80 commits, and the exact 254-test Release gate passes again.
- Follow-up commit `807a8ed` was pushed with an HCS-minted GitHub App token. Exact-commit CI run
  `30063106013` passed both `secret-scan` and `build-test`, including restore, formatting, build,
  all tests, PowerShell parsing, MSI/package validation, vulnerability inspection, and
  commit-addressed candidate upload.

## Current state

- Branch: `main`; direct pushes are the operator-approved workflow.
- Public release: `v0.1.0-preview.2` is **withdrawn** and retained only for immutable evidence and
  existing-install repair/uninstall. Do not install, resubmit, or reuse its artifacts.
- Current public Preview: unsigned non-production `v0.1.1-preview.1` at
  `https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.1.1-preview.1`.
  CI.68 is superseded; Preview.2 remains withdrawn.
- Authoritative gate matrix: `docs/product/release-readiness.md`.
- Decision: `0.1.1-preview.1` is GO and published for non-production evaluation. GA remains blocked
  by the documented open readiness gates.
- Repository writes must use an HCS governance-minted GitHub App installation token for
  `Hybrid-Solutions-Cloud`; never use a personal token.

## Validation completed on 2026-07-17

- CI now retains a commit-addressed unsigned Windows validation artifact for 14 days.
- Artifact `windows-candidate-50f6e2f9321b4441830aee953809d879f099e267`, ID
  `8400198059`, was downloaded from the passing workflow rather than rebuilt locally.
- Host and clean-guest provenance, MSI, WinGet archive, Chocolatey package, and checksum hashes
  matched.
- On Windows 11 Enterprise Evaluation 25H2 x64 with Secure Boot and TPM, the exact MSI passed
  silent installation, Start-menu launch, forced repair, silent uninstall, cleanup, retained-data,
  and byte-identical pre-test state restoration.
- The installed package passed five-view empty-state Windows UI Automation target checks at default
  and 200% Windows text size, plus real High Contrast Black focus/readability inspection.
- The VM was restored: no install, process, shortcut, or test roots; `TextScaleFactor` absent; High
  Contrast `Flags=126`; original `%LOCALAPPDATA%\VaultProspector` inventory restored.
- Evidence is recorded in
  `docs/release-evidence/ci-packaged-windows-candidate-2026-07-17.md`; related accessibility and
  readiness documents are synchronized.
- Evidence commit `4ec5d43` is pushed to `main`; exact-commit CI run `29565396801` passed both the
  build/test/package and full-history secret-scan jobs. GitHub issues `#5` and `#8` are synchronized.
- The next source candidate explicitly preserves the initiating control across asynchronous
  operations and waits for both operation completion and window reactivation before restoring
  keyboard focus. Four coordinator tests cover active/inactive and valid/invalid target behavior.
- A live Windows system-browser launch followed by in-app cancellation returned visible and UI
  Automation keyboard focus to **Continue to Microsoft sign-in**.
- Focus-return evidence is recorded in
  `docs/release-evidence/windows-external-focus-return-2026-07-17.md`. The VM and host scratch are
  restored and clean.
- Official NVDA `2026.1.1` testing on final local candidate `0.1.69` proved secondary-tab focus
  announcements, routine status, complete safe actionable-error guidance, browser cancellation
  status, and initiating-control return. The guest has no audio endpoint, so the proof is NVDA's
  speech queue and Speech Viewer rather than audible output.
- The locked Release gate passed with no known vulnerable packages, formatting unchanged,
  0 warnings/errors, and 84/84 tests. NVDA evidence is recorded in
  `docs/release-evidence/windows-nvda-accessibility-2026-07-17.md`.
- NVDA remediation and evidence commit `133f87f` is pushed to `main`; exact-commit CI run
  `29580268318` passed both `build-test` and full-history `secret-scan`. GitHub issues `#5` and
  `#8` are synchronized, while P-15 correctly remains open for its unverified requirements.
- A subsequent internal security pass found three additional boundaries: orphaned MSAL accounts
  after metadata-write failure, missing offline-open audit/disposal behavior, and clipboard
  ownership retaining a second plaintext string. Source and regression tests remediate all three;
  the locked local gate passes 88/88 tests. The independent-review execution plan is at
  `docs/security/independent-review-plan.md`; internal work does not approve P-08.
- Security hardening commit `f586639` is pushed to `main`; exact-commit CI run `29582360571`
  passed both `build-test` and full-history `secret-scan`. GitHub issue `#9` now tracks independent
  P-08 execution, and issue `#5` is synchronized without incorrectly checking the gate.
- A follow-up offline-cache attack pass found that descriptor metadata was checked before AES-GCM
  authentication during retrieval and trusted directly during scoped purge. Source now
  authenticates before item/expiry/fingerprint/scope decisions and removes untrusted entries
  conservatively. Twelve additional cases cover cryptographic and descriptor tampering, missing
  fields, malformed encodings, cross-item substitution, version behavior, and purge continuation;
  the locked local gate passes 100/100 tests.
- Offline-cache hardening commit `9a602ba` is pushed to `main`; exact-commit CI run `29583938082`
  passed both required jobs. GitHub issues `#5` and `#9` contain the findings, remediation, test
  scope, and explicit remaining independent/runtime boundary.
- A transient production-service probe on the clean Windows guest proved the noninteractive
  unavailable Windows Hello boundary: Windows returned `DeviceNotPresent`, `VerifyAsync` returned
  `false`, and the task exited `0`. No logged-in Explorer session existed, so interactive
  success/cancel was not attempted without operator confirmation. All task, credential, guest, and
  host probe artifacts were removed. Evidence is in
  `docs/release-evidence/windows-hello-unavailable-2026-07-17.md`.
- Windows Hello unavailable-boundary evidence commit `141c456` is pushed to `main`; exact-commit CI
  run `29585217953` passed both `build-test` and full-history `secret-scan`. GitHub issues `#5` and
  `#9` are synchronized with the narrow result; P-05 and P-08 remain open because interactive and
  independent-review evidence is still outstanding.
- P-09 failed-upgrade testing found a real installer defect: WiX's default major-upgrade schedule
  left no installed version after a deterministic post-`InstallFiles` failure. The package now
  schedules `RemoveExistingProducts` after `InstallInitialize`. An unreleased corrected candidate
  then passed 27/27 gates from immutable Preview.2, including exact registration/file/shortcut/state
  rollback, successful upgrade/repair, downgrade rejection, uninstall, and cleanup. Evidence is in
  `docs/release-evidence/windows-installer-failed-upgrade-2026-07-17.md`; final signed-candidate and
  independent repetition remain open.
- Installer rollback fix/evidence commit `4853598` is pushed to `main`; exact-commit CI run
  `29588980444` passed both `build-test` (including the built-MSI sequence guard) and full-history
  `secret-scan`. GitHub readiness issue `#5` is synchronized without incorrectly passing P-09.
- Local G-03 recovery hardening now requires existing keys for existing SQLCipher/AES-GCM state,
  rejects future, corrupt, wrong-key, incomplete-schema, and invalid-relationship databases, and
  provides distinct redacted recovery guidance. The Release-equivalent local gate passes 111/111
  tests with 0 warnings/errors and no known vulnerable packages. Evidence is in
  `docs/release-evidence/local-data-recovery-2026-07-17.md`. Commit `6cec5a4` is pushed to `main`;
  exact-commit CI run `29592049330` passed both `build-test` and full-history `secret-scan`.
- Exact local `0.1.1-preview.1` packaging passed the locked Release gate (111/111 tests, zero
  warnings/errors, no known vulnerable packages), MSI schedule guard, WinGet validation, and
  Chocolatey packing. The exact MSI then passed all 27 lifecycle gates on isolated Windows 11,
  including deterministic failed-upgrade rollback. The VM was restored with zero registrations or
  processes, its test root removed, and its pre-existing encrypted database/key hashes unchanged.
  Evidence is in `docs/release-evidence/0.1.1-preview.1.md`.
- Tagged source commit `1cc391012de370bdb783485e28043642e000e288` passed CI run `29611412932`;
  protected release run `29611845899` produced 13 assets. Their exact bytes were mirrored to the
  public release and all 13 passed credential-free re-download comparison. The public MSI SHA-256
  is `9F9DC0C04362F979FFA064D274961241F12D8789D910CE53C57B6EE119B5C8B0` and separately passed all
  27 lifecycle gates. The guest was fully restored after validation.

## External publication state

- Public unsigned Preview `v0.1.1-preview.1` is live with MSI, portable ZIP, WinGet bundle,
  Chocolatey NUPKG, checksums, SPDX SBOM, Sigstore bundles, release notes, limitations, and Unknown
  Publisher guidance. CI.68 is superseded.
- WinGet PR `microsoft/winget-pkgs#403473` is closed with a withdrawal notice. Microsoft validation
  had passed, but the later failed-upgrade result invalidated the submitted Preview.2 MSI. Submit a
  new PR only after the immutable `0.1.1-preview.1` MSI is publicly published and hash-verified.
- Chocolatey never ingested `0.1.0-preview.2` after six HTTP 504 responses. That version is now
  withdrawn and must not be retried. Submit the rollback-safe `0.1.1-preview.1` NUPKG only after
  public hash verification and upload-path recovery, then verify ingestion and moderation.
- Trusted Windows signing gate P-13 remains blocked until an HCS owner completes Azure Artifact
  Signing Public Trust identity and profile setup. The workflow permits only explicitly versioned
  unsigned Preview evaluation tags without it; stable/GA tags remain fail-closed.
- HCS drift cannot currently validate this local checkout because the MCP server cannot resolve the
  unregistered repository/path; do not report a drift pass.

## Phase 11 browser integration (local, unreleased)

- Active branch: `feature/browser-origin-bound-fill`.
- Added Chromium/Firefox MV3 extension source, strict browser protocol, native host, authenticated
  current-user broker, exact host-process and extension identity checks, encrypted mappings,
  value-free audit, protected fail-closed machine policy, desktop confirmation, and fresh Windows
  verification.
- MSI packaging includes the native host, disabled-by-default machine policy, and exact HKLM Chrome,
  Edge, and Firefox native-host registrations.
- The full locked Release gate passes 318/318 .NET tests with zero warnings/errors and no known
  vulnerable NuGet packages. Extension tests pass 6/6. Local MSI `0.1.0-ci.1002` passes rollback,
  icon, native-host, disabled-policy, and three-registration checks; exact-commit CI remains.
- Phase 11 remains validation-open: no signed extension packages, live installed-browser matrix,
  independent review, compromise/revocation exercise, or representative-user/AT evidence yet.
- Exact-branch review found and fixed a lock race by cancelling pending/in-flight retrieval and
  zeroing any approved response that loses the completion race. The first PR CI secret scan also
  identified the Chromium public key; `.gitleaks.toml` now has an exact-value/exact-path public-key
  exception, and local full-history scanning passes.
- Canonical docs: `docs/browser-integration.md`,
  `docs/security/browser-integration-threat-model.md`, ADR-0014, spike-0009, and
  `docs/release-evidence/browser-integration-phase-11-2026-07-23.md`.

## Next actions

1. Synchronize the Phase 12 merge evidence without marking external gates complete.
2. Execute Phase 13 mobile applications in the canonical plan, beginning with architecture,
   threat models, shared contract boundaries, and platform toolchain/repository constraints.
3. Continue remaining external GA work: signing, independent security review issue `#9`, live
   Entra/MFA/Conditional Access/Windows Hello/browser/CyberArk testing, package-catalog ingestion,
   accessibility/usability evidence, and G-01 feedback.

## Preserved external scratch

Do not delete `D:\tmp\vault-prospector-untracked-AzureIdentityAndVaultProvider.cs`. It is a
quarantined pre-existing untracked source file with SHA-256
`69AC58A44284A1D5B3947F81783288BE19B64C41ECECAC7538C874829849BBDC`; it is intentionally outside
the repository and must not be committed.

## Phase 12 CyberArk Privilege Cloud (merged, unreleased) — 2026-07-24

- PR `#11` merged exact head `31a4f3918cc92d529150bd8578047989c562497c` as
  `6b9d5cd85ca453e561c34d966bfc47efc581b551`. Exact-commit CI run `30069509556`
  passed `build-test` and `secret-scan`.
- ADR-0015 selects CyberArk Privilege Cloud Shared Services with CyberArk Identity service-user
  authentication. Conjur, on-premises PVWA, custom domains, and interactive authentication are
  outside this first provider boundary.
- Added dedicated CyberArk profiles, safes, accounts, versions, direct safe-member evidence,
  provider errors, and value-free audit models. They do not reuse Azure identities, RBAC, vaults,
  or token caches.
- Added strict supported-origin authentication/API behavior, bounded response/pagination/item
  processing, safe error categories, metadata-only discovery, and exact account/version retrieval.
- Added SQLCipher schema v6 and atomic profile-scoped discovery replacement. Credential/tokens,
  retrieval reason, and values do not enter metadata.
- Added a per-profile DPAPI `CurrentUser` credential store with profile-specific entropy, bounded
  files, zeroed buffers, exact removal, cross-profile replay rejection, and reparse-point rejection.
- Added a separate desktop CyberArk destination for profile connect/rotation, local search,
  safes/accounts/versions/direct-permission evidence, fresh-verified reveal/copy, local audit,
  enable/disable, fail-closed local revocation, and permanent removal.
- Added ADR, threat model, integration guide, privacy/architecture/security/release evidence, and
  canonical backlog/plan/readiness/roadmap updates.
- Exact local `pwsh ./scripts/Build.ps1 -Configuration Release` passed locked restore, vulnerable
  package scan, format, zero-warning/error build, coverage collection, and 342/342 .NET tests:
  Domain 4, BrowserProtocol 35, Application 66, Security 1, Platform 50, Azure 27, App 85,
  BrowserHost 8, Infrastructure 54, CyberArk provider 12.
- Browser extension tests pass 6/6 and its production bundle builds.
- Disposable unsigned MSI `0.1.1-preview.12` packaged locally. Shortcut/icon,
  rollback-safe-upgrade, and browser-host/policy inspections passed, and its File table contains
  `VaultProspector.Providers.CyberArk.dll`. The generated artifacts were moved out of the worktree
  to `D:\tmp\vault-prospector-phase12-artifacts-20260724`.
- HCS `which_standards_apply("vault-prospector")` resolved standards by repository type, but
  `check_drift` still returned Path not found. Do not claim drift passed.
- Remaining Phase 12 gates: governed live Privilege Cloud matrix, external service-user
  revoke/rotation drill, independent security review, representative user/AT evidence, and exact
  signed release artifact validation.

## Immediate next actions

1. Synchronize the Phase 13 merge evidence after exact merge-commit CI completes.
2. Complete the native Apple credential-provider and Android Autofill/Credential Manager
   feasibility prototypes and record the capability decision.
3. Continue the mandatory external gates across Phases 8–15: independent review, governed live
   services and physical devices, usability/accessibility, protected signing, package/store
   acceptance, evaluator thresholds, and the stability window.

## Phase 13 native mobile applications (merged, unreleased) — 2026-07-24

- PR `#13` merged exact head `a12b0b024d8cbea2263ac22668708753f6b91c8e` as
  `ead0a29faa4802008ac4d7b0e9c1c10ad881d2df`.
- Exact PR-head CI run `30076673071` passed build-test and secret-scan. Mobile CI run
  `30076673064` passed 19 managed mobile tests, Android Release App Bundle packaging, and an
  unsigned iOS simulator application on macOS 26/Xcode 26.0.1.
- Exact merge-commit CI run `30077519402` passed build-test and secret-scan. Exact merge Mobile CI
  run `30077519354` passed managed tests, Android packaging, and the iOS simulator application.
- Local verification passed locked restore, dependency vulnerability checks, formatting, 343
  desktop/shared tests, 19 mobile tests, Android Release linking/native compilation with zero
  warnings, iOS reference-pack compilation, and staged secret scanning.
- Added a separate .NET 10/Avalonia mobile solution, shared fail-closed session/search/retrieve
  workflow, Android API 31+ host, and iOS 18+ host.
- Android uses authentication-bound Keystore protection, BiometricPrompt, `FLAG_SECURE`,
  obscured-touch rejection, ownership-aware sensitive clipboard clearing, and backup/transfer
  exclusions.
- iOS uses current-biometric-set device-only Keychain protection, LocalAuthentication,
  protected-data lifecycle locking, privacy covering/capture response, expiring local-only
  pasteboard writes, backup exclusion, and a privacy manifest.
- The production Entra registration preserves desktop loopback and adds the exact mobile callback;
  no application credential was created.
- Project-owned reflection-based JSON paths were made trim-safe. The iOS linker keeps four grouped
  upstream `IL2104` warnings visible while project warnings remain build-breaking; the successful
  trimmed simulator build does not replace required physical-device testing.
- Open Phase 13 gates: enabled native autofill mapping/package-association and signed-device
  positive/negative framework testing, governed Entra/device/accessibility matrices, signed
  Android/iOS artifacts, independent mobile review, TestFlight/Play closed testing, declaration
  approval, and store acceptance.

## Phase 13 native autofill feasibility — 2026-07-24

- Added a two-stage shared analyzer: native metadata must contain one canonical default-port HTTPS
  DNS origin and unambiguous username/password hints; value release separately requires a secret,
  exact saved mapping, foreground invocation, and fresh verification.
- Isolated that policy in a dedicated assembly with only domain/browser-origin references so the
  credential-provider extension cannot inherit Azure, SQLCipher, cache, or application services.
- Added a real Android `AutofillService` with bounded `AssistStructure` parsing, required manifest
  permission/action/metadata, and no save/import behavior. The component is package-disabled and
  returns no dataset pending Digital Asset Links/package-signature, mapping, verification, device,
  and review gates.
- Added and embedded an Apple credential-provider extension with the required entitlement on both
  targets. It normalizes service identifiers, returns `UserInteractionRequired` for no-UI
  requests, has no shared app database/Keychain group, and returns no credential.
- Completed SPK-0007 with the cross-platform capability decision and primary vendor references.
- Local checks pass: 44 mobile tests, locked restore/format/vulnerability checks, Android Release
  App Bundle with zero warnings/errors, and iOS application/extension reference-pack compilation.
- Remaining evidence: hosted macOS bundle CI on the exact PR head, enabled signed physical-device
  framework matrices, encrypted one-record mapping exchange, Android association/signature
  validation, accessibility, independent review, and store acceptance.

## Phase 13 native autofill merge evidence — 2026-07-24

- PR `#15` merged exact head `bf34e178b8c5c531718a505c507d0752a5bc3d1c` as
  `69c4c9e0fc84b7485ea019cf8f9bbfd466516896`.
- Exact PR-head CI run `30080022795` passed build-test with 343 desktop/shared tests and
  secret-scan. Mobile CI run `30080022802` passed 44 managed tests, Android Release packaging, and
  the unsigned iOS application plus embedded credential-provider extension on macOS 26/Xcode
  26.0.1.
- Exact merge-commit CI run `30080923681` and Mobile CI run `30080923682` failed before any step
  started. Every job annotation reports that recent organization payments failed or the spending
  limit must be increased. These runs are infrastructure failures and provide no code result.
- Rerun both workflows on the exact merge commit after the Hybrid-Solutions-Cloud GitHub Actions
  billing/spending condition is corrected. Do not describe the passing PR-head jobs as
  merge-commit evidence.
- HCS bootstrap resolved the default `hcs` standards by type. HCS drift validation still returns
  `Path not found` for this checkout, so no drift pass is claimed.

## Phase 14 G-05 performance and scale — 2026-07-24

- PR `#15` is merged as `69c4c9e0fc84b7485ea019cf8f9bbfd466516896`. Its exact-head
  checks passed, but merge-commit workflows `30080923681` and `30080923682` could not start because
  of the organization Actions payment/spending limit. Evidence correction PR `#16` is open and its
  checks are affected by the same condition.
- Commit `04688ff3a386a120ef9d975fe91a65a4000c1953` adds a controlled performance
  gate over the production synchronization service, encrypted SQLCipher repository, and search
  service. CI is configured to publish its JSON report with the other test results.
- The supported controlled profile is 10 identities, 20 tenants, 200 subscriptions, 200 vaults,
  50,000 metadata objects, and 60 measured searches.
- The first run exposed metadata persistence beyond three minutes and search at 3,532 ms p95 /
  6,047 ms maximum. Bounded 50-row parameterized upserts, one-time SQLCipher effective-key
  derivation, and deterministic ranked access-path selection corrected the production paths.
- Exact-source local results pass: initialization 329 ms, 50,000-object sync 6,595 ms, repository
  reopen 1,318 ms, search p95 262 ms, search maximum 275 ms, cancellation 4 ms, private memory
  41.4 MiB, and encrypted database 24.6 MiB.
- Connection pooling remains disabled. The effective SQLCipher key is stored in a mutable buffer
  and zeroed on repository disposal; a regression proves legacy passphrase databases remain
  readable. The full locked Release gate passes 345/345 tests with zero warnings/errors and no
  known vulnerable dependencies.
- G-05 remains In progress pending clean-machine packaged startup, representative supported and
  low-resource devices, live throttled/partial/cancel/resume provider sync, populated UI/AT
  responsiveness, and exact signed-candidate repetition.

## G-08 operational readiness (local, unreleased) — 2026-07-24

- Branch `feature/operational-readiness` starts from merged main commit
  `69c4c9e0fc84b7485ea019cf8f9bbfd466516896`.
- Implementation commit `3410f77f71a374eca684b6d97f3936a8693ee1d3` adds the machine-readable
  readiness contract, fail-closed validator, weekly Dependabot coverage, scheduled
  vulnerability/runtime/public-endpoint monitor, and support/EOS policy.
- Exact implementation-head live validation passed 35/35 contract checks and returned HTTP 200
  for the current Preview release page, exact MSI checksum, and feedback channel.
- An injected `2026-11-11` observation correctly failed with `RUNTIME_END_OF_SUPPORT`.
- The locked Release gate passed restore, structured vulnerability inspection, format, zero
  warnings/errors, and 343/343 desktop/shared tests. Initial overlapping file-lock failures were
  traced to and cleared with the exact stale child process tree left by a command-wrapper timeout;
  unchanged source then passed.
- The official .NET support policy places desktop .NET 9 end of support at 2026-11-10. The monitor
  emits the intended 120-day warning; migrate before any support promise extends past that date.
- G-08 remains In progress. Open proof: named backup operator, successful retained hosted monitor
  runs, complete incident/withdrawal/communication/recovery exercise, approved Authenticode key
  lifecycle, and exact signed-public-candidate review.
- PRs `#16` (Phase 13 evidence) and `#17` (G-05 performance) remain open. GitHub-hosted jobs are
  rejected before the first step because the organization reports a payment/spending-limit
  problem; do not describe those startup rejections as code failures or merge without required
  passing checks.
