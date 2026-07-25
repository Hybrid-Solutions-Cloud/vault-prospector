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

## Verification

- `VaultProspector.Platform.Tests`: 64/64 passed, including HWND-bound request, missing-HWND, and
  remote-session cases.
- `VaultProspector.App.Tests`: 89/89 passed, including the specific Remote Desktop recovery text.
- A locally launched diagnostic candidate rendered the new Remote Desktop explanation through
  Windows UI Automation.

These local checks are diagnostic only and are not release evidence. The authoritative build,
complete test suite, packaging, and publication must run on HCS-managed runners. Local-console
Windows Hello success and cancellation remain required before AB#5539 closes.

## Source

- [Microsoft UserConsentVerifier desktop application example](https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.ui.userconsentverifier)
