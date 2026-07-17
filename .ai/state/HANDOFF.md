# Session handoff

## Current state

- Branch: `main`; direct pushes are the operator-approved workflow.
- Public release: `v0.1.0-preview.2` is **withdrawn** and retained only for immutable evidence and
  existing-install repair/uninstall. Do not install, resubmit, or reuse its artifacts.
- Authoritative gate matrix: `docs/product/release-readiness.md`.
- Decision: not ready for public Preview promotion or General Availability.
- Latest pushed validation: withdrawal record commit `709ff3997e73c886168750034d6c8a7f963e9b3b`;
  exact-commit workflow run `29590038115` passed. There is no publishable release candidate.
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

## External publication state

- WinGet PR `microsoft/winget-pkgs#403473` is closed with a withdrawal notice. Microsoft validation
  had passed, but the later failed-upgrade result invalidated the submitted Preview.2 MSI. Submit a
  new PR only for a new signed immutable corrected version.
- Chocolatey never ingested `0.1.0-preview.2` after six HTTP 504 responses. That version is now
  withdrawn and must not be retried. Submit a new signed rollback-safe version only after the upload
  path recovers, then verify ingestion and moderation.
- Trusted Windows signing gate P-13 remains blocked until an HCS owner completes Azure Artifact
  Signing Public Trust identity and profile setup. The release workflow fails closed without it.
- HCS drift cannot currently validate this local checkout because the MCP server cannot resolve the
  unregistered repository/path; do not report a drift pass.

## Next actions

1. Continue Preview-critical gates: arrange independent security review issue `#9`; run live
   identity, MFA, Conditional Access, and Windows Hello tests; complete keyboard, NVDA, Narrator,
   and usability evidence; finish signing setup, publish a new immutable corrected version, obtain
   replacement WinGet/Chocolatey acceptance, and complete the final signed-candidate go/no-go.

## Preserved external scratch

Do not delete `D:\tmp\vault-prospector-untracked-AzureIdentityAndVaultProvider.cs`. It is a
quarantined pre-existing untracked source file with SHA-256
`69AC58A44284A1D5B3947F81783288BE19B64C41ECECAC7538C874829849BBDC`; it is intentionally outside
the repository and must not be committed.
