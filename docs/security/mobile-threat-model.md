# Mobile application threat model

**Scope:** Phase 13 iOS and Android applications  
**Status:** Implementation baseline; independent and live-device review open  
**Last updated:** 2026-07-24

## Security objectives

- A local attacker must not obtain metadata keys, cached values, MSAL tokens, or revealed values
  from backup, migration, app-switcher snapshots, logs, crash output, clipboard history, or
  background execution.
- Every reveal, copy, and autofill action requires a foreground session, a current identity/source
  binding, explicit intent, and fresh platform verification.
- Losing or invalidating device-bound key material fails closed. The supported recovery path is
  local reset, provider reauthentication, and metadata resynchronization.
- Mobile applications communicate directly with supported provider endpoints. Vault Prospector
  does not become a custodian through a project-hosted relay.

## Assets and trust boundaries

Assets are MSAL refresh/access tokens, the SQLCipher metadata key and database, offline-value keys
and ciphertext, identity/source mappings, explicit autofill mappings, revealed values, and
value-free audit records.

Trust boundaries are:

1. Avalonia shared UI and mobile application core.
2. iOS host, Keychain, LocalAuthentication, protected-data lifecycle, pasteboard, and UIKit.
3. Android host, Android Keystore, BiometricPrompt, clipboard, lifecycle, and window manager.
4. MSAL system browser/broker callback and platform token cache.
5. Azure Resource Manager and Key Vault TLS endpoints.
6. Future Apple credential-provider or Android Autofill/Credential Manager extensions.
7. App Store/Play signing, review, backup, and distribution infrastructure.

The device OS, hardware-backed key services, system authentication UI, and supported provider TLS
endpoints are trusted within their documented guarantees. Rooted/jailbroken devices, accessibility
services, keyboards, screen-recording hardware, compromised operating systems, and malicious
co-resident apps remain adversarial.

## Required controls

| Threat | Required control | Evidence gate |
| --- | --- | --- |
| Database or cache copied from device/backup | SQLCipher/AES-GCM with random keys wrapped by device-bound platform storage; explicit backup and data-transfer exclusion | Automated envelope/canary tests plus encrypted backup/device-migration inspection |
| Key use after biometric enrollment change or device migration | Current-enrollment/device-only access control; fail closed on invalidation; resync recovery | Real-device enrollment-change, restore, migration, and reset matrix |
| App-switcher disclosure | Cover sensitive UI before background snapshot; clear revealed state and cancel work | Automated lifecycle state tests plus app-switcher screenshots |
| Screenshot or recording | Android `FLAG_SECURE`; iOS privacy cover and capture-state response with honest post-capture limitation | Real-device screenshot/recording matrix |
| Clipboard history or cross-app read | Policy default off; explicit copy; sensitive labeling where supported; bounded auto-clear; clear on background | Clipboard ownership/timeout/background/live-device tests |
| Background retrieval race | Session cancellation token revoked synchronously on background/lock; late values disposed and never rendered | Unit/concurrency and lifecycle tests |
| Token theft or identity confusion | Platform MSAL cache, exact application callback, selected account binding, silent-first token use, removal on disconnect | Multi-account/tenant/broker/callback/revocation live matrix |
| Secret in diagnostics or crashes | Allow-listed value-free fields; safe exception categories; no request/response bodies or revealed strings | Canary scans, crash/log inspection, independent review |
| Overlay/tapjacking or untrusted display | Android secure window and obscured-touch review; visible confirmation and system-owned authentication UI | Device/manual security review |
| Autofill origin confusion | Exact canonical HTTPS origin, explicit item/identity/field mapping, foreground invocation, fresh verification, one-shot response | Negative-origin/frame/mapping tests and native framework review |
| Extension compromise | Separate least-privilege extension process; no bulk export, wildcard mapping, background sync, or key/certificate exposure | Extension threat model, compromise/revocation exercise |
| Unsupported platform behavior | Fail closed when protected storage, local verification, backup exclusion, or privacy controls are unavailable | Capability policy tests and unsupported-device matrix |

## Platform-specific boundaries

### Android

- Target API 36; minimum API 31.
- Use Android Keystore keys restricted to encryption/decryption and current device authentication.
- Use the platform BiometricPrompt with device credential fallback only when policy permits.
- Reject touches flagged as obscured or partially obscured before Avalonia receives them.
- Set `FLAG_SECURE` before rendering and keep it set for every activity that can show sensitive
  state.
- Set `allowBackup="false"` and explicit `dataExtractionRules`/`fullBackupContent` exclusions;
  do not rely on `allowBackup` alone for device-to-device transfer behavior.
- Mark copied content sensitive where the platform supports it and clear only content the
  application still owns.

### iOS

- Target iOS 18 or later.
- Store wrapping material as device-only, when-unlocked Keychain data protected by the current
  biometric set. Do not permit passcode fallback for key access or synchronize it through iCloud
  Keychain.
- Treat protected-data unavailability, passcode removal, enrollment changes, and Keychain status
  errors as lock or reset conditions.
- Remove stale install-bound Keychain items when the private, backup-excluded installation marker
  is absent so uninstall/reinstall cannot silently inherit prior local wrapping material.
- Cover the UI before background snapshots and clear it only after foreground unlock.
- Observe screen capture state and screenshot notifications, but do not claim that iOS prevents a
  screenshot already taken.
- Exclude database, cache, and diagnostic files from iCloud/iTunes backup.

## Explicit non-goals

- Defending a revealed value from a fully compromised OS, rooted/jailbroken device, malicious
  keyboard, external camera, or authorized accessibility service.
- Exporting platform keys or restoring encrypted local state to another device.
- Silent background value retrieval.
- Offering arbitrary vault values through password-autofill APIs.
- Treating simulator, unsigned package, CI compilation, or source inspection as store/device
  acceptance.

## Open validation gates

- Governed iOS and Android physical devices across supported OS versions.
- Biometric enrollment, passcode removal, lockout, cancel, unavailable, and recovery behavior.
- Background/suspend/terminate, app-switcher, screenshot/recording, clipboard, backup, restore,
  migration, reinstall, low-memory, offline, and token-expiry matrices.
- VoiceOver/TalkBack, switch/keyboard navigation, text scaling, contrast, reduced motion, and
  representative-user validation.
- MSAL broker/system-browser, guest tenant, MFA, Conditional Access, cancellation, and account
  removal.
- Signed TestFlight/closed-test artifacts, SBOM/provenance, privacy/data-safety declarations, store
  review, and independent security approval.
