# Release Smoke-Test Checklist

Record the Windows version, package checksum, tester, and time with the release evidence. Do not use a production vault for preview validation.

## Package and startup

- [ ] SHA-256 checksum matches the published checksum file.
- [ ] Sigstore bundle verifies with `release/vault-prospector-release-signing.pub`.
- [ ] Direct MSI/ZIP/pre-ingestion MSIX artifacts are labeled unsigned and show `NotSigned`.
- [ ] For stable/GA trust, the Microsoft Store–signed MSIX installs from the Store and Windows
  identifies the certified publisher.
- [ ] MSI installs silently with exit code 0, registers in Installed apps, and creates the Start menu shortcut.
- [ ] Forced MSI repair restores a deliberately changed packaged non-secret file.
- [ ] MSI upgrade replaces the previous version without leaving duplicate Installed apps entries.
- [ ] A deliberately failed upgrade after `InstallFiles` restores the previous registration,
  byte-identical packaged files, shortcut, and retained user state.
- [ ] Installing the previous MSI over the current version is rejected and leaves the current version installed.
- [ ] MSI uninstall removes program files and the Start menu shortcut without deleting user state.
- [ ] ZIP extracts without an installer or administrator rights.
- [ ] `VaultProspector.App.exe` starts and shows the Vault Prospector icon and title.
- [ ] The Start-menu and Windows Search entry show the Vault Prospector icon rather than a generic
  document; `Test-InstallerShortcutIcon.ps1` passes against the exact candidate MSI.
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
- [ ] Closing the application clears an unchanged Vault Prospector clipboard value without clearing unrelated replacement content.

## Offline values and removal

- [ ] Offline value caching is disabled by default.
- [ ] Enabled caching requires Windows Hello and caps the requested lifetime.
- [ ] Open offline succeeds without Azure only before expiry and only after Windows Hello.
- [ ] Changed metadata fingerprints and expiration invalidate cached values.
- [ ] Item, vault, workspace, and global purge remove only the intended encrypted payloads.
- [ ] Identity removal clears the account cache entry; full local-state removal works after the app closes.

## Browser integration

- [ ] The packaged default machine browser-fill policy is disabled and protected from standard-user writes.
- [ ] The exact reviewed Chrome/Edge and Firefox extension packages are signed and their identities match the native-host allowlists.
- [ ] The MSI contains the native host and exact HKLM Chrome, Edge, and Firefox registrations.
- [ ] Unmapped, disabled-policy, wrong-origin, wrong-frame, wrong-purpose, wrong-extension,
  wrong-host-process, replayed, expired, navigated, hidden-window, locked-session, and denied
  requests return no value.
- [ ] A successful fill requires a toolbar gesture, unchanged focused field, visible desktop
  confirmation, and fresh Windows verification.
- [ ] Browser audit and diagnostic inspection contains no values, tokens, usernames, vault names,
  or object names.
- [ ] Update, rollback, extension compromise/revocation, native-host removal, and browser uninstall
  exercises pass on the exact signed candidate.

## Diagnostic and release evidence

- [ ] Logs contain event categories and pseudonymous IDs but no token, value, username, vault name, or object name.
- [ ] CI build, tests, formatting, .NET analyzers, vulnerability scan, and secret scan are green for the tagged commit.
- [ ] Release contains MSI, MSIX, ZIP, WinGet manifests, Chocolatey package, checksums, SPDX SBOM,
  and Sigstore bundles.
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

The scenario refuses a pre-existing installation and writes timestamped JSON plus verbose MSI logs under `artifacts/installer-lifecycle`. It installs the previous version, injects a deterministic post-`InstallFiles` failure into a test-only copy of the candidate, proves transactional rollback restores the previous version, then completes the genuine upgrade. It also deliberately changes a packaged non-secret runtime configuration, proves forced repair restores it, proves downgrade rejection preserves the current version, uninstalls, verifies program/shortcut cleanup and retained `%LOCALAPPDATA%` state, then removes only its own sentinel. Archive the structured result with release evidence. Keep verbose MSI logs restricted because they can contain machine paths; never publish the deliberately modified rollback-probe MSI.
