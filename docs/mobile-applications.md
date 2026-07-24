# Mobile applications

**Status:** Source implementation and CI validation in progress; not released

**Platforms:** Android 12/API 31 or later and iOS/iPadOS 18 or later

Vault Prospector has separate Android and iOS hosts around a shared Avalonia 12 user experience.
The mobile applications reuse the domain, application, encrypted metadata, and Azure provider
layers without importing Windows-only code. They communicate directly with Microsoft Entra,
Azure Resource Manager, and Azure Key Vault; no Vault Prospector relay stores tokens or values.

## Implemented user path

1. Unlock the application with the device's native user-verification prompt.
2. Connect a Microsoft Entra user identity through MSAL and the system browser.
3. Synchronize the selected identity's accessible vault metadata.
4. Search the encrypted local metadata index.
5. Explicitly reveal a value for 15 seconds or copy it for 30 seconds after fresh device
   verification.
6. Lock immediately when the application leaves the foreground, cancel in-flight value work, clear
   revealed state, and clear clipboard content still owned by Vault Prospector.

Offline value caching is disabled by the mobile secure-default policy.

## Platform security

Android uses an authentication-bound AES-GCM key in Android Keystore, BiometricPrompt with strong
biometric or device credential, `FLAG_SECURE`, sensitive clipboard labeling, private app storage,
and explicit backup/data-transfer exclusions.
Touches reported as obscured or partially obscured are rejected before reaching the shared UI.

iOS uses a device-only Keychain item protected by the current biometric enrollment,
LocalAuthentication, local-only expiring pasteboard items, backup exclusion, background
snapshot covering, and capture-state observation. iOS does not promise to prevent a screenshot
that the operating system has already taken. While screen capture is active, the app remains
covered and refuses to unlock. Protected-data loss locks the session, and a clean reinstall
removes stale install-bound Keychain material before creating new local state.

An iOS device without enrolled biometrics is unsupported and fails closed. Passcode fallback does
not unlock the application key; enrollment changes invalidate access and require local reset,
Microsoft Entra reauthentication, and metadata resynchronization.

See the [mobile threat model](security/mobile-threat-model.md) and
[native-host ADR](adr/0016-native-mobile-security-hosts.md).

## Microsoft Entra registration

The public client application ID is `221af888-1c16-4637-9d45-b6dd2e1e7634`. Before a mobile build
can authenticate, its Microsoft Entra app registration must include this custom public-client
redirect URI:

```text
msal221af888-1c16-4637-9d45-b6dd2e1e7634://auth
```

The Android manifest and iOS URL scheme are already restricted to that exact callback. This
baseline uses the system browser. Android broker enablement additionally requires the final
package/signature-bound `msauth` callback and must be validated only after the protected signing
identity exists. Do not put client secrets in either application; these are public clients.

The production registration was updated and re-read successfully on 2026-07-24 with both the
existing desktop loopback callback and this mobile callback. Live authentication remains a
separate device-validation gate.

## Build and test

Mobile builds use the SDK pinned in `mobile/global.json` and locked NuGet dependencies.

```powershell
./scripts/Build-Mobile.ps1 -Platform Managed
./scripts/Build-Mobile.ps1 -Platform Android
./scripts/Build-Mobile.ps1 -Platform iOS
```

Android requires the .NET Android workload, API 36 SDK, and JDK 21. Optional
`-AndroidSdkDirectory` and `-JavaSdkDirectory` parameters select non-default installations. A
signed release needs a protected Play upload keystore supplied outside the repository.

iOS compilation and simulator builds require the .NET iOS workload on a supported macOS/Xcode
host. App Store archives require an Apple Distribution identity, provisioning profile, App Store
Connect application, and protected signing material supplied outside the repository.

The mobile CI workflow runs shared tests on Linux, builds an Android App Bundle on Linux, and
builds an unsigned iOS simulator application on a macOS 26 runner. CI compilation is not a
substitute for signed-device, TestFlight, closed-test, accessibility, or store-review evidence.

## Release boundary

Neither mobile application is released. The following remain mandatory:

- live mobile multi-account/tenant authentication and Android broker callback validation;
- physical-device lifecycle, secure-storage invalidation, backup, migration, reinstall,
  screenshot/recording, clipboard, and accessibility matrices;
- protected Android and Apple signing identities and reproducible signed artifacts;
- App Store privacy and Google Play data-safety review;
- TestFlight and Play closed testing;
- independent security approval and successful store review.

Current evidence and owners are tracked in
[Phase 13 mobile evidence](release-evidence/mobile-phase-13-2026-07-24.md).
