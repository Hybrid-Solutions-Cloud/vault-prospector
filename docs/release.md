# Release and Artifact Verification

Stable and GA Windows releases require the one-time [Azure Artifact Signing setup](artifact-signing.md).
The protected workflow may publish an explicitly labeled `vX.Y.Z-preview.N` evaluation release
without Authenticode when signing is unavailable; all stable tags still fail closed.

## Release contents

Each Windows release contains:

- a Windows x64 MSI installer;
- a self-contained `win-x64` ZIP;
- WinGet manifests and a Chocolatey `.nupkg`;
- SHA-256 checksum files;
- an SPDX JSON software bill of materials;
- Sigstore bundles for the installer and packaged artifacts.

Unsigned Preview evaluation candidates must carry checksums, an SPDX SBOM, HCS Key Vault-backed
Cosign bundles, explicit Unknown Publisher guidance, and immutable provenance. Stable and GA
candidates must additionally carry Windows Authenticode signatures with RFC 3161 timestamps.

## Verify the checksum

```powershell
$artifact = 'VaultProspector-0.1.1-preview.1-win-x64.msi'
$expected = (Get-Content "$artifact.sha256").Split(' ')[0]
$actual = (Get-FileHash $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw 'Checksum verification failed.' }
```

## Verify the Sigstore bundle

Install Cosign, then run:

```powershell
$version = '<version>'
cosign verify-blob `
  --key release/vault-prospector-release-signing.pub `
  --bundle "VaultProspector-$version-win-x64.msi.sigstore.json" `
  "VaultProspector-$version-win-x64.msi"
```

The signing private key is non-exportable in `kv-hcs-vault-01`. Azure DevOps receives only
key-scoped signing permission through the `HCS Platform Azure` service connection. The public key
is committed at `release/vault-prospector-release-signing.pub`. Releases through
`0.1.1-preview.1` predate this migration and retain their original GitHub OIDC verification
instructions in their immutable release evidence.

## Maintainer release procedure

1. Confirm CI on `main` passes build, tests, formatting, .NET analyzer enforcement, dependency vulnerability auditing, and secret scanning.
2. Update release notes and version references.
3. Create and push an annotated `vX.Y.Z` or `vX.Y.Z-preview.N` tag.
4. The protected Azure DevOps release pipeline builds and tests on Windows, creates the MSI,
   portable ZIP, WinGet manifests, Chocolatey package, SBOM, checksums, and Cosign bundles, then
   creates the immutable public distribution release.
5. Download the published assets and independently verify the checksum and Sigstore bundle.
6. Publish the same immutable artifacts to the public distribution repository, then submit the generated manifests to WinGet and Chocolatey by following [Windows package distribution](package-distribution.md).
7. Install the MSI on a clean supported Windows machine and complete the [release smoke-test checklist](release-checklist.md).

Rollback is performed by marking the release as withdrawn, documenting the reason, and directing users to the last verified release. Never replace assets under an existing version tag.

Exact publication, failure recovery, package withdrawal, incident response, and credential rotation
steps are maintained in the [Release operations and incident runbook](release-operations-runbook.md).
