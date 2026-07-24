# Research Spike: Platform credential integration

- **Status:** Native feasibility prototypes complete; live framework evidence open
- **Owner:** Vault Prospector maintainers
- **Created:** 2026-07-16
- **Updated:** 2026-07-24

## Objective

Determine whether Vault Prospector can use Apple Password AutoFill, Android Autofill, Windows
Credential Manager, browser AutoFill, or Windows Hello without presenting an arbitrary Azure value
as a website credential or weakening the accepted origin, mapping, and user-presence boundaries.

## Evaluated toolchains

| Surface | Evaluation baseline |
| --- | --- |
| Shared policy and native package contracts | .NET SDK 10.0.302; 43 mobile tests |
| Android | .NET for Android 36.1.2; Android API 36; minimum API 31 |
| Apple | .NET for iOS 26.0.11017; iOS minimum 18; macOS 26/Xcode 26.0.1 CI baseline |
| Windows | Windows App SDK/Win32 desktop implementation and Windows SDK 10.0.26100 contracts |

The Android and Apple extension sources compile against the listed reference packs. That is
source/API evidence, not live-device or store acceptance.

## Capability matrix

| Platform capability | Supported public mechanism | Vault Prospector decision | Remaining proof |
| --- | --- | --- | --- |
| Apple password suggestions | `ASCredentialProviderViewController`, `ASCredentialIdentityStore`, and password credential identities | Eligible only for username/password records explicitly mapped to one exact HTTPS origin; no arbitrary value inventory | Signed entitlement/provisioning, identity-store, user-verification, negative-origin, device, and App Store evidence |
| Apple no-UI credential request | Credential-provider callback can complete or request interaction | Always request interaction; the product requires fresh user verification and must not release a value while the extension UI is absent | Physical-device cancellation/re-entry and value-lifetime evidence |
| Apple app/extension data sharing | Explicit App Group/Keychain access group | Not enabled by the prototype. The extension cannot access the app database or wrapping key, preventing accidental bulk exposure | Independent design of a minimal encrypted mapping/one-record exchange before any enablement |
| Android field discovery | `AutofillService.onFillRequest` receives `AssistStructure` and explicit autofill hints | Parse only one HTTPS web domain and one unambiguous username/password field of each purpose; reject missing, mixed, duplicate, oversized, or unsupported fields | Instrumented app/WebView/browser matrix |
| Android protected fill | Authenticated `FillResponse`/`Dataset` through a foreground activity | Required for any future value. The prototype returns no dataset because no exact mapping and verified package/domain association are available | Fresh BiometricPrompt, exact mapping, Digital Asset Links/signature, cancellation, and negative-origin implementation/evidence |
| Android save/import | `onSaveRequest` can observe form data | Prohibited. The prototype acknowledges the callback without reading or persisting form values | Verify no storage/network/log side effects on devices |
| Windows verification | Window-bound `UserConsentVerifierInterop` | Supported as an action gate; it is not a secret store and does not authenticate an origin | Existing Windows Hello live matrix |
| Windows Credential Manager | Win32 Credentials Management API | Suitable only for narrow provider credentials already covered by a separate threat model; do not duplicate Azure values or browser inventories | Existing CyberArk credential-store review/live evidence |
| Browser one-time fill | Signed extension and authenticated native messaging | Separate Phase 11 design; exact mapping, origin/frame/purpose, foreground request, and fresh verification are mandatory | Signed installed-browser and independent evidence |

## Prototype results

### Shared policy

`MobileAutofillRequestAnalyzer` normalizes a default-port HTTPS origin and accepts only explicit
`username` or `password` hints. It rejects credentials in URLs, non-HTTPS schemes, ports, paths,
queries, fragments, ambiguous hints, duplicate purposes, empty requests, and oversized identifiers.
`MobileAutofillPolicy` then requires a secret object, exact saved mapping, foreground invocation,
and fresh verification before any value can be offered.

The policy/analyzer live in a dedicated assembly that references only the domain and canonical
browser-origin contracts. Native credential-provider targets do not inherit the application,
Azure provider, encrypted database, value cache, or UI dependency graph.

This is intentionally a two-stage decision:

1. native metadata may be parsed to decide whether a request is eligible for lookup; and
2. a value may be released only after the mapping and fresh-verification checks also pass.

Native request metadata alone never authorizes retrieval.

### Android

The application contains a real `AutofillService` declaration with the required
`BIND_AUTOFILL_SERVICE` permission, intent action, and metadata resource. Its bounded tree parser
walks the latest `AssistStructure`, requires exactly one HTTPS web origin, and passes only explicit
credential hints to the shared analyzer. It implements no save/import path.

