# Phase 13 mobile implementation evidence

**Date:** 2026-07-24

**Branch:** `feature/mobile-platforms`

**Release status:** Not released; signed-device and store gates open

## Implemented

- Shared .NET 10/Avalonia mobile shell for unlock, identity connection, synchronization, encrypted
  metadata search, explicit reveal, and bounded clipboard copy.
- Fail-closed session coordinator that cancels sensitive work and clears transient UI on lock or
  background.
- Android native host with authentication-bound Android Keystore protection, BiometricPrompt,
  secure-window flags, sensitive clipboard behavior, and backup/data-transfer exclusions.
- iOS native host with current-biometric-set, device-only Keychain access control,
  LocalAuthentication, background
  covering, active-capture lockout, local-only pasteboard expiration, and backup exclusion.
- Platform-specific MSAL callback and native parent-window handling.
- Exact-origin mobile autofill eligibility policy. Native credential-provider extensions remain a
  separate validation gate and no arbitrary vault value is offered.
- Mobile threat model, architecture decision, build automation, privacy manifest, and CI workflow.

## Automated evidence

| Check | Result |
| --- | --- |
| Shared mobile unit and view-model tests | 19 passed, 0 failed |
| Existing Windows/shared regression suite | 342 passed, 0 failed |
| Android managed/native compile on Windows | Passed |
| iOS managed/native compile against .NET iOS reference pack | Passed |
| Android Release App Bundle package build | Passed with 0 warnings/errors; exact-commit CI rerun required |
| iOS simulator build on macOS/Xcode | Pending exact-commit CI |

The local mobile toolchains were installed under `D:/tmp` and did not modify a governed system
toolchain. Local Android output used development signing and is not a release candidate.

The production Microsoft Entra public-client registration was updated on 2026-07-24 to preserve
`http://localhost` and add the exact `msal221af888-1c16-4637-9d45-b6dd2e1e7634://auth` callback.
A post-update read returned both callbacks. No application credential was added.

## Store declarations baseline

Vault Prospector does not track users, sell data, serve advertising, or send product telemetry.
It stores encrypted metadata and optional application diagnostics locally. User-directed
authentication and vault operations communicate directly with Microsoft and the selected provider.

The iOS privacy manifest declares no tracking and no application-collected data. The final App
Store declaration must be reconciled against the exact transitive SDK manifests and observed
network behavior.

The Android data-safety baseline is no developer collection or sharing. Authentication and
user-directed vault content are processed ephemerally by the app and provider. The final Play
declaration must be reconciled against the exact signed bundle, SDK disclosures, and live traffic.

## Open gates

- Live mobile system-browser, account, tenant, guest, MFA, Conditional Access, cancellation,
  expiry, and removal matrix; Android broker callback remains signing-bound.
- CI green for the exact pull-request head on managed tests, Android package, and iOS simulator.
- Signed Android App Bundle verified with the protected Play upload key.
- Signed iOS archive verified with the protected Apple Distribution identity and provisioning.
- Governed physical-device matrix for security, lifecycle, backup/migration/reinstall, identity,
  accessibility, offline behavior, and representative-user tasks.
- Native Apple Password AutoFill and Android Autofill/Credential Manager eligibility prototypes and
  negative-origin testing.
- Independent mobile threat-model review with all critical/high findings closed.
- TestFlight, Play closed-test, privacy/data-safety approval, and store acceptance.

No open gate may be represented as passed by source review, simulator use, unsigned output, or
unit tests.
