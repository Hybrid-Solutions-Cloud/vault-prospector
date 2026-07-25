# Atlas Windows candidate evidence — 2026-07-25

> **Release-stopping result:** the exact installed candidate passed its security, installer,
> updater, diagnostics, and notification-area checks, but failed product-owner visual review.
> The production desktop retained a legacy-derived content layout with Atlas colors and shell
> chrome instead of implementing the approved C · Atlas screens. Release run
> `30178225455` was cancelled before publication. This record must not be used as evidence of
> Atlas desktop parity.

## Candidate identity

- Pull request:
  [#46](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/46).
- Source commit: `01c2b820c01b64a4ddb2d83d917ea385c7d3a74a`.
- Candidate version: `0.3.0-ci.190`.
- MSI SHA-256:
  `506B43A01D91A0C6437D60B04852EDD00031723C73DD76675B410131AEC80A8B`.
- Public release status: not released; this record covers an exact CI candidate.

The product owner selected Atlas as the production desktop direction. This candidate implements
the Atlas installer and a partial Atlas-themed desktop shell, trusted in-app update workflow, privacy-safe
diagnostics, Remote Desktop verification fallback, reveal-verification grace policy, discovered
source selectors, guided browser setup, service-principal candidate filtering, and
notification-area lifecycle.

## Authoritative HCS validation

GitHub Actions
[run 30175377767](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30175377767)
passed all three jobs on the exact source commit:

- the HCS Linux runner passed locked restore, formatting, the zero-warning portable build,
  platform-neutral tests, browser-extension and design-prototype checks, dependency inspection,
  PowerShell syntax, and operational-readiness validation;
- the HCS Linux runner passed the pinned full-history Gitleaks scan; and
- the ephemeral HCS Azure Windows runner passed the zero-warning build and all 432 Windows tests,
  performance/scale limits, MSI/MSIX and package-manager builds, installer and browser-host
  contracts, legal/privacy/enterprise/operational checks, and the complete installer lifecycle.

The automated lifecycle passed 27 checks covering clean install, previous-version install,
deliberately failed upgrade and rollback, successful upgrade, forced repair, downgrade rejection,
uninstall, and retained `%LOCALAPPDATA%` state. No authoritative build, test, package, or
publication step ran on the operator workstation.

## Exact MSI walkthrough

The exact MSI was hash-verified on the host and again inside a disposable Windows 11 Enterprise
25H2 Generation 2 Hyper-V guest with Secure Boot and vTPM enabled.

| Check | Result |
| --- | --- |
| Atlas welcome, license, destination, ready, progress, and completion pages | Passed |
| Keyboard acceptance of the MIT license | Passed |
| Unsigned direct-download disclosure and Windows **Unknown Publisher** boundary | Passed |
| Installed Apps version | Passed: `0.3.190` |
| Installed executable version and location | Passed: `0.3.0.0` at `C:\Program Files\Vault Prospector\VaultProspector.App.exe` |
| Local console without an available verification device | Passed fail-closed with actionable status |
| Actual RDP session detection | Passed |
| Current-account Windows credential fallback in RDP | Passed; the application unlocked only after successful Windows verification |
| Functional shell after remote verification | Passed; the application reported **READY** |
| Visual parity with approved C · Atlas production screens | **Failed**; content remained legacy-derived |
| Activity and Support page | Passed |
| Settings update check against the public binary-only release repository | Passed |
| Minimize to notification area | Passed; the taskbar entry disappeared while the process and Vault Prospector notification icon remained active |
| Restore from notification icon | Passed; one window returned and required verification again |

The RDP verification test used only a disposable local Windows account. No tenant credential,
secret value, PIN, access token, or customer identifier was retained in this evidence.

## Diagnostics and updater

The installed application created a local support bundle at the user-selected support location.
Its SHA-256 was
`2B590E49BB18C4BBA74C936C69295D150C857D300E217BD7547495CD7433411D`.
The ZIP contained only `manifest.json` because the fresh guest had no diagnostic events. The
manifest stated that secret values, access tokens, usernames, vault names, and object names were
excluded and that automatic upload was disabled.

The installed updater:

- displayed installed version `0.3.0-ci.190`;
- queried the authenticated public binary-release repository;
- identified `0.2.0-preview.5` as the latest public release;
- loaded its release notes; and
- correctly disabled download and launch because the CI candidate was newer than the release
  channel.

## Acceptance disposition

This candidate remains valid evidence for the RDP verification and notification-area portions of
AB#5575 and AB#5611. It is not sufficient evidence for the Atlas implementation or visual-parity
portions of AB#5571 and AB#5574. Tasks AB#5589, AB#5591, AB#5592, AB#5600, and AB#5601 were
reopened. The next corrected package must use a new immutable version and pass a direct rendered
comparison against the approved C · Atlas screens before any UI item is closed.

The following separate live matrices are not claimed by this record and remain open:

- two real Microsoft Entra identities and multi-identity busy-state recovery;
- live partial Azure synchronization with isolated failures and retry/export;
- consecutive live secret reveals;
- installed Chrome, Edge, and Firefox fill;
- tenant-scale service-principal discovery; and
- populated search selectors sourced from live discovery.
