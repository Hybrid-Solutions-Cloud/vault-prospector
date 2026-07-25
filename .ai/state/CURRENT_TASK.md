# Current task

Complete the remaining Windows GA evidence without treating future products, paid signing,
or an unavailable preferred build host as release blockers.

Confirmed on 2026-07-25:

- CyberArk and native mobile applications are future-roadmap work, not Windows GA blockers.
- The repo-specific Azure Container Apps GitHub runner is healthy with labels
  `self-hosted,linux,ubuntu-22.04,hcs`.
- Windows-only packaging uses the HCS Tier-4 ephemeral Azure VM with labels
  `self-hosted,windows,hcs,vault-prospector`.
- GitHub Actions owns CI/release execution; Azure DevOps remains the governed hierarchy for epics,
  features, user stories, and tasks.
- The free trusted Windows path is Microsoft Store–signed MSIX. Direct MSI/ZIP downloads remain
  explicitly unsigned with checksums, SBOM, and Sigstore evidence.
- ADO builds 284, 287, 290, and 295 and the 27/27 clean-Windows run remain historical evidence; they
  do not justify continuing ADO pipelines.
- Installed `0.2.0-preview.3` exposed a Windows desktop verification defect: it used the UWP-only
  request API. AB#5539 tracks the HWND-bound correction, explicit RDP diagnosis, HCS-runner
  validation, replacement Preview, and local-console Windows Hello evidence.

Validated:

- `0.2.0-preview.3` is published from immutable source
  `f0ff8e7fc6190953620b4cf7d8aae4447875dfe2` in the public binary repository. Release run
  `30150472368` attempt 2 passed, all 16 assets are present, all five adjacent hashes matched
  independent downloads, and all five keyless Sigstore bundles verified.
- PR #29 corrected the performance metric without changing its two-second limit. PR #30 repaired
  the one-shot Windows runner's Git Bash path. Exact-main runs `30149406966` and `30150138832`
  passed all three jobs.
- Failed immutable tag `v0.2.0-preview.2` has no public release or assets and was not moved or
  reused.
- PRs #26 and #27 merged as `c6748ccc87ad62fb9c6f3ac46c067360972acce4` and
  `a0370c3163e4389ac5fbf61b81f2921051533546`.
- PR runs `30146345649` and `30146846301` passed portable validation and full-history secret
  scanning on the HCS Tier-2 runner.
- Exact-main runs `30146470563` and `30146971143` passed all three jobs, including the Windows
  candidate on the governed Tier-4 fallback.
- HCS Windows CI passed 371 tests plus MSI, MSIX, performance, legal/privacy, enterprise, browser,
  and operational gates for the prior release.
- The Tier-4 deployment succeeded twice, the final VM was stopped, and
  `rg-hcs-vp-winbuild-eus2-01` was deleted.
- ADO pipeline definitions 5, 6, and 7 were retired after replacement validation; historical
  build records remain.
- All 137 ADO work items were audited. No item lacks formal acceptance criteria, tags, or priority,
  and no closed parent has an open child or New parent has only terminal children.
- AB#5095 and AB#5332 are closed with evidence. CyberArk, mobile, and Store signing are Priority 4
  `future-roadmap` work. Every other open User Story has a recorded acceptance-evidence gap.

Next:

1. Complete AB#5539 on HCS runners and replace `0.2.0-preview.3`; record local-console Windows Hello
   success and cancellation before closing the Task.
2. Complete current-Windows live matrices, independent security/accessibility review, enterprise
   policy deployment, operational exercise, and legal/privacy approval.
3. Reserve the free Partner Center identity, submit the reproducible MSIX, and validate the
   Microsoft-signed Store package.
4. Implement governed Azure mutations only after the required design/security gate; the current
   product remains intentionally read-only.
5. Keep CyberArk and mobile in their separate future-roadmap releases.
