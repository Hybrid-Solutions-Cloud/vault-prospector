# Browser integration and autofill threat model

**Status:** Internal design-review draft; independent review required before distribution  
**Date:** 2026-07-23  
**Related decision:** [ADR-0014](../adr/0014-user-initiated-origin-bound-browser-fill.md)  
**Research:** [SPK-0009](../spikes/0009-browser-integration-feasibility.md)

## Security objective

A browser page, frame, extension, browser profile, or native-message peer must never cause Vault
Prospector to disclose an arbitrary value. One user-initiated request may disclose only the exact
mapped value to the exact approved HTTPS origin, frame, document, tab, and field purpose after
policy approval and fresh local verification.

## Protected assets

- Azure secret values and offline cached values;
- connected identities, access paths, tokens, and tenant boundaries;
- encrypted origin-to-item mappings and browser policy;
- Windows verification and foreground-lock state;
- extension signing keys, store accounts, native-host manifests, and update channels;
- value-free audit evidence; and
- the user's ability to disable a compromised integration without unlocking the vault.

## Components and trust boundaries

1. hostile page JavaScript and DOM to isolated extension content script;
2. content script to extension background/service-worker context;
3. extension context to browser native-messaging implementation;
4. browser process to the browser-launched native host over framed standard I/O;
5. native host to the running desktop app over a current-user-only authenticated local channel;
6. desktop request coordinator to mapping, policy, verification, provider, and audit services;
7. extension package source to browser-store signing and update infrastructure; and
8. MSI installation to browser native-host manifests and Windows registry registration.

The native-host allowlist proves only which extension identity the browser allowed to launch the
host. It does not make page-supplied strings trustworthy. The desktop never trusts a caller-claimed
origin without the full extension, mapping, policy, and user-confirmation boundary.

## Required invariants

- Only a browser action, context-menu command, or registered shortcut creates a fill transaction.
- A transaction is single-use, expires quickly, and is bound to browser family, profile-independent
  extension identity, tab, frame, document nonce, top origin, frame origin, mapping, item, identity,
  and field purpose.
- Only canonical HTTPS origins are accepted. Fragment, path, query, credentials, opaque origins,
  wildcards, and unapproved ports do not participate in origin equality.
- The extension obtains tab and frame context from browser APIs; page messages cannot override it.
- A cross-origin frame needs its own explicit frame-origin mapping and an explicit allowed
  top-frame origin. Same-origin ancestry is not inferred after navigation.
- The desktop is already running, foreground-capable, and unlocked; the native host cannot open the
  encrypted repository or provider directly.
- Fresh Windows verification occurs after the confirmation UI identifies both destination origin
  and source item and before value retrieval.
- A value response contains one disposable value, no candidate list, metadata inventory, token, or
  alternate secret.
- Immediately before DOM assignment, the extension rechecks tab, frame, document, origin, field
  purpose, enabled/editable state, and transaction expiry.
- Audit, diagnostics, browser console output, native-host standard error, crashes, and protocol
  errors never contain the value.

## Threats, controls, and evidence

