# In-app update threat model

Status: implemented for Windows Preview; exact installed-package lifecycle validation remains a release gate.

## Trust boundary

Vault Prospector never updates silently. The Settings page performs three separate user-controlled
actions: check, download and verify, and launch the verified installer. Release metadata and binaries
come only from the public, binary-only
`Hybrid-Solutions-Cloud/vault-prospector-releases` repository.

The client accepts a release only when:

- the GitHub Releases API response is successful over HTTPS;
- the release publisher is exactly `hcs-platform-app[bot]`;
- the release, asset, checksum, and release-page URLs remain under the exact release repository;
- the release is not a draft or marked withdrawn;
- the exact versioned MSI, checksum, and Sigstore bundle are all present;
- the MSI's authenticated GitHub `sha256:` asset digest is valid; and
- package names, versions, sizes, and semantic ordering satisfy bounded parsing rules.

The source repository remains private. The update client reads no source artifact and receives no
GitHub credential.

## Package verification and handoff

The MSI is streamed into a new partial file in
`%LOCALAPPDATA%\VaultProspector\updates\<version>`. The client rejects size mismatches and compares
the streamed SHA-256 digest with both the authenticated GitHub asset digest and the separately
published checksum. A failed or cancelled operation removes its partial file.

Before launch, Vault Prospector hashes the retained MSI again and rejects a missing, renamed, moved,
or changed package. Only then does it start `msiexec.exe /i <exact-path>` with Windows elevation.
The application locks and exits after Windows Installer starts. Installation decisions and any
elevation prompt remain under user and Windows control.

## Failure behavior

Offline, malformed, untrusted, withdrawn, oversized, redirected-outside-repository, incomplete, or
tampered input fails closed. No installer is launched, unverified release notes are not displayed,
and the rest of Vault Prospector remains usable.

## Local-data lifecycle

An in-place upgrade or reinstall by the same Windows account retains the existing
`%LOCALAPPDATA%\VaultProspector` data and DPAPI-bound encryption. Copying that data to another
Windows account or device is unsupported because the receiving account cannot decrypt it. Reset and
recovery operations remain explicit, independently verified workflows.

## Residual risk and release evidence

Public release assets use keyless Sigstore provenance and SHA-256 checksums because no paid
Authenticode certificate is available. Windows therefore may show an unknown publisher warning.
Each release must still pass the governed exact-package clean-VM upgrade, reinstall, downgrade, data
retention, and incompatible-data scenarios before the update story can close.
