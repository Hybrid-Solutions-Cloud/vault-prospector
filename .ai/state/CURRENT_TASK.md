# Current task

Complete the remaining Windows General Availability (GA) evidence without treating future
products, paid signing, or an unavailable preferred build host as release blockers.

Confirmed on 2026-07-25:

- CyberArk and native mobile applications are future-roadmap work, not Windows GA blockers.
- GitHub Actions owns CI/release execution; Azure DevOps remains the governed hierarchy for epics,
  features, user stories, and tasks.
- Portable validation uses the HCS Linux runner. Windows-only validation and packaging use the
  one-shot HCS Tier-4 ephemeral Azure VM.
- The operator workstation does not perform authoritative builds, tests, packaging, or
  publication. It may orchestrate workflows and host isolated Hyper-V acceptance-test guests.
- The free trusted Windows path is Microsoft Store-signed MSIX. Direct MSI/ZIP downloads remain
  explicitly unsigned with checksums, SBOM, and Sigstore evidence.

Validated:

- PR #33 corrected the unpackaged desktop verification path and merged as
  `e84d0f0e47605d9575a3306721adf3b50764c4d2`.
- The exact public Preview.4 MSI passed Windows Hello success, cancellation, button re-entry, and
  button-initiated success in a dedicated Windows 11 Hyper-V basic-console session, completing
  AB#5539.
- That clean first-run test found a separate null Identity Type binding defect. PR #36 corrected
  it by preserving and incrementally synchronizing the bound collection, with regression coverage
  that rejects a collection reset.
- PR #36 merged as `542be4679006c2a34ef1df3b58722ae8a844b1ae`. Exact-main run
  `30162673459` passed all three jobs on HCS-managed runners, including the zero-warning 375-test
  Windows build and packaging/readiness gates.
- Immutable tag `v0.2.0-preview.5` points to that exact merge commit. Release run `30163007720`
  repeated the zero-warning Windows build and all 375 tests, created packages/SBOM/Sigstore
  evidence, and published through the HCS GitHub App.
- The public `0.2.0-preview.5` prerelease has exactly 16 assets. Independent downloads matched all
  five package checksum files; Cosign `v3.0.6` verified all five bundles against the exact
  tag-workflow identity.
- The exact public Preview.5 MSI passed a fresh Windows 11 first-run test: Windows Hello unlocked
  the application, `InteractiveUser` was selected, and no null conversion error appeared. The
  retained evidence SHA-256 is
  `FDB6AA4A12C3EC683BBDBDC11EADC56DB5A9AFE175EA2187A5DA79A795A6D35E` and is attached to AB#5542.
- AB#5542 is Closed. Parent AB#5348 remains open because its separate live Microsoft Entra,
  keyboard/screen-reader, independent-review, and exact-release Acceptance Criteria are not all
  complete.
- The public Preview.4 record is marked withdrawn and points to Preview.5; immutable historical
  tags and assets were not changed.
- Release-record PR #37 merged as `29a957af86c022a8479ee46f39fab94d0f2377bb`.
  Exact-main run `30164830620` passed all three HCS jobs.
- The one-shot Azure Windows runner resource group was deleted, zero Windows runners remain, and
  its temporary Key Vault credentials were soft-deleted.
- The disposable Hyper-V guest is powered off at its clean baseline with zero snapshots.
  Disposable credential/PIN files and transient captures were deleted.

Next:

1. Complete current-Windows live identity/provider matrices, independent security/accessibility
   review, enterprise-policy deployment, operational exercise, and legal/privacy approval.
2. Reserve the free Partner Center identity, submit the reproducible MSIX, and validate the
   Microsoft-signed Store package.
3. Implement governed Azure mutations only after the required design/security gate; the current
   product remains intentionally read-only.
4. Keep CyberArk and mobile in their separate future-roadmap releases.