The component is `android:enabled="false"` in the packaged manifest and always returns a null fill
response. This is deliberate: Android's guidance requires package/signature and Digital Asset
Links verification for web credentials, while the product additionally requires an encrypted
exact mapping and a fresh authenticated foreground response. Enabling a partial service would
create a false security claim and a poor system-settings experience.

### Apple

The iOS container embeds a credential-provider extension target with the AutoFill entitlement on
both targets. The extension subclasses `ASCredentialProviderViewController`, validates Apple
domain/URL service identifiers through the shared exact-HTTPS normalizer, and responds to no-UI
requests with `UserInteractionRequired`. It never returns a credential, lists a vault inventory,
or implements password/passkey/OTP save.

The prototype has no App Group and no shared Keychain access group. Consequently it cannot read
the containing application's encrypted database or device-bound wrapping key. This proves the
extension point and the desired least-privilege default without inventing an unsafe data-sharing
design.

### Windows

Windows Hello/UserConsentVerifier performs window-bound user verification for a sensitive action;
it does not bind that action to a website and is not a credential database. Windows Credential
Manager can store a deliberately scoped provider credential but would duplicate and broaden the
exposure of Azure values if used as a general Vault Prospector value store. The existing
SQLCipher/device-bound design remains authoritative.

## Security and user-experience implications

- Autofill is not a generic “send this vault item” feature. Only an explicit credential record may
  be mapped to one exact HTTPS origin and one username/password purpose.
- No wildcard, subdomain inheritance, nondefault port, HTTP, arbitrary custom field, key,
  certificate, one-time code, passkey, or background fill is allowed in the first release.
- Apple identity-store entries may contain only nonsecret display/lookup metadata and an opaque
  record identifier. Password values must never be placed there.
- Android request domains and caller packages are untrusted until canonical-domain, Digital Asset
  Links, and signing-certificate validation succeeds. No confirmation dialog may silently convert
  a failed association into a permanent wildcard.
- Both platforms need a visible, cancelable, fresh-verification step for every fill. Failure,
  backgrounding, timeout, mapping change, key invalidation, or provider error returns no dataset.
- The service/extension must not log, cache, copy, import, save, or retry the credential value.

## Recommendation

Keep mobile autofill disabled in distributed builds until the following design is independently
approved and implemented:

1. a user-created mobile mapping binds one item, identity, canonical HTTPS origin, and field
   purpose;
2. the native component receives only opaque, nonsecret mapping identifiers;
3. a foreground authentication UI performs fresh platform verification;
4. Android additionally validates the requesting package/domain association and signing
   certificate;
5. one mapped value is retrieved on demand, returned once, disposed, and audited without value
   content; and
6. signed physical-device negative-origin, lifecycle, accessibility, compromise, and store-review
   matrices pass.

Until then, manual bounded reveal/copy remains the supported mobile workflow. This is a capability
decision, not a claim that live autofill has shipped.

## Decision impact

- ADR-0016 remains accepted and is clarified by this result: credential-provider work is a
  separately gated native boundary.
- ADR-0014's exact-origin, explicit-purpose, fresh-verification, one-shot, and no-private-store
  principles also govern future mobile fill.
- A new ADR is required before enabling either native component because the encrypted
  app-to-extension mapping exchange and Android package/domain verification are not yet selected.

## Primary references

- [Apple: ASCredentialProviderViewController](https://developer.apple.com/documentation/authenticationservices/ascredentialproviderviewcontroller)
- [Apple: no-interaction password credential request](https://developer.apple.com/documentation/authenticationservices/ascredentialproviderviewcontroller/providecredentialwithoutuserinteraction%28for%3A%29-7jlg0)
- [Apple: extension configuration and identity store](https://developer.apple.com/documentation/authenticationservices/ascredentialproviderviewcontroller/prepareinterfaceforextensionconfiguration%28%29)
- [Android: build autofill services](https://developer.android.com/identity/autofill/autofill-services)
- [Android: AutofillService web security](https://developer.android.com/reference/android/service/autofill/AutofillService)
- [.NET for iOS app-extension build property](https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-properties#isappextension)
- [Windows: window-bound user verification](https://learn.microsoft.com/en-us/windows/win32/api/userconsentverifierinterop/nf-userconsentverifierinterop-iuserconsentverifierinterop-requestverificationforwindowasync)
- [Windows: Credentials Management](https://learn.microsoft.com/en-us/windows/win32/secauthn/credentials-management)
