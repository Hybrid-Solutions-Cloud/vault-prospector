# Session handoff

## 0.3.0-preview.6 current release — 2026-07-27

- Exact-main source `8751df7f2a6c1014f3e51c4b570625364f9fb5f9` passed HCS run
  `30229406561`.
- Immutable tag `v0.3.0-preview.6` rebuilt, tested, packaged, generated SBOM and Sigstore evidence,
  and published 16 binary-only assets in release run `30233704752`.
- Independent public downloads matched all five adjacent package checksums.
- MSI SHA-256:
  `1FAABD20B917C26B2150DA49A6F1558CE1DAE0F2FA6E215038FB876EC5F0040C`.
- The HCS Tier-4 Windows runner was provisioned only for the release and cleanup was started after
  publication. No authoritative build ran on the operator workstation.
- Current Windows code and known bug fixes are included. Product-owner installed workflow testing
  and formal acceptance evidence remain pending.
- Signing, mobile, browser interoperability research, CyberArk, GA approval, operations, and
  legal/privacy approval remain deferred roadmap or external-gate work.

## 0.3.0-preview.5 current release and ADO reconciliation — 2026-07-26

- Current public source is `1a4f9f7fdc470c71d5faad4aaa819c1452a15799`; exact-main HCS run
  `30220348003` and immutable release run `30222244323` passed.
- Tag `v0.3.0-preview.5` published 16 binary-only assets. Independent public verification matched
  all GitHub digests and all five adjacent checksums.
- MSI SHA-256:
  `DBE46EB192912BA7317F0152B23494179781E2C94C22BA94DA778F7C8D10C29D`.
- Current Windows production code includes the approved Atlas UI, trusted updates, safe support
  bundles, remote-session unlock, reveal grace, discovered filters, notification-area behavior,
  service-principal filtering, browser setup diagnostics, isolated-error retry, and release-gated
  governed Azure mutations.
- PR #60 merged the verified release record. PR #61 merged the post-release ADO and cleanup record;
  current documentation main is `0ba5fcc022ffc5c3ae3f481f646c8518ed406822`.
- The private Agile ADO project is correctly named `Vault Prospector`. It has 198 work items:
  53 Closed, 1 Removed, and 144 open with named work or evidence. Zero open parents have only
  terminal children.
- GitHub Bug #62 and ADO mirror AB#5799 track the product-owner-observed missing installer
  logo/red-X placeholder. No fix has been attempted.
- GitHub Bugs #42 and #44 and ADO mirrors AB#5575 and AB#5611 are Closed. GitHub Bugs #39, #40,
  #41, and #43 remain open for their exact installed or live validation requirements.
- The Tier-4 Windows resource group is deleted, no repository runner remains registered, and both
  temporary build credentials are inactive and soft-deleted.
- Product-owner exact installed workflow validation remains pending. Do not infer it from the
  automated release result.
- Mobile remains a separate roadmap, CyberArk remains post-GA, and trusted Microsoft signing
  remains the final pre-GA distribution gate.

## 0.3.0-preview.3 corrected Atlas release — 2026-07-25

- PRs #52 and #53 merged the persistent grouped Atlas secure-unlock shell and its protected-main
  validation correction. Exact source:
  `866f434e6d39c647c34c86456fc7dac4827412f0`.
- HCS exact-main run `30184356857` passed portable validation, the Windows candidate,
  protected-main, and full-history secret scanning. Candidate MSI `0.3.0-ci.207` SHA-256:
  `6C7A448816776C31D3716202654D77AE2F298A2E015788D12DC0EA427D140C38`.
- The CI MSI installed in Windows 11 RDP, preserved local state, rendered the corrected Atlas
  startup without an automatic prompt, opened the explicit current-account prompt, and completed
  machine-qualified account verification.
- Immutable tag `v0.3.0-preview.3` points to that exact source. Release run `30185620476` passed
  all 18 steps and published 16 binary-only assets.
- Independent public verification matched all 16 GitHub asset digests and all five adjacent
  checksums. Cosign `v3.1.2` verified all five bundles against
  `release.yml@refs/tags/v0.3.0-preview.3`.
- Public MSI SHA-256:
  `778456E2B8BEBE595092961BCA19221F1E034AD31911E57D332EAA01FAD72C78`.
  It installed as `0.3.3`, product code `{361536EE-4EE0-497B-979E-19AD46B58E69}`, preserved five
  local-state files, rendered the Atlas secure-unlock startup, and opened the explicit RDP prompt.
- Evidence:
  `docs/release-evidence/atlas-secure-unlock-ci207-2026-07-25.md` and
  `docs/release-evidence/0.3.0-preview.3.md`.
