#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.1-preview.1',

    [ValidatePattern('^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$')]
    [string]$DistributionRepository = 'Hybrid-Solutions-Cloud/vault-prospector-releases',

    [ValidatePattern('^[0-9A-Za-z._-]+$')]
    [string]$OutputDirectory = 'artifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
    throw 'Set GH_TOKEN to the Hybrid Solutions Cloud GitHub App installation token before publishing.'
}

$gh = Get-Command 'gh' -ErrorAction SilentlyContinue
if ($null -eq $gh) {
    throw 'GitHub CLI is required to publish distribution assets.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot $OutputDirectory
$tag = "v$Version"
$assets = @(
    Get-ChildItem -LiteralPath $outputRoot -File |
        Where-Object {
            $_.Name -like "VaultProspector-$Version-*" -or
            $_.Name -like "vault-prospector.$Version.*"
        }
)

if ($assets.Count -eq 0) {
    throw "No version $Version distribution assets were found in '$outputRoot'."
}

$visibility = & $gh.Source repo view $DistributionRepository --json visibility --jq '.visibility'
if ($LASTEXITCODE -ne 0) {
    throw "Distribution repository '$DistributionRepository' is unavailable to the GitHub App token."
}
if ($visibility.Trim() -ne 'PUBLIC') {
    throw "Distribution repository '$DistributionRepository' must be public for WinGet and Chocolatey downloads."
}

& $gh.Source release view $tag --repo $DistributionRepository | Out-Null
if ($LASTEXITCODE -eq 0) {
    throw "Distribution release '$tag' already exists. Immutable releases and assets must never be replaced; publish a new version."
}

$releaseArguments = @(
    'release', 'create', $tag,
    '--repo', $DistributionRepository,
    '--title', "Vault Prospector $Version",
    '--notes', "Windows x64 installer and package-manager artifacts for Vault Prospector $Version."
)
if ($Version.Contains('-')) {
    $releaseArguments += '--prerelease'
}
$releaseArguments += @($assets.FullName)

& $gh.Source @releaseArguments
if ($LASTEXITCODE -ne 0) {
    throw "Creating distribution release '$tag' and uploading its assets failed with exit code $LASTEXITCODE."
}

Write-Output "https://github.com/$DistributionRepository/releases/tag/$tag"
