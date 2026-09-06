# In-app update threat model

Status: explicit verified update installation is implemented for Windows Preview. It remains a
user-initiated, non-production path for an unsigned package.

## Trust boundary

Vault Prospector never updates silently. **Settings > Product updates** can check public release
metadata independently. **Install and verify update** is a separate explicit action that checks for
the newest trusted release, downloads its exact MSI into the app-owned local update directory,
verifies it, re-verifies it immediately before launch, and requests Windows administrator approval.

The client accepts metadata only when:

- the GitHub Releases API response is successful over HTTPS;
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

The app accepts only the exact versioned MSI named by trusted release metadata. Before retaining the
installer, it requires agreement between the GitHub asset SHA-256 digest, the adjacent checksum file,
the expected package name, the authenticated byte size, and the hash computed while streaming the
download. Partial or oversized files are deleted. The app stores the verified MSI only under
`%LOCALAPPDATA%\VaultProspector\updates`, rejects reparse-point update directories, and contains
every resolved path under that root.

Immediately before launch, the app resolves and contains the path again, confirms the file exists
under its exact release filename, and rehashes the bytes. Any change fails closed. Only then does it
invoke `msiexec.exe /i` with `runas`; Windows owns the administrator-consent prompt. Vault Prospector
locks and exits only after Windows accepts the process launch. Cancellation or rejection leaves the
app open.

The release must include a Sigstore bundle, and the public verification guide remains available for
independent provenance validation. The client does not itself perform Fulcio/Rekor verification.
Because Preview MSIs are not Authenticode-signed, Windows displays **Unknown Publisher**. The product
owner accepted this disclosed non-production risk on 2026-09-06 to make the explicit in-app update
action complete the installation workflow. Trusted signing or Store identity and independent
security review remain mandatory GA gates.

## Failure behavior

Offline, malformed, untrusted, withdrawn, oversized, incomplete, or hash-mismatched releases fail
closed. A changed retained installer is not launched, partial files are removed, untrusted release
notes are not displayed, and the rest of Vault Prospector remains usable.

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