- Exact-public every-state keyboard, assistive-technology, representative-user, and independent
  approval criteria remain open. Do not close AB#5592, AB#5601, AB#5571, or AB#5574 until those
  separate criteria pass.

## Atlas corrected exact-main acceptance — 2026-07-25

- PR #47 merged the approved C · Atlas production layouts; PR #48 fixed qualified-account Remote
  Desktop verification; PR #49 fixed the dark-header action contrast; and PR #50 fixed the
  Administration panel overlap, moved Setup check to Browser fill, corrected warning/policy
  palettes, and widened the navigation column.
- Exact-main source `ae976be1d7a486aa26ba8ec70d52a48ad4bfa6ef` passed HCS run
  `30181586109` across portable validation, full-history secret scanning, and the Windows
  candidate.
- Exact MSI `0.3.0-ci.201` SHA-256 is
  `FED4F4E877498A56EB6AEE0D9C3A86E4761BDEABD5DFF788057C8BF063EF30C1`.
- The retained Windows 11 Enterprise 25H2 guest upgraded from `0.3.199` to Installed Apps version
  `0.3.201`. A real RDP session unlocked using
  `VP-WIN11-PREVIE\vp-test-admin`.
- Direct installed review passed Connections, Search, Administration, Workspaces, Browser fill,
  Activity and support, Settings and updates, and About. Exact screenshots are retained under
  `docs/release-evidence/images/atlas-ci201/`.
- Candidate evidence is recorded in
  `docs/release-evidence/atlas-windows-candidate-2026-07-25.md`.
- This is not yet exact public-package evidence. Publish only a new immutable
  `v0.3.0-preview.2`, verify its public assets/provenance, install its public MSI, and repeat the
  walkthrough before closing AB#5592, AB#5601, or their parents.

## Atlas visual-parity correction — 2026-07-25

- The product-owner review found that the exact installed candidate retained legacy-derived
  content layouts beneath Atlas colors, navigation, and context chrome. It did not match the
  approved C · Atlas screens.
- Release run `30178225455` was cancelled before publication. No public
  `v0.3.0-preview.1` release exists; the private source tag is immutable and must not be reused.
- ADO Tasks AB#5589, AB#5591, AB#5592, AB#5600, and AB#5601 were reopened.
- Correction work continues in the existing worktree on `fix/atlas-production-parity`.

## Atlas exact-candidate functional acceptance — 2026-07-25

- Active branch `feature/desktop-ui-redesign`; draft PR #46; exact implementation head
  `01c2b820c01b64a4ddb2d83d917ea385c7d3a74a`.
- HCS run `30175377767` passed all three jobs, including all 432 Windows tests, the performance
  gate, package/readiness checks, and 27 Windows Installer lifecycle checks.
- Candidate `0.3.0-ci.190` MSI SHA-256:
  `506B43A01D91A0C6437D60B04852EDD00031723C73DD76675B410131AEC80A8B`.
- The exact MSI passed the Atlas installer UI, local fail-closed behavior without a verification
  device, actual RDP current-account credential verification, Atlas shell, updater, diagnostics,
  support-bundle export, minimize-to-notification-area, and locked restore from the tray icon.
- Support bundle SHA-256:
  `2B590E49BB18C4BBA74C936C69295D150C857D300E217BD7547495CD7433411D`;
  it contained only the privacy manifest in the fresh test profile.
- Candidate evidence is being recorded in
  `docs/release-evidence/atlas-windows-candidate-2026-07-25.md`.
- Do not close parent items requiring an exact public package until PR #46 merges, an immutable
  release is published, and that public package is rechecked.
- Still-open live matrices include two real Entra identities, isolated Azure sync failures,
  consecutive real reveals, installed Chrome/Edge/Firefox fill, tenant-scale service-principal
  discovery, and populated discovered-source filters.

## 0.2.0-preview.5 first-run replacement — 2026-07-25

- PR #36 corrected the clean first-run Identity Type null conversion defect and merged as
  `542be4679006c2a34ef1df3b58722ae8a844b1ae`.
- Exact-main run `30162673459` passed all three jobs on HCS-managed runners, including the
  zero-warning 375-test Windows build and packaging/readiness gates.
- Immutable tag `v0.2.0-preview.5` points to the exact merge commit. Release run `30163007720`
  passed and published 16 binary-only assets through the HCS GitHub App.
- Independent public downloads matched all five adjacent checksums. Cosign `v3.0.6` verified all
  five bundles against
  `release.yml@refs/tags/v0.2.0-preview.5`.
- The exact public MSI SHA-256 is
  `FDFF5FB0458012B558E1B0C51AA6BBD6A39FD3BBCFABBC44870795075C8B567B`.
