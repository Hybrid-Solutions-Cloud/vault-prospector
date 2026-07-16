# Release and Artifact Verification

## Release contents

Each Windows release contains:

- a Windows x64 MSI installer;
- a self-contained `win-x64` ZIP;
- WinGet manifests and a Chocolatey `.nupkg`;
- SHA-256 checksum files;
- an SPDX JSON software bill of materials;
- Sigstore bundles for the installer and packaged artifacts.

The preview packages are signed with Sigstore keyless signing. Authenticode signing of individual Windows binaries remains a supply-chain hardening item.

## Verify the checksum

```powershell
$artifact = 'VaultProspector-0.1.0-preview.2-win-x64.msi'
$expected = (Get-Content "$artifact.sha256").Split(' ')[0]
$actual = (Get-FileHash $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw 'Checksum verification failed.' }
```

## Verify the Sigstore bundle

Install Cosign, then run:

```powershell
cosign verify-blob `
  --bundle VaultProspector-0.1.0-preview.2-win-x64.msi.sigstore.json `
  --certificate-identity-regexp '^https://github.com/Hybrid-Solutions-Cloud/vault-prospector/' `
  --certificate-oidc-issuer 'https://token.actions.githubusercontent.com' `
  VaultProspector-0.1.0-preview.2-win-x64.msi
```

GitHub-native artifact attestations are unavailable for this private repository under the organization's current plan. The workflow will also publish a GitHub attestation automatically if the repository becomes public.

## Maintainer release procedure

1. Confirm CI on `main` passes build, tests, formatting, .NET analyzer enforcement, dependency vulnerability auditing, and secret scanning.
2. Update release notes and version references.
3. Create and push an annotated `vX.Y.Z` or `vX.Y.Z-preview.N` tag.
4. The protected release workflow builds and tests on Windows, creates the MSI, portable ZIP, WinGet manifests, Chocolatey package, SBOM, checksums, and Sigstore bundles, then creates the source release.
5. Download the published assets and independently verify the checksum and Sigstore bundle.
6. Publish the same immutable artifacts to the public distribution repository, then submit the generated manifests to WinGet and Chocolatey by following [Windows package distribution](package-distribution.md).
7. Install the MSI on a clean supported Windows machine and complete the [release smoke-test checklist](release-checklist.md).

Rollback is performed by marking the release as withdrawn, documenting the reason, and directing users to the last verified release. Never replace assets under an existing version tag.

Exact publication, failure recovery, package withdrawal, incident response, and credential rotation
steps are maintained in the [Release operations and incident runbook](release-operations-runbook.md).