| Threat | Required control | Required evidence |
| --- | --- | --- |
| Arbitrary page requests every value | Exact enabled origin/field/item mapping; no discovery or candidate inventory in the browser protocol | Unmapped origin/item/field and enumeration tests |
| Page spoofs origin or frame | Background derives sender/tab/frame context from browser APIs; desktop binds top and frame origins | Spoofed message, cross-origin iframe, opaque frame, and `about:blank` tests |
| Redirect or navigation races fill | Short transaction plus tab/frame/document nonce and final pre-injection origin check | Same-origin and cross-origin navigation race tests |
| IDN/lookalike origin deceives user | Canonical ASCII equality; confirmation displays ASCII and reviewed Unicode; mapping creation warns on mixed/confusable labels | IDN, trailing-dot, case, Unicode, and lookalike tests |
| Compromised page replaces target field | Exact field purpose and conservative field eligibility; final enabled/editable/type/autocomplete check; no arbitrary selector from the page | Hidden, disabled, readonly, offscreen, shadow-DOM, and changed-field tests |
| Compromised extension fabricates requests | Signed allowlisted identity, minimal reviewed code, reproducible package, visible desktop confirmation, fresh verification, emergency host disable | Package provenance, store review, tampered-extension, and revocation drills |
| Another local process impersonates browser or desktop | Browser launches registered host; host validates browser-supplied caller origin/ID where available; desktop channel is current-user-only and uses an installation-scoped challenge | Wrong caller argument, pipe ACL, challenge replay, and peer-replacement tests |
| Native-message parser abuse | Four-byte framing, strict UTF-8/JSON schema, fixed maximum, one request/response, bounded strings/collections, unknown-operation rejection | Oversize, truncated, malformed, duplicate-property, Unicode, and fuzz tests |
| User-presence bypass | Desktop must be foreground/unlocked and fresh Windows verification must return `Verified`; every other outcome denies | Locked/background and all verification-result tests |
| Stale or switched identity/access | Mapping contains identity/access path; desktop rehydrates and validates ready state immediately before provider retrieval | Revoked, disabled, switched-tenant, removed-access, and stale-mapping tests |
| Secret leaks after cancellation/failure | Cancellation checks after every await; disposable value ownership; no retry or persistence in extension/native host | Cancellation, crash, timeout, log-canary, dump review, and buffer-lifetime tests |
| Extension gains broad browsing access | `activeTab` and `scripting` only; no `<all_urls>`, cookies, history, clipboard, downloads, or request interception | Manifest permission allowlist test and browser install-warning review |
| Malicious update or signing-account takeover | Protected publisher accounts, mandatory review, reproducible package/hash evidence, staged rollout, revocation and native-host deny switch | Update provenance and compromise-response exercise |
| Browser private database access | No file/profile/database code or password-vault import/sync API; only documented extension/native-messaging APIs | Static dependency/file-access review and sandbox monitoring |
| Fill into insecure page | HTTPS only; no certificate-error bypass; restricted internal/browser pages rejected by browser APIs and extension logic | HTTP, file, browser-internal, certificate-error, and mixed-frame tests |
| Audit becomes a secret copy | Store identifiers, canonical origins, purpose, timestamps, and result only | Canary and structured audit-schema tests |

## Protocol limits

- Native message payload: at most 64 KiB, below the Chromium host limit.
- Protocol version: exact integer `1`; no downgrade negotiation.
- One top-level JSON object with unique properties and strict enum values.
- Request identifiers and nonces: canonical random GUIDs generated by trusted components.
- Origin strings: at most 2,048 UTF-8 bytes before parsing and at most 255 ASCII host characters.
- Field purpose: allowlisted `username`, `password`, `oneTimeCode`, or explicitly reviewed `custom`;
  `custom` is disabled in the first release.
- Response: generic result code plus one value only on success. Error responses never reveal
  whether an item, mapping, identity, or cached value exists.

## Explicitly prohibited behavior

- background or page-load autofill;
- fill without a user gesture and fresh Windows verification;
- wildcard origins or all-site mappings;
- browser password-database, profile, cookie, history, or form-history access;
- importing, exporting, or synchronizing browser credentials;
- sending the browser a searchable Vault Prospector inventory;
- arbitrary DOM selectors supplied by pages or stored without a reviewed field purpose;
- value logging, browser storage, extension cache, native-host persistence, clipboard fallback, or
  automatic retry; and
- distribution of unsigned/developer-mode builds as a supported release.

## Compromise response

1. Disable browser integration through the machine policy/native-host kill switch without requiring
   repository unlock.
2. Revoke affected store packages or publisher credentials and stop their update channel.
3. Remove affected extension IDs from native-host manifests and issue a signed MSI repair/update.
4. Invalidate all outstanding transactions and rotate the desktop channel installation secret.
5. Preserve value-free audit and package provenance, notify affected users, and assess whether
   mapped source credentials require external rotation.
6. Resume only with a new signed identity/version, completed root-cause review, and repeated live
   browser/security evidence.

## Enablement gates

- accepted ADR and closed internal threat-model findings;
- strict protocol/native-host/desktop/extension implementation with negative and fuzz coverage;
- signed Chrome/Edge and Firefox packages from protected publisher accounts;
- installed-host, update, revocation, navigation, iframe, IDN, verification, accessibility, and
  secret-lifetime tests on supported browser versions;
- independent security review with no open critical/high findings;
- administrator and user deployment/rollback/compromise documentation; and
- validation of the exact signed MSI and extension packages before distribution.