- A fresh Windows 11 Enterprise 25H2 Hyper-V guest installed that exact MSI, enrolled Windows
  Hello, unlocked the application, and opened the first-run Identities workflow with
  `InteractiveUser` selected and no `InvalidCastException`.
- The retained acceptance screenshot SHA-256 is
  `FDB6AA4A12C3EC683BBDBDC11EADC56DB5A9AFE175EA2187A5DA79A795A6D35E`
  and is attached to AB#5542. AB#5542 is Closed.
- Parent AB#5348 remains open because its separate live Microsoft Entra,
  keyboard/screen-reader, independent-review, and exact-release Acceptance Criteria remain open.
- The public Preview.4 release is marked withdrawn and points to Preview.5. Historical tags and
  assets remain immutable.
- Release-record PR #37 merged as `29a957af86c022a8479ee46f39fab94d0f2377bb`;
  exact-main run `30164830620` passed portable validation, full-history secret scanning, and the
  Windows candidate on HCS-managed runners.
- Final cleanup deleted `rg-hcs-vp-winbuild-eus2-01`; zero Windows runners remain. The disposable
  Hyper-V guest is off at its clean baseline with zero snapshots, and disposable credentials,
  PIN, and transient captures were removed.
- No authoritative build, test, package, or publication step ran on the operator workstation.
  HCS Linux and ephemeral Azure Windows runners performed those operations; the workstation only
  orchestrated them and hosted the isolated acceptance-test guest.

## 0.2.0-preview.4 desktop verification replacement — 2026-07-25

- Installed `0.2.0-preview.3` exposed a release defect: the unpackaged desktop application called
  the UWP-only Windows verification request API. The installed application remained locked.
- The public `0.2.0-preview.3` release title and body now mark it **WITHDRAWN — DO NOT INSTALL**
  and link to `0.2.0-preview.4`; its immutable tag and assets were not changed.
- Windows returned `DeviceNotPresent` for both the availability probe and the correct HWND-bound
  desktop interop request in the active RDP session. Retrying in that session cannot open the
  Windows verification prompt.
- AB#5539 tracks the correction under parent User Story AB#5348. The exact public Preview.4 MSI
  passed real Windows Hello success, cancellation, locked-screen button re-entry, and
  button-initiated success in a dedicated Windows 11 Hyper-V basic-console session.
- The same session found a separate first-run null Identity Type binding error. Selecting
  `InteractiveUser` clears it; AB#5542 tracks the no-workaround correction and exact-package
  repetition. Parent AB#5348 remains open for that task and its broader live Entra,
  keyboard/screen-reader, independent-review, and exact-release evidence.
- PR #33 merged the HWND-bound interop correction and explicit Remote Desktop diagnosis as
  `e84d0f0e47605d9575a3306721adf3b50764c4d2`.
- Exact-main run `30158989872` passed all three jobs on HCS-managed runners, including the
  zero-warning 375-test Windows build, MSI/MSIX/package-manager candidates, installer/browser
  contracts, and readiness checks.
- Immutable tag `v0.2.0-preview.4` points to the exact merge commit. Release run `30159321059`
  repeated the zero-warning Windows build and all 375 tests, generated the SPDX SBOM and five
  keyless Sigstore bundles, and published through the HCS GitHub App.
- The public prerelease contains exactly 16 assets. Independent downloads matched all five
  adjacent package checksums; Cosign `v3.0.6` verified all five bundles against
  `release.yml@refs/tags/v0.2.0-preview.4`.
- The machine-readable operational contract now names `0.2.0-preview.4` as its only supported
  Preview and monitors that release page and exact MSI checksum.
- The current workstation did not perform the authoritative build, tests, packaging, or
  publication. Those ran on the HCS Linux and ephemeral Azure Windows runners.
- HCS bootstrap resolves the repository as the `hcs`-scoped `app` profile. Drift validation still
  returns `Path not found` for both the registered checkout and this temporary worktree, so no
  deterministic drift pass is claimed.

## 0.2.0-preview.3 published and independently verified — 2026-07-25

- PR #29 merged as `ea8b407707de7bb743ac27607f6bfa7b98df9801`; it isolates one-time
  .NET/SQLCipher/cryptographic activation from the encrypted-repository initialization metric
  while retaining the two-second limit. Exact-main run `30149406966` passed all three jobs.
- Immutable tag `v0.2.0-preview.2` built and packaged successfully but stopped before publication
  because Git Bash was absent from the one-shot runner `PATH`. It has no public release or assets
  and was not moved or reused.
- PR #30 merged as `f0ff8e7fc6190953620b4cf7d8aae4447875dfe2`; the Tier-4 bootstrap now
  verifies and exposes `C:\Program Files\Git\bin\bash.exe`. Exact-main run `30150138832` passed all
  three jobs.
