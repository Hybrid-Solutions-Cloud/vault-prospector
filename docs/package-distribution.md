# Windows Package Distribution

Vault Prospector ships as a Windows x64 MSI. The release process also creates a portable ZIP, WinGet manifests, and a Chocolatey package from the same build.

## User installation commands

After community approval, the stable package commands are:

```powershell
winget install --id HybridSolutionsCloud.VaultProspector --exact
choco install vault-prospector
```

For a Chocolatey preview release, add `--pre`.

## Why releases use a public distribution repository

The application source repository is private. WinGet and Chocolatey must be able to download an immutable installer without private-repository credentials, so approved release binaries are mirrored to the public `Hybrid-Solutions-Cloud/vault-prospector-releases` repository. Source code remains in the private application repository.

Never replace an asset under an existing version tag. Publish a new version if an installer, checksum, or manifest changes.

## Build the distribution artifacts

Run on Windows with PowerShell 7, .NET 9, WiX, WinGet, and Chocolatey available:

```powershell
pwsh ./scripts/PackageInstaller.ps1 -Version 0.1.0-preview.2
pwsh ./scripts/PackageDistribution.ps1 -Version 0.1.0-preview.2
winget validate --manifest ./artifacts/distribution/winget/HybridSolutionsCloud.VaultProspector/0.1.0-preview.2
```

`PackageInstaller.ps1` creates the MSI and checksum. `PackageDistribution.ps1` reads the MSI product identifiers and checksum, then creates the WinGet manifests, manifest archive, Chocolatey source package, `.nupkg`, and checksums.

## Publish the public installer

Set `GH_TOKEN` to the Hybrid Solutions Cloud GitHub App installation token. Do not use a personal access token to push or publish into the organization.

```powershell
pwsh ./scripts/PublishDistribution.ps1 -Version 0.1.0-preview.2
```

The script creates or updates the matching release in the public distribution repository and uploads the immutable artifacts.

## Submit to WinGet and Chocolatey

Package-manager submission requires credentials owned by the publisher accounts:

- the WinGet Manifest Creator OAuth credential cached by `wingetcreate token --store`;
- `CHOCOLATEY_API_KEY`: the API key for the Chocolatey Community Repository publisher account.

The Chocolatey key is stored in two places for separate consumers:

- GitHub repository secret `CHOCOLATEY_API_KEY` for release automation;
- HCS Key Vault secret
  `keyvault://kv-hcs-vault-01/hcs-vault-prospector-chocolatey-publisher-api-key`
  for local publishing sessions.

To create or rotate the Key Vault copy, authenticate to Azure and run the secure prompt:

```powershell
az login
pwsh ./scripts/Set-ChocolateyApiKeyInKeyVault.ps1
```

Paste the same API key used for the GitHub secret. The script does not echo or persist the
entered value, and applies the HCS-required tags with a 180-day expiration. Load it into a
later publishing session as `CHOCOLATEY_API_KEY` with the platform environment loader:

```powershell
. D:/git/platform/scripts/Load-HCSEnvironment.ps1
```

Install WinGet Manifest Creator once with `winget install Microsoft.WingetCreate`, authenticate with `wingetcreate token --store`, then run:

```powershell
pwsh ./scripts/SubmitPackageManagers.ps1 -Version 0.1.0-preview.2
```

The script submits the validated WinGet manifest directory and pushes the Chocolatey `.nupkg`. Both community services perform independent automated checks and moderation before the commands become available to users.

The first Microsoft contribution from a GitHub account may require that account holder to accept the Microsoft Contributor License Agreement on the generated pull request. This is a legal acceptance and must be completed by the account holder, not by release automation.
