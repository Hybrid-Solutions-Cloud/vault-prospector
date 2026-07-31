# Downloads

::: danger Preview software — major work in progress
Vault Prospector is under active development and is published for **non-production evaluation
only**. Direct packages are **unsigned**, so Windows displays **Unknown Publisher**. Features and
the local database format change between previews. Do not use this to manage production secrets.
:::

All artifacts are published to the [public distribution
repository](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases). The
source repository does not host binaries.

## Current release — `0.3.0-preview.3`

| Package | Download | Checksum | Signature |
| --- | --- | --- | --- |
| **Windows installer (MSI)** — recommended | [`.msi`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.msi) | [`.sha256`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.msi.sha256) | [`.sigstore.json`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.msi.sigstore.json) |
| **Portable ZIP** — no installer required | [`.zip`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.zip) | [`.sha256`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.zip.sha256) | [`.sigstore.json`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.zip.sigstore.json) |
| **MSIX package** | [`.msix`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.msix) | [`.sha256`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.msix.sha256) | [`.sigstore.json`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.msix.sigstore.json) |
| **Chocolatey package** | [`.nupkg`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/vault-prospector.0.3.0-preview.3.nupkg) | [`.sha256`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/vault-prospector.0.3.0-preview.3.nupkg.sha256) | [`.sigstore.json`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/vault-prospector.0.3.0-preview.3.nupkg.sigstore.json) |
| **WinGet manifests** | [`.zip`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-winget-manifests.zip) | [`.sha256`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-winget-manifests.zip.sha256) | [`.sigstore.json`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-winget-manifests.zip.sigstore.json) |
| **SBOM (SPDX)** | [`.spdx.json`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.spdx.json) | — | — |

`winget install` and `choco install` are not yet available — the community repositories must
approve the package first. See [Windows package distribution](/package-distribution) for status.

## Verify before installing

Because the direct packages are unsigned, the checksum and Sigstore bundle are the only integrity
evidence. Do not skip this step.

```powershell
# Compare against the published .sha256 file
(Get-FileHash .\VaultProspector-0.3.0-preview.3-win-x64.msi -Algorithm SHA256).Hash
```

Full instructions, including Sigstore bundle verification, are in the
[release verification guide](/release).

## Requirements

- Windows 10/11 x64.
- Windows Hello configured — it gates every secret reveal.
- A Microsoft Entra account with read access to the Key Vaults you want to index.

## Older releases

Every previous Preview remains available on the [releases
page](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases), with matching
[release notes](/release-notes/0.3.0-preview.3) and the [changelog](/changelog).

## Build from source

The [source repository](https://github.com/Hybrid-Solutions-Cloud/vault-prospector) builds with
PowerShell 7+ and the .NET SDK pinned in `global.json`:

```powershell
pwsh ./scripts/Build.ps1 -Configuration Release
pwsh ./scripts/PackageInstaller.ps1 -Version 0.3.0-preview.3
```