- GitHub Actions release configuration now has the HCS GitHub App ID variable and private-key
  secret sourced from HCS Key Vault. Secret values were not committed.
- Immutable tag `v0.2.0-preview.3` points to `f0ff8e7fc6190953620b4cf7d8aae4447875dfe2`.
  Release run `30150472368`, attempt 2, passed the 371-test build, MSI/MSIX and distribution
  packaging, unsigned-package boundary, SPDX SBOM, five keyless Sigstore bundles, GitHub App token
  creation, and public publication.
- The public prerelease has exactly 16 assets. Independent downloads matched all five adjacent
  SHA-256 files; Cosign `v3.0.6` verified all five bundles against the exact tag-workflow identity.
- Final release-record PR #31 merged as `1fd7e2ac2d8a112cfe2c2712f75eca8433ab5dc1`;
  exact-main run `30151303811` passed all three jobs.
- AB#5309 closed after its evidence-audit Done condition passed. Parent AB#5308 remains New because
  its representative-device/live-provider Acceptance Criteria remain unmet.
- Final Tier-4 cleanup completed: the Azure resource group no longer exists, the repository has
  zero registered runners and zero open pull requests, and the ADO project has zero pipeline
  definitions.
- CyberArk and native mobile remain separate future-roadmap scope. Direct downloads remain
  explicitly unsigned; the no-cost trusted path remains Microsoft Store–signed MSIX.

## HCS runner and ADO reconciliation completed — 2026-07-25

- PR #26 merged as `c6748ccc87ad62fb9c6f3ac46c067360972acce4`; PR #27 merged as
  `a0370c3163e4389ac5fbf61b81f2921051533546`.
- HCS Tier-2 PR runs `30146345649` and `30146846301` passed. Exact-main runs `30146470563` and
  `30146971143` passed portable validation, full-history secret scanning, and the Windows
  candidate.
- The approved Tier-4 Windows fallback deployed successfully twice, including restart/reuse of
  the stopped VM. The final VM was stopped, cleanup completed, and
  `rg-hcs-vp-winbuild-eus2-01` no longer exists.
- ADO pipeline definitions 5, 6, and 7 were deleted only after replacement exact-main validation.
  Historical builds 284, 287, 290, and 295 remain evidence.
- All 137 ADO work items were audited against the HCS work-item standard. Formal Acceptance
  Criteria now exists on every non-Task item; every item has tags and priority; and the hierarchy
  has no terminal-state contradiction.
- AB#5095 and its children are terminal: four Tasks Closed, bld-01 repair Removed from product
  scope, and the User Story Closed. AB#5332/5333 also closed after current dependency and
  full-history secret-scan evidence was verified.
- Every remaining open User Story received a dated acceptance audit. CyberArk, native mobile, and
  Microsoft Store signing are Priority 4 `future-roadmap`; they do not block the unsigned Windows
  Preview. Governed Azure writes remain genuinely unimplemented and gated by design/security
  review. Other Windows stories remain open only for their named live, independent, Store,
  operational, or legal evidence.

## Release-scope and runner correction — 2026-07-25

- CyberArk and native mobile were moved to future-roadmap status and removed from Windows GA
  dependencies. The CyberArk source remains for future work, but its Windows tab is disabled.
- HCS runner inventory confirmed the repo-specific Azure Container Apps runner is healthy. GitHub
  Actions now targets that runner for portable validation and the ephemeral Tier-4 Windows VM for
  Windows-only packaging.
- Azure DevOps remains the work-item hierarchy. ADO build history is retained as evidence, but ADO
  pipeline YAML is retired after replacement GitHub validation succeeds.
- The paid Authenticode assumption was replaced with a free Microsoft Store–signed MSIX path.
  Direct MSI/ZIP artifacts remain explicitly unsigned with checksum, SBOM, and Sigstore evidence.
- The self-imposed 30-day collection window, evaluator/task quotas, and 14-day stability period
  were removed as hard GA blockers. G-01 now requires traceable workflow coverage, report
  disposition, zero known blockers, an exact-candidate suite after the final blocking change, and
  named approval.
- Stale PRs #16–#21 were closed as superseded by merged PR #22. Dependabot PR #23 was tested and
  merged as `62700679edd8141bed87d12e107d73278a1eb9e8`.

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
- Current public Preview: unsigned non-production `v0.2.0-preview.1` at
  `https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.2.0-preview.1`.
  It supersedes `v0.1.1-preview.1`; Preview.2 remains withdrawn.
