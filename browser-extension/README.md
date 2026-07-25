# Vault Prospector browser extension

This directory contains the shared source and separate manifests for the Chromium-family and
Firefox one-shot fill extension. It is security-review source, not a supported distributed
extension.

The extension:

- runs only after the user invokes its toolbar action;
- requests `activeTab`, `scripting`, and `nativeMessaging`;
- inspects only the focused eligible field in the active tab;
- sends browser-derived tab, frame, document, origin, and field-purpose context to the registered
  Vault Prospector native host;
- opens the desktop guided-mapping flow when that exact destination does not yet have a mapping,
  without asking the user to type or copy an origin;
- fills one approved value after the desktop app confirms the exact mapping and completes Windows
  verification; and
- stores no values, mappings, history, cookies, or page content.

It does not request `<all_urls>`, inject a persistent content script, read browser password
databases, or perform background/page-load autofill.

## Local verification

```powershell
npm test
npm run build
```

`npm run build` creates unpacked validation directories under `dist/`. Those directories are
ignored and are not signed release packages.

Store identities, signed packages, installed-browser testing, and independent security review are
required before distribution. See the
[threat model](../docs/security/browser-integration-threat-model.md).
