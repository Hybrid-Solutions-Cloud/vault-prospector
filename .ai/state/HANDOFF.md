# Session handoff

## Current state

- Branch: `main`; direct pushes are the operator-approved workflow.
- Public release: `v0.1.0-preview.2` is **withdrawn** and retained only for immutable evidence and
  existing-install repair/uninstall. Do not install, resubmit, or reuse its artifacts.
- Public test release: unsigned `v0.1.0-ci.68` remains available until formal Preview publication.
- Promotion target: unsigned non-production `v0.1.1-preview.1`; release automation, release notes,
  roadmap, backlog, scope, readiness, install/package/security/privacy/runbook/evidence documents,
  and the named go/no-go record are being synchronized for publication.
- Authoritative gate matrix: `docs/product/release-readiness.md`.
- Decision: Preview promotion pending exact-commit CI, tagged workflow artifacts, public mirroring,
  and anonymous hash verification. GA remains blocked by the documented open gates.
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

## External publication state

- Public unsigned test prerelease `v0.1.0-ci.68` contains the exact green-CI MSI, checksum, and
  provenance at `Hybrid-Solutions-Cloud/vault-prospector-releases`; anonymous download validation
  passed. This unblocks immediate testing without changing the signing or formal promotion gates.
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

## Next actions

1. Commit/push the synchronized `0.1.1-preview.1` source and documentation; require exact-commit CI.
2. Push immutable tag `v0.1.1-preview.1`, verify the protected unsigned-Preview workflow, mirror the
   exact full artifact set to the public release repository, anonymously re-download/hash it, and
   finalize P-12/P-18 evidence plus issue `#5`.
3. After direct Preview publication, submit the exact immutable MSI to WinGet and Chocolatey.
4. Continue GA work: signing, independent security review issue `#9`, live Entra/MFA/Conditional
   Access/Windows Hello testing, remaining accessibility/usability evidence, and G-01 feedback.

## Preserved external scratch

Do not delete `D:\tmp\vault-prospector-untracked-AzureIdentityAndVaultProvider.cs`. It is a
quarantined pre-existing untracked source file with SHA-256
`69AC58A44284A1D5B3947F81783288BE19B64C41ECECAC7538C874829849BBDC`; it is intentionally outside
the repository and must not be committed.
