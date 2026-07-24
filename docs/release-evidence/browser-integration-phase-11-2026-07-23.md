# Browser integration Phase 11 evidence — 2026-07-23

## Result

The source tree contains an internal, fail-closed browser integration for Chromium-family and
Firefox-family browsers. It is not released or approved for production credentials. Signed browser
packages, live clean-machine browser execution, independent security review, and governed
distribution remain open.

## Implemented boundary

- A toolbar gesture grants temporary page access; manifests contain no persistent host access or
  content scripts.
- The extension derives tab, frame, document, and origin context from the browser and rechecks that
  context immediately before writing to the original focused field.
- The native-messaging protocol uses bounded, strict JSON; the native host and current-user desktop
  broker use authenticated, nonce-bound envelopes and reject replay.
- Native host configuration and manifests allow only the reviewed Chromium and Firefox extension
  identities.
- The desktop broker accepts only the exact installed native-host executable.
- A protected machine policy and an encrypted local mapping must independently allow the exact
  origin, frame, browser family, purpose, item, and identity.
- The visible, unlocked desktop application shows a one-time confirmation and requires fresh
  Windows verification before retrieval.
- Audit records never contain a secret value.
- The implementation does not read private browser credential databases and does not implement
  browser password-store import, export, or synchronization.

## Automated/local evidence

The locked Release gate completed with no known vulnerable direct or transitive NuGet packages,
formatting unchanged, and zero build warnings or errors. All 318 .NET tests passed:

- Application: 60 tests, including policy-before-mapping denial, policy revocation during
  confirmation, and identity-bound retrieval.
- Platform: 48 tests, including authenticated broker, replay rejection, extension identity,
  exact-client executable, and strict machine policy.
- App: 83 tests, including accessible browser confirmation and policy status.
- Browser protocol: 35; browser host: 8; infrastructure: 52; Azure provider: 27; domain: 4;
  static security: 1.

The browser extension's six Node tests and production build passed, including rejection of
password-creation and unlabelled username fields. Local candidate
`0.1.0-ci.1001` passed rollback-safe MSI schedule, shortcut-icon, native-host payload,
disabled-by-default policy, and exact HKLM Chrome, Edge, and Firefox registration checks. Its local
MSI SHA-256 is `C052B91A5B27AFDD2BB0DF63284CB7B34DBC25AAE277813729ACDC704D8A51BE`.
This transient local candidate is not a published artifact.

Exact-commit CI remains required after commit and push.

## Open release gates

- Independent security review and disposition of the browser threat model.
- Reviewed production extension keys and signed packages.
- Firefox signing and Chromium store or governed enterprise distribution acceptance.
- Authenticode signing for the MSI, app, and native host.
- Installed Chrome, Edge, and Firefox success/deny/replay/navigation/frame/session-lock tests.
- Extension update, rollback, compromise, revocation, and native-host removal exercise.
- Representative-user and assistive-technology validation of mapping and confirmation workflows.
- Exact-candidate CI, packaging, provenance, and clean-machine evidence.
