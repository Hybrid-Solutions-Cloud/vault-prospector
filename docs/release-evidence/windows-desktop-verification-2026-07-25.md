# Windows desktop verification correction — 2026-07-25

## Finding

`0.2.0-preview.3` called `UserConsentVerifier.RequestVerificationAsync`, which Microsoft documents
as the UWP API. An unpackaged Windows desktop application must obtain its HWND and call
`UserConsentVerifierInterop.RequestVerificationForWindowAsync`.

The defect was found during an installed-MSI first-run attempt. The application stayed locked and
showed `Application locked — verification unavailable`. A read-only probe in the same active RDP
session returned:

```text
Availability: DeviceNotPresent (1)
Desktop verification: DeviceNotPresent (1)
```

The probe confirms a second, environment-specific result: Windows does not expose a verification
device to this RDP session. Repeating the request cannot produce a prompt in that session.

## Correction

- The production service now receives the real Avalonia window handle and uses the desktop interop
  API.
- A missing HWND fails closed without attempting verification.
- `DeviceNotPresent` in a detected remote session has a distinct application result and locked-screen
  explanation.
- The application does not bypass local verification or initialize encrypted metadata in the
  affected remote session.

## Diagnostic verification

- `VaultProspector.Platform.Tests`: 64/64 passed, including HWND-bound request, missing-HWND, and
  remote-session cases.
- `VaultProspector.App.Tests`: 89/89 passed, including the specific Remote Desktop recovery text.
- A locally launched diagnostic candidate rendered the new Remote Desktop explanation through
  Windows UI Automation.

These local checks are diagnostic only and are not release evidence. The authoritative build,
complete test suite, packaging, and publication must run on HCS-managed runners. Local-console
Windows Hello success and cancellation remain required before AB#5539 closes.

## Authoritative HCS verification and release

- [PR #33](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/33) merged the correction
  as source commit `e84d0f0e47605d9575a3306721adf3b50764c4d2`.
- Exact-main [run 30158989872](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30158989872)
  passed all three jobs on HCS-managed runners, including the zero-warning 375-test Windows build,
  packaging, installer/browser contracts, and readiness gates.
- Immutable-tag [run 30159321059](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30159321059)
  repeated the zero-warning Windows build and all 375 tests, produced five packages and their
  Sigstore bundles plus the SPDX SBOM, and published
  [`0.2.0-preview.4`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.2.0-preview.4)
  through the HCS GitHub App.
- Independent public verification matched all five package checksum files and verified all five
  Sigstore bundles against the exact tag-workflow identity.

The authoritative build, tests, packaging, and publication did not run on the operator
workstation. See the complete [`0.2.0-preview.4` release evidence](0.2.0-preview.4.md).

## Source

- [Microsoft UserConsentVerifier desktop application example](https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.ui.userconsentverifier)