- Authoritative gate matrix: `docs/product/release-readiness.md`.
- Decision: `0.2.0-preview.1` is GO and published for non-production evaluation. GA remains blocked
  by the documented open readiness gates.
- Repository writes must use an HCS governance-minted GitHub App installation token for
  `Hybrid-Solutions-Cloud`; never use a personal token.

## Release delivery completed on 2026-07-24

- PR `#22` merged as `c7c8cb3191e392901f1dc0c8271ab62a0947e758`.
- Exact `main` ADO CI build `284` passed Windows build/package and 370 tests, full-history secret
  scan, native iOS application plus credential-provider extension, and 44 managed mobile tests plus
  Android Release App Bundle.
- Immutable tag `v0.2.0-preview.1` points to that exact merge commit.
- ADO release build `287` passed tag/version, locked build/test, packaging, SBOM, and Key
  Vault-backed Cosign sign/verify stages. The final publication helper failed on an invalid
  `Convert.FromBase64String` overload; retained artifact `release-0.2.0-preview.1` was complete.
- The retained artifact was published as a one-time recovery through the HCS GitHub App boundary.
  The public prerelease has exactly 13 assets, and a credential-free check matched every public
  size and GitHub SHA-256 digest to the ADO artifact.
- Public package hashes are MSI
  `DC15AF609EE6D55933551D24339DB914060E9616D40604D2AD9F10E7625EA4F2`, ZIP
  `0C4017FC532704E5D3B86339A202C2A31E00D166972546216B5539A82F8F66F8`, NUPKG
  `D2C1A22C3CA13083B1C68D06D36D816326BC9505F90DD4BA9975499D61D584F9`, and WinGet archive
  `00EF9ED0DA0E56C9FB8FF43F9529A10FEDC13F335CC2899805520D41418F1DA2`.
- Corrective PR `#24` fixes private-key normalization in `Set-AdoGitHubAppToken.ps1`; a real HCS
  GitHub App token-mint smoke test passed, ADO PR build `288` passed all four jobs, and the PR
  merged as `ea1bbdccf96811acdd86d2a8f39893b488f91324`. Exact-merge `main` build `290` passed all
  four jobs.
- The exact public MSI passed all 27 installer lifecycle gates on isolated Windows 11 Enterprise
  Evaluation 25H2 from `2026-07-24T21:03:59Z` through `21:05:23Z`. The run covered `0.1.1` install,
  deliberate failed-upgrade rollback, `0.2.0` upgrade, repair, downgrade rejection, uninstall, and
  retained state. The checkpoint was restored and removed; final state is VM off, zero
  registrations, no test root, and Guest Service disabled.
- WinGet PR `microsoft/winget-pkgs#407541` is open and mergeable with CLA passed; acceptance is
  external and pending.
- Two exact Chocolatey submissions returned HTTP 504 and catalog lookup remains empty. Do not
  claim ingestion or catalog availability.
- The exact tag-guarded cleanup script deleted ephemeral resource group
  `rg-hcs-vp-winbuild-eus2-01`. Its two temporary Key Vault credentials were soft-deleted and
  remain recoverable under Key Vault retention policy.
- GA remains open for trusted Authenticode, independent security/legal, governed live Azure and
  CyberArk, representative accessibility/usability, physical-device/mobile store, operational
  exercise, and stability-window evidence.

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
- PR `#18` is open for G-08. Initial head `9aef1f31ad6b44a66bcdd4e7d18813d7c30f48e9`
  received CI run `30084503284`; both jobs had zero steps and the same payment/spending-limit
  startup rejection. The PR is mergeable by Git but must remain unmerged until required exact-head
  checks can execute and pass.

## .NET 10 LTS desktop migration (local, unreleased) — 2026-07-24

- Branch `feature/dotnet10-lts` starts from merged main
  `69c4c9e0fc84b7485ea019cf8f9bbfd466516896`.
- Implementation commit `03a5af014af0e26a49fca7462a02677ba825fb04` retargets the complete desktop
  source/test graph and lock files to .NET 10, pins SDK `10.0.302` in the repository plus CI/release
  workflows, and updates current build documentation.
- Direct requested/resolved NuGet versions are unchanged across all 19 regenerated lock files.
  Removed transitive entries are framework assemblies supplied by .NET 10.
- Locked Release verification passed vulnerable-package inspection, format, zero-warning/error
  build, and 343/343 desktop/shared tests.
- Shared consumers passed 44/44 mobile tests, Android arm64 Release AOT/linking/App Bundle
  production with zero warnings/errors, and the Windows-hosted iOS app/credential-extension
  reference build.
- Disposable self-contained/MSI/ZIP/Chocolatey/WinGet version `0.1.0-ci.910` passed startup,
  rollback-safe MSI schedule, shortcut/icon, browser-host/policy, and manifest validation. It was
  not published.
