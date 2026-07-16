#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0-preview.2',

    [ValidatePattern('^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$')]
    [string]$DistributionRepository = 'Hybrid-Solutions-Cloud/vault-prospector-releases',

    [ValidatePattern('^[0-9A-Za-z._-]+$')]
    [string]$OutputDirectory = 'artifacts',

    [switch]$SkipChocolateyPack
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot $OutputDirectory
$installerName = "VaultProspector-$Version-win-x64.msi"
$installerPath = Join-Path $outputRoot $installerName
$installerUrl = "https://github.com/$DistributionRepository/releases/download/v$Version/$installerName"
$distributionRoot = Join-Path $outputRoot 'distribution'
$wingetRoot = Join-Path $distributionRoot "winget/HybridSolutionsCloud.VaultProspector/$Version"
$wingetArchive = Join-Path $outputRoot "VaultProspector-$Version-winget-manifests.zip"
$chocolateyRoot = Join-Path $distributionRoot 'chocolatey/vault-prospector'
$chocolateyTools = Join-Path $chocolateyRoot 'tools'
$chocolateyPackage = Join-Path $outputRoot "vault-prospector.$Version.nupkg"

function Get-MsiProperty {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Property
    )

    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($Path, 0)
    $view = $database.OpenView("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = '$Property'")
    [void]$view.Execute()
    $record = $view.Fetch()
    if ($null -eq $record) {
        throw "MSI property '$Property' was not found in '$Path'."
    }

    return $record.StringData(1).Trim()
}

if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer was not found at '$installerPath'. Run PackageInstaller.ps1 first."
}

