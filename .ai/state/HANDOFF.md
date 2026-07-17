# Session handoff

## Current state

- Branch: `main`; direct pushes are the operator-approved workflow.
- Public release: `v0.1.0-preview.2`.
- Authoritative gate matrix: `docs/product/release-readiness.md`.
- Decision: not ready for public Preview promotion or General Availability.
- Current exact CI validation candidate: `0.1.0-ci.52` from commit
  `50f6e2f9321b4441830aee953809d879f099e267`, workflow run `29563316747`.
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

## External publication state

- WinGet PR `microsoft/winget-pkgs#403473` remains open. Automated validation passed; the
  `Policy-Test-2.7` classification is awaiting manual moderator review.
- Chocolatey `0.1.0-preview.2` submission remains externally blocked after repeated HTTP 504
  responses. Retry only after evidence of push-service recovery, then verify ingestion and
  moderation.
- Trusted Windows signing gate P-13 remains blocked until an HCS owner completes Azure Artifact
  Signing Public Trust identity and profile setup. The release workflow fails closed without it.
- HCS drift cannot currently validate this local checkout because the MCP server cannot resolve the
  unregistered repository/path; do not report a drift pass.

## Next actions

1. Publish the NVDA/focus implementation, tests, evidence, and synchronized readiness documents to
   `main`; record exact-commit CI and synchronize GitHub issues `#5` and `#8`.
2. Continue Preview-critical gates: independent security review, live identity/MFA/Conditional
   Access and Windows Hello tests, full keyboard/NVDA/Narrator/usability evidence, signing setup,
   WinGet acceptance, Chocolatey ingestion, and the final signed-candidate go/no-go.

## Preserved external scratch

Do not delete `D:\tmp\vault-prospector-untracked-AzureIdentityAndVaultProvider.cs`. It is a
quarantined pre-existing untracked source file with SHA-256
`69AC58A44284A1D5B3947F81783288BE19B64C41ECECAC7538C874829849BBDC`; it is intentionally outside
the repository and must not be committed.
