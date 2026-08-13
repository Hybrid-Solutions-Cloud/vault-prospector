# In-app update threat model

Status: update discovery is implemented for Windows Preview; the application does not download or
launch Preview installers.

## Trust boundary

Vault Prospector never updates silently. **Settings > Product updates** performs one bounded,
read-only action: it checks public release metadata and reports whether a newer supported Preview
exists. It provides links to the exact public release history and the installation and verification
guide.

The client accepts metadata only when:

- the GitHub Releases API response is successful over HTTPS without redirects;
- the release publisher is exactly `hcs-platform-app[bot]`;
- the release, asset, checksum, Sigstore-bundle, and release-page URLs remain under the exact public
  `Hybrid-Solutions-Cloud/vault-prospector-releases` repository;
- the release is not a draft or marked withdrawn;
- the exact versioned MSI, checksum, and Sigstore bundle are all present;
- the MSI's GitHub `sha256:` asset digest is valid; and
- package names, versions, sizes, and semantic ordering satisfy bounded parsing rules.

The source repository remains private. The discovery client reads no source artifact and receives no
GitHub credential.

## Download, verification, and installation

Unsigned Preview installers are not downloaded, retained, verified, elevated, or launched by Vault
Prospector. This removes the local writable-file and privileged installer handoff from the
application's trust boundary. Users download only from the linked public binary release, validate
the adjacent SHA-256 checksum and keyless Sigstore provenance using the public guide, and explicitly
start the MSI through Windows.

A managed in-app installation flow must not be restored until the installed package has a trusted
Windows signature or store identity and the design has a race-free privileged handoff with
independent security evidence.

## Failure behavior

Offline, malformed, untrusted, withdrawn, oversized, or redirected release metadata fails closed.
No installer is downloaded or launched, untrusted release notes are not displayed, and the rest of
Vault Prospector remains usable.

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
