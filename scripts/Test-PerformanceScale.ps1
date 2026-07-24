#Requires -Version 7.0

[CmdletBinding()]
param(
    [string]$OutputPath = './artifacts/performance-scale.json',

    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'tools/VaultProspector.PerformanceProbe/VaultProspector.PerformanceProbe.csproj'
$resolvedOutput = [System.IO.Path]::GetFullPath(
    $OutputPath,
    (Get-Location).Path)
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to resolve the source commit for performance evidence.'
}

if (-not $NoBuild) {
    & dotnet restore $projectPath --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw 'Locked restore failed for the performance probe.'
    }

    & dotnet build $projectPath --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Release build failed for the performance probe.'
    }
}

& dotnet run `
    --project $projectPath `
    --configuration Release `
    --no-build `
    --no-restore `
    -- `
    --output $resolvedOutput `
    --commit $sourceCommit
if ($LASTEXITCODE -ne 0) {
    throw "Performance and scale probe failed with exit code $LASTEXITCODE."
}

Write-Host "Performance evidence: $resolvedOutput"