- Remaining: exact-head hosted CI, macOS iOS build, clean-machine installed lifecycle, trusted
  signing, and exact immutable public-candidate repetition.
- PR `#19` is open. Initial head `15e72f8eefa7ca5792b7519d16007a9449f8a6d0` received CI run
  `30085663231` and Mobile CI run `30085663253`; all five jobs had zero steps and the organization
  payment/spending-limit startup rejection. The PR is mergeable by Git but must remain unmerged
  until required exact-head checks execute and pass.

## G-09 legal and privacy source readiness — 2026-07-24

- Implementation commit `96300e9` generates a deterministic 236-record NuGet/npm component
  inventory and `THIRD-PARTY-NOTICES.md`, enforced by a CI drift/contract check.
- Windows packaging now fails on notice drift and embeds `LICENSE.txt`, `PRIVACY.md`, and
  `THIRD-PARTY-NOTICES.md`. Disposable MSI/ZIP `0.1.0-ci.920` contain all three.
- Local evidence passes: 25/25 source checks, 29/29 packaged checks, locked Release build with zero
  warnings/errors, and 343/343 desktop/shared tests.
- `AvaloniaUI.DiagnosticsSupport 2.2.3` is Release-excluded but has no declared NuGet license; it
  remains an explicit approval-required finding rather than an invented license.
- G-09 remains In progress. Exact signed-candidate SBOM/file and upstream-obligation review, an
  approved public privacy URL, Apple/Google declaration reconciliation, and named legal/privacy
  approval remain mandatory.
- GitHub Actions exact-head validation is externally blocked because organization jobs are
  rejected before execution by the account spending limit.

## G-06 machine-managed enterprise policy (PR #21, unmerged) — 2026-07-24

- Active worktree: `D:\tmp\vault-prospector-enterprise-policy`; branch:
  `feature/enterprise-policy`; implementation commit:
  `5d20399ce37370213fdf280a2b9ff97918fbf1ef`.
- Added versioned read-only HKLM policy plus packaged ADMX/ADML for allowed tenant GUIDs, providers,
  Azure identity types, clipboard disablement, offline-value disablement, and maximum offline-value
  lifetime. Invalid/unreadable enabled configuration fails closed; `Enabled=0` remains unmanaged
  without requiring the enabled-only `PolicyVersion` value.
- Application services enforce policy before governed sign-in/validation/network/value paths.
  Azure receives tenant constraints before enumeration and returned/local metadata is filtered
  again. Disable/revoke/purge/remove cleanup remains available.
- Settings displays a safe live status and managed controls cannot weaken the effective boundary.
  Workload candidate assessment enforces the target identity type before ARM access.
- Exact implementation-commit `scripts/Build.ps1` passed locked restore, package vulnerability
  scan, format, zero-warning/error Release build, and 368/368 tests.
- Source readiness passed 42/42; exact publish readiness passed 44/44. Local HKLM observation was
  readable with no policy key and made no registry changes. Full-history gitleaks scanned 123
  commits with no leaks.
- Disposable exact-source `0.1.0-ci.930` ZIP SHA-256:
  `CD033441AE37B5579DE96C3C0C396C55BD22256AF5517C8F4229EACE7F0B3834`; MSI SHA-256:
  `9177A473B88DFEDAD5EB0E6C0725A717557DC2F1B2149F0EDA1EB174D931671D`.
  MSI File-table inspection found both policy templates. Browser-host, shortcut/icon, and
  rollback-safe-upgrade guards passed.
- Evidence: `docs/release-evidence/enterprise-policy-2026-07-24.md`.
- G-06 remains in progress. Governed Group Policy/Intune deployment, live Azure/CyberArk
  allowed/denied matrices, diagnostics review, independent review, and exact trusted-signed
  candidate validation are still required.
- PR `#21` is open. Initial exact head
  `930dc361de8999b4320900af5e50f1c88a2e2c4d` started CI run `30088899332` and Mobile CI run
  `30088899350`. All five jobs completed as failures with zero steps and the same GitHub annotation
  that recent account payments failed or the spending limit must be increased. Evidence comment:
  `https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/21#issuecomment-5069245820`.
  This is an external infrastructure block, not a code-test result. Leave the PR unmerged until
  exact-head required checks execute and pass.

## Readiness integration candidate — 2026-07-24

- Branch `integration/readiness-candidate` combines PRs `#16`–`#21` on merged main
  `69c4c9e0fc84b7485ea019cf8f9bbfd466516896`.
