# Browser integration

Vault Prospector's browser integration is an unreleased, fail-closed Phase 11 implementation for
explicit, one-time fills. It does not provide background autofill, enumerate a browser password
store, import credentials, or offer arbitrary vault values to a page.

## Security model

A fill succeeds only when all of these independent checks pass:

1. The user invokes the installed extension from its toolbar button.
2. The browser supplies the current tab, frame, document, and HTTPS origins.
3. The focused editable field has a supported purpose: username, password, or one-time code.
4. The signed extension identity is allowlisted by the native-host manifest and host configuration.
5. The native host is the exact installed executable and authenticates to the current-user desktop
   broker.
6. Protected machine policy allows the exact top origin, frame origin, browser family, and field
   purpose.
7. An enabled local mapping selects one secret and one connected identity for that exact
   destination and purpose.
8. The desktop application is visible, unlocked, and ready.
9. The user reviews the destination, purpose, and source and chooses **Verify and fill once**.
10. Fresh Windows verification succeeds, the mapping and page context are rechecked, and the value
    is written only to the same focused element.

Every failure denies the operation without returning a value. Audit rows contain identifiers,
origins, purpose, time, and result, but never the value.

See the [browser threat model](security/browser-integration-threat-model.md), [ADR
0014](adr/0014-user-initiated-origin-bound-browser-fill.md), and [feasibility
spike](spikes/0009-browser-integration-feasibility.md) for the design boundary.

## Administrator policy

The MSI installs `browser-fill-policy.json` beside `VaultProspector.App.exe` under the protected
per-machine installation directory. Its shipped state is disabled:

```json
{
  "version": 1,
  "enabled": false,
  "allowedDestinations": []
}
```

An administrator may replace it with an exact allowlist:

```json
{
  "version": 1,
  "enabled": true,
  "allowedDestinations": [
    {
      "topOrigin": "https://login.example.com",
      "frameOrigin": "https://login.example.com",
      "browserFamilies": ["chromium"],
      "fieldPurposes": ["username", "password"]
    }
  ]
}
```

Supported browser families are `chromium` and `firefox`. Supported purposes are `username`,
`password`, and `oneTimeCode`. Origins must be canonical HTTPS origins with no path, query,
fragment, wildcard, user information, or unapproved non-default port. An embedded login frame needs
its own exact `frameOrigin`; use the top origin only when the field is in the top frame.

Keep the file owned and writable only by administrators. Restart Vault Prospector after changing
the policy. The Browser tab reports whether policy loaded. Missing, disabled, malformed, duplicate,
oversized, reparse-point, or out-of-installation policy fails closed.

## User workflow

1. On the intended HTTPS page, focus a supported username, current-password, or one-time-code
   field and invoke the Vault Prospector extension.
2. The extension supplies the canonical top-frame origin, target-frame origin, browser family,
   frame, and focused-field purpose. The desktop application opens **Browser fill**; the user never
   types or copies an origin.
3. Review the setup check. It shows whether the extension/native-host/broker path reached the
   desktop and whether protected machine policy permits the exact destination.
4. If no mapping exists, select one eligible secret and one connected identity in the guided
   desktop card, review the exact destination, and create the mapping. Saving retrieves no value
   and cannot override machine policy.
5. Return to the same browser field and invoke the extension again.
6. Review the exact destination, purpose, and source in the desktop confirmation and choose
   **Verify and fill once**, or deny it.

Mappings are encrypted in local metadata. Removing an identity or item removes its mappings while
retaining value-free audit history. A capture that creates a mapping is deliberately denied in the
browser; a new explicit browser gesture is required for the first fill, so setup cannot become an
implicit fill.

## Development and release validation

Build and test the unpacked extension:

```powershell
Set-Location browser-extension
npm test
npm run build
```

Build the Windows package and validate the host payload, default policy, and exact HKLM native-host
registrations:

```powershell
pwsh ./scripts/PackageInstaller.ps1 -Version 0.1.0-ci.1
pwsh ./scripts/Test-BrowserHostInstaller.ps1 `
  -InstallerPath ./artifacts/VaultProspector-0.1.0-ci.1-win-x64.msi `
  -PublishDirectory ./artifacts/publish-win-x64
```

Developer builds remain unsuitable for real credentials. Public distribution requires reviewed,
signed extension packages, a trusted signed desktop/native-host candidate, clean-machine browser
tests, browser-store or governed-enterprise distribution review, and a documented compromise and
revocation exercise.