if (Test-Path -LiteralPath $distributionRoot) {
    Remove-Item -LiteralPath $distributionRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $wingetRoot, $chocolateyTools -Force | Out-Null

$installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToUpperInvariant()
$productCode = Get-MsiProperty -Path $installerPath -Property 'ProductCode'
$productVersion = Get-MsiProperty -Path $installerPath -Property 'ProductVersion'
$upgradeCode = Get-MsiProperty -Path $installerPath -Property 'UpgradeCode'

$versionManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.12.0.schema.json
# Created by scripts/PackageDistribution.ps1
PackageIdentifier: HybridSolutionsCloud.VaultProspector
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.12.0
"@

$installerManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.12.0.schema.json
# Created by scripts/PackageDistribution.ps1
PackageIdentifier: HybridSolutionsCloud.VaultProspector
PackageVersion: $Version
InstallerType: wix
Scope: machine
InstallModes:
  - interactive
  - silent
  - silentWithProgress
UpgradeBehavior: install
ReleaseDate: $(Get-Date -Format 'yyyy-MM-dd')
Installers:
  - Architecture: x64
    InstallerUrl: $installerUrl
    InstallerSha256: $installerHash
    ProductCode: '$productCode'
    AppsAndFeaturesEntries:
      - DisplayName: Vault Prospector
        Publisher: Hybrid Solutions Cloud
        DisplayVersion: '$productVersion'
        ProductCode: '$productCode'
        UpgradeCode: '$upgradeCode'
        InstallerType: wix
ManifestType: installer
ManifestVersion: 1.12.0
"@

$localeManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.12.0.schema.json
# Created by scripts/PackageDistribution.ps1
PackageIdentifier: HybridSolutionsCloud.VaultProspector
PackageVersion: $Version
PackageLocale: en-US
Publisher: Hybrid Solutions Cloud
PublisherUrl: https://github.com/Hybrid-Solutions-Cloud
PublisherSupportUrl: https://github.com/$DistributionRepository/issues
PackageName: Vault Prospector
PackageUrl: https://github.com/$DistributionRepository
License: MIT
LicenseUrl: https://github.com/$DistributionRepository/blob/main/LICENSE
ShortDescription: Securely discover and search Azure Key Vault metadata across Microsoft Entra identities.
Description: Vault Prospector is a local-first Windows application for encrypted Azure Key Vault metadata discovery, offline search, and explicit Windows Hello-gated secret retrieval.
Tags:
  - azure
  - key-vault
  - security
  - secrets
  - windows
ReleaseNotesUrl: https://github.com/$DistributionRepository/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: 1.12.0
"@

Set-Content -LiteralPath (Join-Path $wingetRoot 'HybridSolutionsCloud.VaultProspector.yaml') -Value $versionManifest -Encoding utf8NoBOM
Set-Content -LiteralPath (Join-Path $wingetRoot 'HybridSolutionsCloud.VaultProspector.installer.yaml') -Value $installerManifest -Encoding utf8NoBOM
Set-Content -LiteralPath (Join-Path $wingetRoot 'HybridSolutionsCloud.VaultProspector.locale.en-US.yaml') -Value $localeManifest -Encoding utf8NoBOM

$nuspec = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2015/06/nuspec.xsd">
  <metadata>
    <id>vault-prospector</id>
    <version>$Version</version>
    <title>Vault Prospector</title>
    <authors>Hybrid Solutions Cloud</authors>
    <owners>kristopherjturner</owners>
    <projectUrl>https://github.com/$DistributionRepository</projectUrl>
    <packageSourceUrl>https://github.com/$DistributionRepository</packageSourceUrl>
    <licenseUrl>https://github.com/$DistributionRepository/blob/main/LICENSE</licenseUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <tags>azure key-vault security secrets windows</tags>
    <summary>Secure Azure Key Vault metadata discovery and search for Windows.</summary>
    <description>Vault Prospector is a local-first Windows application for encrypted Azure Key Vault metadata discovery, offline search, and explicit Windows Hello-gated secret retrieval.</description>
    <releaseNotes>https://github.com/$DistributionRepository/releases/tag/v$Version</releaseNotes>
    <dependencies>
      <dependency id="chocolatey" version="[2.0.0,)" />
    </dependencies>
  </metadata>
  <files>
    <file src="tools\**" target="tools" />
  </files>
</package>
"@

$chocolateyInstall = @"
`$ErrorActionPreference = 'Stop'

`$packageArgs = @{
    packageName    = `$env:ChocolateyPackageName
    fileType       = 'msi'
    url64          = '$installerUrl'
    checksum64     = '$installerHash'
    checksumType64 = 'sha256'
    silentArgs     = '/qn /norestart'
    validExitCodes = @(0, 1641, 3010)
}

Install-ChocolateyPackage @packageArgs
"@

$chocolateyUninstall = @"
`$ErrorActionPreference = 'Stop'

`$packageArgs = @{
    packageName    = `$env:ChocolateyPackageName
    fileType       = 'msi'
    silentArgs     = '$productCode /qn /norestart'
    validExitCodes = @(0, 1605, 1614, 1641, 3010)
}

Uninstall-ChocolateyPackage @packageArgs
"@

$verification = @"
VERIFICATION

The MSI is downloaded from the publisher's public GitHub release at:
$installerUrl

SHA-256:
$installerHash

The public release also contains the matching checksum file, SPDX SBOM, and Sigstore bundle.
"@

Set-Content -LiteralPath (Join-Path $chocolateyRoot 'vault-prospector.nuspec') -Value $nuspec -Encoding utf8NoBOM
Set-Content -LiteralPath (Join-Path $chocolateyTools 'chocolateyInstall.ps1') -Value $chocolateyInstall -Encoding utf8NoBOM
Set-Content -LiteralPath (Join-Path $chocolateyTools 'chocolateyUninstall.ps1') -Value $chocolateyUninstall -Encoding utf8NoBOM
Set-Content -LiteralPath (Join-Path $chocolateyTools 'VERIFICATION.txt') -Value $verification -Encoding utf8NoBOM

if (Test-Path -LiteralPath $wingetArchive) {
    Remove-Item -LiteralPath $wingetArchive -Force
}
Compress-Archive -Path (Join-Path $wingetRoot '*') -DestinationPath $wingetArchive -CompressionLevel Optimal
$wingetHash = (Get-FileHash -LiteralPath $wingetArchive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$wingetArchive.sha256" -Value "$wingetHash  $(Split-Path -Leaf $wingetArchive)" -Encoding utf8NoBOM

if (-not $SkipChocolateyPack) {
    $choco = Get-Command 'choco' -ErrorAction SilentlyContinue
    if ($null -eq $choco) {
        throw 'Chocolatey CLI is required to build the .nupkg. Use -SkipChocolateyPack only for manifest generation.'
    }

    if (Test-Path -LiteralPath $chocolateyPackage) {
        Remove-Item -LiteralPath $chocolateyPackage -Force
    }

    & $choco.Source pack (Join-Path $chocolateyRoot 'vault-prospector.nuspec') --output-directory $outputRoot --yes
    if ($LASTEXITCODE -ne 0) {
        throw "choco pack failed with exit code $LASTEXITCODE."
    }

    $chocolateyHash = (Get-FileHash -LiteralPath $chocolateyPackage -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$chocolateyPackage.sha256" -Value "$chocolateyHash  $(Split-Path -Leaf $chocolateyPackage)" -Encoding utf8NoBOM
}

Write-Output $distributionRoot