- Integration commit `1185747307c6f0ca6b916abfbfc29cb16b125d4b` fixes the performance-probe
  .NET target/lock mismatch, updates the operational contract to .NET 10 LTS through 2028-11-14,
  regenerates the exact 225-record legal inventory, validates packaged legal files in CI, and
  prevents parallel TRX overwrite.
- Exact local evidence passes: locked restore/format, 0-warning/error Release build, 370/370
  desktop/shared tests, 44/44 managed mobile tests, Android arm64 Release production, Windows iOS
  reference compilation, six browser tests/build, vulnerability scan, and 141-commit gitleaks.
- Readiness checks pass: performance 8/8 at 50,000 objects, operations 35/35 with live endpoints,
  legal/privacy source 25/25 and package 29/29, enterprise policy source 42/42 and package 44/44.
- Disposable `0.1.0-ci.940` MSI SHA-256 is
  `1E67B6267CA7A69EC6A529A185A9364512D968714F3AC323B1002F538D6C97AB`; ZIP SHA-256 is
  `16B7168D6EC0E5D7E54DCC2D3B447F731B133CB067C626A28B50F90D96F77EFC`. Package smoke,
  rollback schedule, shortcut/icon, browser host, MSI legal/policy payload, Chocolatey, and WinGet
  validation pass.
- Evidence: `docs/release-evidence/readiness-integration-candidate-2026-07-24.md`.
- This remains local/disposable evidence. Hosted checks, trusted signing, clean-machine installed
  lifecycle, live services/devices, independent and human approvals, stores, exercises, and
  stability windows remain mandatory.
- PR `#22` is open. Initial evidence head
  `4427f48b8a8d90594f74bba5b8dde509cbc11dd9` started CI run `30099139189` and Mobile CI run
  `30099139278`. All five jobs had zero steps and the organization payment/spending-limit
  startup rejection. This is not a code result; leave the PR unmerged until required exact-head
  jobs execute and pass.

## HCS Azure DevOps delivery migration — 2026-07-24

- HCS MCP confirmed the solution must use Azure DevOps for CI/CD rather than mixed GitHub Actions
  and ADO workflows.
- Private ADO project `Vault Prospector` now contains CI definition `5`, scheduled operational
  readiness definition `6`, and release definition `7`.
- GitHub App connection `Hybrid-Solutions-Cloud GitHub`, Azure connection `HCS Platform Azure`,
  and Key Vault-linked variable group `vp-prd-secrets` are authorized to their required pipelines.
- Platform governance registration PR `#7` merged as
  `fd1cb41f5d6118d6b4013537282263802b49e472`.
- Release package key `hcs-vault-prospector-release-signing-key` exists in `kv-hcs-vault-01`;
  a real Cosign sign/verify smoke test passed against the committed public key.
- GitHub Actions workflow definitions were removed. The private source repository remains private;
  the existing public `vault-prospector-releases` repository is binary distribution only.
- Hosted validation corrected two environment-specific defects without weakening gates:
  WinGet is copied from the verified AppX install into agent temp before manifest validation, and
  both iOS projects now lock `iossimulator-x64` assets for Intel macOS agents.
- Exact PR validation ADO build `281` passed Windows, secret scan, native iOS, and Android jobs against
  PR merge commit `c39270f62537f34c1094213b76a20a93e74e1598`. Windows passed all 370 tests and
  package/readiness checks; mobile passed 44 tests, Android Release packaging, and native iOS
  application/extension compilation on pinned Xcode 26.0.1.
- ADO work item `AB#5095` owns the migration and Preview delivery.
- Next: validate the evidence-only head, merge PR `#22`, validate the exact `main` merge commit,
  run the immutable Preview release pipeline, verify public artifacts and package submissions,
  then remove the temporary HCS Windows fallback.

## Preview.5 laptop walkthrough backlog — 2026-07-25

- Active walkthrough worktree:
  `D:\tmp\vault-prospector-laptop-walkthrough`, branch
  `docs/laptop-install-walkthrough`, based on remote main
  `92a65535bcb3556a26d4ee084ffe35007daeb8cc`.
- Running product-owner observations and answers are in
  `.ai/state/LAPTOP_INSTALL_WALKTHROUGH.md`.
- The walkthrough synchronized two vaults and 124 objects with three isolated errors. It also
  reproduced an application-wide stuck busy state after connecting two interactive identities,
  confirmed that Cancel clears it for both identities, identified that the shipped UI remains the
  legacy design, and confirmed that AVD/Remote Desktop sessions have no usable unlock path.
- GitHub master Bugs were created and mirrored into ADO according to the HCS standards:
  `#39` / `AB#5572` busy state, `#40` / `AB#5573` isolated sync errors,
  `#41` / `AB#5574` UI divergence, and `#42` / `AB#5575` remote-session unlock.
  Each GitHub item is native type Bug, labeled `ado-tracked`, and comments link its ADO mirror.
