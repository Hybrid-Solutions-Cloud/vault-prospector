# Release Smoke-Test Checklist

Record the Windows version, package checksum, tester, and time with the release evidence. Do not use a production vault for preview validation.

## Package and startup

- [ ] SHA-256 checksum matches the published checksum file.
- [ ] Sigstore bundle verifies against the repository's GitHub Actions identity.
- [ ] MSI installs silently with exit code 0, registers in Installed apps, and creates the Start menu shortcut.
- [ ] Forced MSI repair restores a deliberately changed packaged non-secret file.
- [ ] MSI upgrade replaces the previous version without leaving duplicate Installed apps entries.
- [ ] Installing the previous MSI over the current version is rejected and leaves the current version installed.
- [ ] MSI uninstall removes program files and the Start menu shortcut without deleting user state.
- [ ] ZIP extracts without an installer or administrator rights.
- [ ] `VaultProspector.App.exe` starts and shows the Vault Prospector icon and title.
- [ ] First start creates protected local state under `%LOCALAPPDATA%\VaultProspector` and no plaintext SQLite header.

## Authentication and discovery

- [ ] A consented multi-tenant public-client application ID signs in through the system browser.
- [ ] MFA and applicable Conditional Access complete without the app collecting a password or client secret.
- [ ] Two test identities can be connected and remain distinct.
- [ ] Sync discovers expected subscriptions and vaults and can be cancelled safely.
- [ ] A deliberately inaccessible vault reports an isolated safe error while accessible vaults remain searchable.

## Search and retrieval

- [ ] Metadata search works after network access is disabled.
- [ ] Identity, workspace, tenant, subscription, vault, type, enabled, expired, favorite, stale, and recent controls return expected results.
- [ ] Secret, key, and certificate versions display their source vault and identity.
- [ ] Reveal and copy reject keys/certificates and require Windows Hello for secrets.
- [ ] Revealed text is remasked and clipboard text clears only when it remains unchanged.

## Offline values and removal

- [ ] Offline value caching is disabled by default.
- [ ] Enabled caching requires Windows Hello and caps the requested lifetime.
- [ ] Open offline succeeds without Azure only before expiry and only after Windows Hello.
- [ ] Changed metadata fingerprints and expiration invalidate cached values.
- [ ] Item, vault, workspace, and global purge remove only the intended encrypted payloads.
- [ ] Identity removal clears the account cache entry; full local-state removal works after the app closes.

## Diagnostic and release evidence

- [ ] Logs contain event categories and pseudonymous IDs but no token, value, username, vault name, or object name.
- [ ] CI build, tests, formatting, .NET analyzers, vulnerability scan, and secret scan are green for the tagged commit.
- [ ] Release contains MSI, ZIP, WinGet manifests, Chocolatey package, checksums, SPDX SBOM, and Sigstore bundles.
- [ ] `winget validate` succeeds against the generated manifest directory without warnings.

## Repeatable MSI lifecycle scenario

Run this only from an elevated PowerShell 7 session on a Windows system that does not already have Vault Prospector installed. Supply hashes copied from the published checksum files; do not calculate an expected value from the MSI under test.

```powershell
pwsh ./tests/scenario/windows-installer-lifecycle.scenario.ps1 `
  -PreviousMsiPath <previous.msi> `
  -PreviousSha256 <published-previous-sha256> `
  -CurrentMsiPath <current.msi> `
  -CurrentSha256 <published-current-sha256>
```

The scenario refuses a pre-existing installation and writes timestamped JSON plus verbose MSI logs under `artifacts/installer-lifecycle`. It installs the previous version, upgrades, deliberately changes a packaged non-secret runtime configuration, proves forced repair restores it, proves downgrade rejection preserves the current version, uninstalls, verifies program/shortcut cleanup and retained `%LOCALAPPDATA%` state, then removes only its own sentinel. Archive the result and logs with restricted release evidence; MSI logs can contain machine paths and should not be published without review.
