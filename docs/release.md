# Release and artifact verification

The free publicly trusted Windows channel is a
[Microsoft Store–signed MSIX](artifact-signing.md). Direct MSI, portable ZIP, and pre-ingestion
MSIX artifacts are unsigned and display **Unknown Publisher**. Their SHA-256 files, SPDX SBOM,
immutable release location, and Sigstore bundles provide integrity and provenance, not Windows
publisher trust.

## Release contents

Each Windows release contains:

- a Windows x64 MSI installer;
- an unsigned MSIX for Microsoft Store ingestion;
- a self-contained `win-x64` ZIP;
- WinGet manifests and a Chocolatey `.nupkg`;
- SHA-256 checksum files;
- an SPDX JSON software bill of materials; and
- Sigstore bundles for the packaged artifacts.

## Verify a checksum

```powershell
$artifact = 'VaultProspector-0.2.0-preview.4-win-x64.msi'
$expected = (Get-Content "$artifact.sha256").Split(' ')[0]
$actual = (Get-FileHash $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw 'Checksum verification failed.' }
```

## Maintainer procedure

1. Confirm GitHub CI on HCS runners passes for `main`.
2. Update release notes and version references.
3. Provision the ephemeral HCS Windows runner.
4. Create and push an annotated `vX.Y.Z` or `vX.Y.Z-preview.N` tag.
5. `.github/workflows/release.yml` rebuilds and tests the exact tag, creates packages, checksums,
   SPDX SBOM, and Sigstore bundles, then publishes binaries only to
   `Hybrid-Solutions-Cloud/vault-prospector-releases` using the HCS GitHub App.
6. Clean up the ephemeral Windows runner.
7. Independently verify the published assets and complete the
   [release smoke-test checklist](release-checklist.md).
8. For the trusted channel, rebuild with the exact Partner Center identity values, submit the MSIX,
   and record Store certification and clean-machine install/upgrade evidence.
9. Submit the immutable direct-download metadata to WinGet and Chocolatey as applicable.

Never replace assets under an existing version tag. Roll back by marking the release withdrawn,
recording the reason, and directing users to the last verified release.

Failure recovery, package withdrawal, incident response, and credential handling are maintained in
the [release operations and incident runbook](release-operations-runbook.md).
