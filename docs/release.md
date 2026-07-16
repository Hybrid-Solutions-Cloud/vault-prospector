# Release and Artifact Verification

## Release contents

Each Windows release contains:

- a self-contained `win-x64` ZIP;
- a SHA-256 checksum file;
- an SPDX JSON software bill of materials;
- a Sigstore bundle for the ZIP;
- a GitHub build-provenance attestation.

The preview ZIP is signed with Sigstore keyless signing. Authenticode signing of individual Windows binaries remains a supply-chain hardening item.

## Verify the checksum

```powershell
$archive = 'VaultProspector-0.1.0-preview.1-win-x64.zip'
$expected = (Get-Content "$archive.sha256").Split(' ')[0]
$actual = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw 'Checksum verification failed.' }
```

## Verify the Sigstore bundle

Install Cosign, then run:

```powershell
cosign verify-blob `
  --bundle VaultProspector-0.1.0-preview.1-win-x64.zip.sigstore.json `
  --certificate-identity-regexp '^https://github.com/Hybrid-Solutions-Cloud/vault-prospector/' `
  --certificate-oidc-issuer 'https://token.actions.githubusercontent.com' `
  VaultProspector-0.1.0-preview.1-win-x64.zip
```

GitHub's artifact attestation can also be verified with:

```powershell
gh attestation verify VaultProspector-0.1.0-preview.1-win-x64.zip `
  --repo Hybrid-Solutions-Cloud/vault-prospector
```

## Maintainer release procedure

1. Confirm CI on `main` passes build, tests, formatting, CodeQL, dependency review, and secret scanning.
2. Update release notes and version references.
3. Create and push an annotated `vX.Y.Z` or `vX.Y.Z-preview.N` tag.
4. The protected release workflow builds and tests on Windows, packages the self-contained application, generates the SBOM, signs and attests the archive, and creates the GitHub release.
5. Download the published assets and independently verify the checksum, Sigstore bundle, and GitHub attestation.
6. Launch the extracted app on a clean supported Windows machine and complete the [release smoke-test checklist](release-checklist.md).

Rollback is performed by marking the release as withdrawn, documenting the reason, and directing users to the last verified release. Never replace assets under an existing version tag.
