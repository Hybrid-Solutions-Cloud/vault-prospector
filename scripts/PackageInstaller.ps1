#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.1-preview.1',

    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',

    [ValidatePattern('^[0-9A-Za-z._-]+$')]
    [string]$OutputDirectory = 'artifacts',

    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot $OutputDirectory
$publishDirectory = Join-Path $outputRoot "publish-$Runtime"
$installerOutput = Join-Path $outputRoot 'installer'
$installerPath = Join-Path $outputRoot "VaultProspector-$Version-$Runtime.msi"
$installerProject = Join-Path $repoRoot 'installer/VaultProspector.Installer.wixproj'

function Invoke-Native {
    param(
        [Parameter(Mandatory)] [string] $Command,
        [Parameter(Mandatory)] [string[]] $Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

function Get-MsiVersion {
    param([Parameter(Mandatory)] [string] $SemanticVersion)

    $match = [regex]::Match($SemanticVersion, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<label>[0-9A-Za-z.-]+))?')
    if (-not $match.Success) {
        throw "Version '$SemanticVersion' is not valid semantic versioning."
    }

    $major = [int]$match.Groups['major'].Value
    $minor = [int]$match.Groups['minor'].Value
    $patch = [int]$match.Groups['patch'].Value
    $label = $match.Groups['label'].Value
    $build = ($patch * 100) + 99

    if ($label) {
        $sequenceMatch = [regex]::Match($label, '(?:^|\.)(?<sequence>\d+)$')
        if (-not $sequenceMatch.Success) {
            throw "Prerelease version '$SemanticVersion' must end in a numeric sequence for MSI ordering."
        }

        $build = ($patch * 100) + [int]$sequenceMatch.Groups['sequence'].Value
    }

    if ($major -gt 255 -or $minor -gt 255 -or $build -gt 65535) {
        throw "Version '$SemanticVersion' exceeds Windows Installer version limits."
    }

    return "$major.$minor.$build"
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot 'Package.ps1') -Version $Version -Runtime $Runtime -OutputDirectory $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Application packaging failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory 'VaultProspector.App.exe'))) {
    throw "Published application was not found at '$publishDirectory'."
}

if (Test-Path -LiteralPath $installerOutput) {
    Remove-Item -LiteralPath $installerOutput -Recurse -Force
}

$msiVersion = Get-MsiVersion -SemanticVersion $Version
Invoke-Native -Command 'dotnet' -Arguments @(
    'build',
    $installerProject,
    '--configuration', 'Release',
    '--no-incremental',
    '--output', $installerOutput,
    "-p:ReleaseVersion=$Version",
    "-p:MsiVersion=$msiVersion",
    "-p:PublishDirectory=$publishDirectory"
)

$builtInstallers = @(Get-ChildItem -LiteralPath $installerOutput -File -Filter '*.msi')
if ($builtInstallers.Count -ne 1) {
    throw "Expected exactly one MSI in '$installerOutput' but found $($builtInstallers.Count)."
}

$builtInstaller = $builtInstallers[0]
Copy-Item -LiteralPath $builtInstaller.FullName -Destination $installerPath -Force
$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$installerPath.sha256" -Value "$hash  $(Split-Path -Leaf $installerPath)" -Encoding utf8NoBOM

Write-Output $installerPath
