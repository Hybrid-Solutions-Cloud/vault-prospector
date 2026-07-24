# Phase 13 mobile implementation evidence

**Date:** 2026-07-24

**Merged commit:** `ead0a29faa4802008ac4d7b0e9c1c10ad881d2df` (PR #13)

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
- Exact-origin mobile autofill eligibility policy, a package-disabled Android `AutofillService`,
  and an embedded least-privilege Apple credential-provider extension. Both native prototypes
  return no value until the remaining mapping, verification, device, and review gates pass.
- Mobile threat model, architecture decision, build automation, privacy manifest, and CI workflow.

## Automated evidence

| Check | Result |
| --- | --- |
| Shared mobile unit and view-model tests | 19 passed, 0 failed |
| Existing Windows/shared regression suite | 343 passed, 0 failed |
| Android managed/native compile on Windows | Passed |
| iOS managed/native compile against .NET iOS reference pack | Passed |
| Exact PR-head CI | Run `30076673071` passed build-test and secret-scan |
| Android Release App Bundle package build | Run `30076673064` passed |
| iOS simulator build on macOS 26/Xcode 26.0.1 | Run `30076673064` passed |
| Exact merge-commit CI | Run `30077519402` passed build-test and secret-scan |
| Exact merge-commit Mobile CI | Run `30077519354` passed managed-tests, Android package, and iOS simulator |
| Follow-on native-autofill local checks | 43 mobile tests, Android Release App Bundle, iOS app/extension reference-pack compile, locked restore, formatting, and vulnerability checks passed |

The local mobile toolchains were installed under `D:/tmp` and did not modify a governed system
toolchain. Local Android output used development signing and is not a release candidate.

The iOS linker reports grouped `IL2104` warnings from Avalonia DesignerSupport, Azure Core,
Azure Key Vault Certificates, and Microsoft.Data.Sqlite. The build keeps those package-internal
warnings visible but does not promote `IL2104` to an error. Vault Prospector's own reflection-based
JSON paths were replaced with source-generated metadata or direct structured writing, and
project-owned trim diagnostics remain build-breaking. Physical-device tests remain required before
release because a successful trimmed simulator build does not prove every upstream runtime path.

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
- Signed Android App Bundle verified with the protected Play upload key.
- Signed iOS archive verified with the protected Apple Distribution identity and provisioning.
- Governed physical-device matrix for security, lifecycle, backup/migration/reinstall, identity,
  accessibility, offline behavior, and representative-user tasks.
- Enabled Apple Password AutoFill and Android Autofill/Credential Manager on signed physical
  devices, including exact mapping exchange, Android package/domain/signature association,
  fresh-verification, positive fill, and negative-origin/lifecycle/accessibility tests.
- Independent mobile threat-model review with all critical/high findings closed.
- TestFlight, Play closed-test, privacy/data-safety approval, and store acceptance.

No open gate may be represented as passed by source review, simulator use, unsigned output, or
unit tests.
