# ADR: Shared Avalonia mobile shell with native security hosts

- **Status:** Accepted
- **Date:** 2026-07-24

## Context

Phase 13 requires production iOS and Android clients without weakening the local-first,
provider-direct, user-presence, or encrypted-storage boundaries already established for Windows.
Avalonia 12 supports mobile only on .NET 10. The existing domain, application, infrastructure, and
Azure provider assemblies are portable, but the desktop application, DPAPI, Windows Hello,
clipboard, lifecycle, and desktop MSAL cache are not.

iOS and Android also differ materially:

- Android can block ordinary screenshots and non-secure displays with `FLAG_SECURE`; iOS cannot
  guarantee screenshot prevention and must instead obscure app-switcher snapshots and react to
  capture state.
- Apple Keychain/LocalAuthentication and Android Keystore/BiometricPrompt have different key,
  migration, enrollment-change, and recovery semantics.
- Store signing, privacy declarations, backup rules, lifecycle testing, and credential-provider
  extensions are platform-specific release gates.

## Decision

Build Phase 13 as a separate `mobile/` .NET 10 solution:

1. A platform-neutral core owns session locking, cancellation, search/retrieval orchestration,
   redacted state, and mobile autofill eligibility policy.
2. One Avalonia 12 single-view project owns the shared touch UI and accessibility semantics.
3. Separate .NET for Android and .NET for iOS hosts own authentication callbacks, secure key
   storage, local verification, clipboard, screen privacy, backup exclusion, and lifecycle events.
4. The mobile hosts reuse the existing domain, application, SQLCipher infrastructure, and Azure
   provider contracts. They do not reference the Windows platform or desktop application projects.
5. Mobile MSAL uses the platform mobile cache and a native parent activity/view controller.
   Desktop `Microsoft.Identity.Client.Extensions.Msal` storage is not reused.
6. Metadata and any explicitly cached values remain local. No project-hosted backend is introduced.

Android targets API 36 with a minimum supported API of 28. The Android host must use an
authentication-bound Android Keystore key, BiometricPrompt/device credential, `FLAG_SECURE`,
explicit backup/data-transfer exclusions, sensitive clipboard labeling, foreground-only value
operations, and lock/cancel on background.

iOS targets iOS 18 or later. The iOS host must use device-bound Keychain protection,
LocalAuthentication, protected-data availability, app-switcher privacy covering, capture-state
notification, backup exclusion, foreground-only value operations, and lock/cancel on background.
Because iOS screenshot notification occurs after capture, product copy and release evidence must
not claim screenshot prevention.

Password AutoFill/credential-provider work is a separate native extension boundary. A value may be
offered only for a saved, exact HTTPS origin and explicit credential field purpose after user
presence. Arbitrary Azure values, keys, certificates, wildcard origins, background retrieval, and
silent fill are prohibited.

Signing, TestFlight, App Store, closed testing, Play review, privacy/data-safety declarations,
accessibility, real-device security, and independent review remain separate per-platform gates.
Passing one platform never implies the other is releasable.

## Consequences

- The Windows SDK and release pipeline stay stable while mobile uses the supported .NET 10
  toolchain.
- Business and provider behavior remains shared and testable, while native security behavior stays
  visible rather than hidden behind desktop fallbacks.
- Android can be built and inspected on Windows/Linux CI. iOS compilation, signing, simulator, and
  device validation require a governed macOS/Xcode environment.
- Secure-storage loss after biometric enrollment change, device migration, restore, or key
  invalidation is fail-closed. Recovery is reauthentication and metadata resynchronization, not key
  export.

## Alternatives considered

- Retarget the entire Windows solution to .NET 10 in the same change.
- Reuse the Windows platform assembly and emulate unsupported services.
- Build unrelated SwiftUI and Kotlin applications with duplicated business logic.
- Use MAUI Essentials as an opaque security abstraction.
- Add a hosted service to centralize tokens or values.

These alternatives either enlarge unrelated release risk, obscure required platform controls,
duplicate authorization logic, or violate the accepted local-first boundary.
