#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0-preview.2',

    [ValidatePattern('^[0-9A-Za-z._-]+$')]
    [string]$OutputDirectory = 'artifacts',

    [switch]$SkipWinGet,

    [switch]$SkipChocolatey
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot $OutputDirectory
$manifestRoot = Join-Path $outputRoot "distribution/winget/HybridSolutionsCloud.VaultProspector/$Version"
$chocolateyPackage = Join-Path $outputRoot "vault-prospector.$Version.nupkg"

if (-not $SkipWinGet) {
    if ([string]::IsNullOrWhiteSpace($env:WINGET_GITHUB_TOKEN)) {
        throw 'Set WINGET_GITHUB_TOKEN to a GitHub token authorized to open a pull request in microsoft/winget-pkgs.'
    }

    $wingetCreate = Get-Command 'wingetcreate' -ErrorAction SilentlyContinue
    if ($null -eq $wingetCreate) {
        throw 'WinGet Manifest Creator is required. Install it with: winget install Microsoft.WingetCreate'
    }
    if (-not (Test-Path -LiteralPath $manifestRoot)) {
        throw "WinGet manifests were not found at '$manifestRoot'."
    }

    & $wingetCreate.Source submit --token $env:WINGET_GITHUB_TOKEN $manifestRoot
    if ($LASTEXITCODE -ne 0) {
        throw "WinGet submission failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipChocolatey) {
    if ([string]::IsNullOrWhiteSpace($env:CHOCOLATEY_API_KEY)) {
        throw 'Set CHOCOLATEY_API_KEY to the Chocolatey Community Repository API key before submission.'
    }

    $choco = Get-Command 'choco' -ErrorAction SilentlyContinue
    if ($null -eq $choco) {
        throw 'Chocolatey CLI is required to submit the package.'
    }
    if (-not (Test-Path -LiteralPath $chocolateyPackage)) {
        throw "Chocolatey package was not found at '$chocolateyPackage'."
    }

    & $choco.Source push $chocolateyPackage --source 'https://push.chocolatey.org/' --api-key $env:CHOCOLATEY_API_KEY
    if ($LASTEXITCODE -ne 0) {
        throw "Chocolatey submission failed with exit code $LASTEXITCODE."
    }
}