- ADO-master User Stories were created:
  `AB#5569` trusted in-app updates, `AB#5570` privacy-safe diagnostics and support bundles,
  and `AB#5571` approved installation/setup/desktop experience.
- Thirty child Tasks, `AB#5576` through `AB#5605`, cover implementation, design approval,
  contextual help, security analysis, documentation, automated tests, and exact-package
  validation.
- A REST verification of all 37 new ADO items found zero errors: all are New in the root
  iteration, use approved tags, have exactly one parent, and meet type-specific fields and
  Acceptance Criteria requirements. Bugs also have repro steps, severity, GitHub hyperlinks, and
  Related links.
- Reinstall behavior verified from the installer and code: installed binaries are per-machine,
  but state remains under `%LOCALAPPDATA%\VaultProspector`; a same-account same/newer-version
  reinstall is intended to retain it. DPAPI prevents supported cross-account/device restoration,
  and the MSI blocks downgrade.
- No local build, test, packaging, or deployment ran during this backlog-authoring work.
- A second product-owner walkthrough batch added:
  - ADO Story `AB#5608` for a short policy-controlled verification grace period across consecutive
    explicit reveals. It is deliberately not plaintext or offline-value caching and invalidates on
    every security boundary.
  - Existing search Story `AB#5312` was refined to require discovered tenant, subscription, and
    vault selectors; Task `AB#5628` implements the selectors.
  - GitHub Bug `#43` / ADO mirror `AB#5610` for tenant-wide service-principal discovery including
    Microsoft first-party infrastructure principals.
  - GitHub Bug `#44` / ADO mirror `AB#5611` for minimize not hiding the window/taskbar entry in the
    notification area.
  - ADO Story `AB#5609` for a guided browser workflow that captures destination context
    automatically while retaining origin, policy, presence, confirmation, and verification checks.
- Tasks `AB#5612` through `AB#5628` decompose the second batch. All 22 inspected records—the 21 new
  items plus refined `AB#5312`—passed standards verification with zero errors. GitHub Bugs `#43`
  and `#44` are native Bug issues, labeled `ado-tracked`, and link their ADO mirrors.

## Complete UI redesign prototype — 2026-07-25

- A complete review artifact now exists at
  `docs/design/vault-prospector-ui-redesign-2026-07-25/bundle.html`.
- It provides three materially different directions—Compass, Command Center, and Atlas—across 11
  lifecycle screens from install and first-run setup through search, reveal, browser fill,
  administration, diagnostics, updates, and background behavior.
- It incorporates all Preview.5 walkthrough findings and has dedicated screenshots for install,
  setup, and daily search in each direction.
- Vite build and single-file bundling pass. Automated browser traversal verified all 33
  direction/screen states with zero console or page errors.
- This is intentionally isolated design work, not a build or deployment of the production desktop
  application. The next step is product-owner selection under AB#5571/AB#5587, followed by a
  production Avalonia handoff and implementation plan.

## Production desktop redesign implementation — 2026-07-25

- The temporary walkthrough worktree was moved into the durable repository area and converted to
  branch `feature/desktop-ui-redesign` at
  `D:\git\hybrid-solutions-cloud\vault-prospector-desktop-ui`.
- Design PR `#45` passed portable validation and full-history secret scanning and was merged to
  `main` as `613cb37`. The production code is intentionally proceeding in a separate PR.
- Product direction: Compass is the working production baseline with Atlas's persistent
  workspace/source context. Command Center is not a competing runtime shell.
- Added `docs/design/desktop-ui-production-handoff-2026-07-25.md` with tokens, shell behavior,
  responsive rules, interaction states, accessibility requirements, and delivery slices.
- First production slice is in progress:
  - shared Avalonia design tokens and Compass/Atlas shell;
  - left navigation that moves to the top in the existing narrow-layout mode;
  - persistent workspace, identity, indexed-object, safety, progress, and cancellation context;
  - discovered tenant, subscription, and vault search selectors;
  - markup regression ensuring the three source filters remain populated selectors.
- The operator workstation has .NET SDK 9 only while the repository pins 10.0.302, and the
  documented Ubuntu 22.04 WSL distribution is absent. No production build was run on the operator
  workstation. Validation must use the HCS self-hosted runner according to the current
  build-environments standard.
- HCS bootstrap resolves `vault-prospector` as an HCS `app` profile and applies scripting, testing,
  documentation, governance, build-environments, and project-management standards. Its drift
  endpoint still returns `Path not found` for the registered Windows checkout; no drift pass is
  claimed.
