# Release Smoke-Test Checklist

Record the Windows version, package checksum, tester, and time with the release evidence. Do not use a production vault for preview validation.

## Package and startup

- [ ] SHA-256 checksum matches the published checksum file.
- [ ] Sigstore bundle and GitHub provenance attestation verify.
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
- [ ] CI build, tests, formatting, vulnerability scan, secret scan, and CodeQL are green for the tagged commit.
- [ ] Release contains ZIP, checksum, SPDX SBOM, Sigstore bundle, and GitHub attestation.
