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

Run on Windows with PowerShell 7, .NET 10, WiX, WinGet, and Chocolatey available:

```powershell
pwsh ./scripts/PackageInstaller.ps1 -Version 0.3.0-preview.5
pwsh ./scripts/PackageDistribution.ps1 -Version 0.3.0-preview.5
winget validate --manifest ./artifacts/distribution/winget/HybridSolutionsCloud.VaultProspector/0.3.0-preview.5
```

`PackageInstaller.ps1` creates the MSI and checksum. `PackageDistribution.ps1` reads the MSI product identifiers and checksum, then creates the WinGet manifests, manifest archive, Chocolatey source package, `.nupkg`, and checksums.

## CI validation candidates

Every successful push to `main` builds a unique `0.3.0-ci.<run-number>` package set on the
ephemeral HCS Windows runner. The governed GitHub Actions job validates the MSI, MSIX, WinGet
manifest archive, Chocolatey package, checksums, and installer lifecycle against the exact source.
Actions artifact retention is best effort while organization storage is exhausted; a retention
warning never substitutes for or invalidates the mandatory build and test result.

CI candidates exist for clean-machine validation and are not package-manager submissions. The
protected tag pipeline creates publishable Preview or stable artifacts. A Preview may be unsigned
only when explicitly labeled and documented; stable and GA artifacts require trusted signing.

## Publish the public installer

Set `GH_TOKEN` to the Hybrid Solutions Cloud GitHub App installation token. Do not use a personal access token to push or publish into the organization.

```powershell
pwsh ./scripts/PublishDistribution.ps1 -Version 0.3.0-preview.5
```

The script creates the matching release in the public distribution repository and uploads the
immutable artifacts. It refuses to run if that release already exists; publish a new version rather
than replacing any asset.

## Submit to WinGet and Chocolatey

Package-manager submission requires credentials owned by the publisher accounts:

- the WinGet Manifest Creator OAuth credential cached by `wingetcreate token --store`;
- `CHOCOLATEY_API_KEY`: the API key for the Chocolatey Community Repository publisher account.

### WinGet publisher identity

WinGet does not require a separate publisher portal account. Community-repository submissions
are GitHub pull requests to `microsoft/winget-pkgs`. Vault Prospector submissions use the
personal GitHub account `kristopherjturner`, because that account owns the contribution and has
accepted the Microsoft Contributor License Agreement. The first submission is
[`microsoft/winget-pkgs#403473`](https://github.com/microsoft/winget-pkgs/pull/403473).

`wingetcreate token --store` initiates GitHub OAuth and stores the resulting credential in the
local WinGetCreate token cache. It is not a Vault Prospector application secret and does not
need a Key Vault entry for the current manual submission process. Do not pass a token with the
`--token` command-line argument because it can be recorded in command history or logs.

If WinGet submission is automated later, use a separately scoped GitHub credential owned by
`kristopherjturner`, store it as a protected automation secret, and document its rotation. The
Hybrid Solutions Cloud GitHub App cannot submit to `microsoft/winget-pkgs` unless Microsoft
installs that app in its organization, so the HCS App token is not a substitute for this
contributor credential.

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
pwsh ./scripts/SubmitPackageManagers.ps1 -Version 0.3.0-preview.5
```

The script submits the validated WinGet manifest directory and pushes the Chocolatey `.nupkg`. Both community services perform independent automated checks and moderation before the commands become available to users.

The Microsoft CLA for `kristopherjturner` was accepted successfully on the first Vault Prospector
submission. The acceptance applies to future Microsoft repository contributions from that GitHub
identity unless Microsoft requires it to be renewed.
