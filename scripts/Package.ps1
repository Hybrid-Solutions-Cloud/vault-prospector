#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',

    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',

    [ValidatePattern('^[0-9A-Za-z._-]+$')]
    [string]$OutputDirectory = 'artifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot $OutputDirectory
$publishDirectory = Join-Path $outputRoot "publish-$Runtime"
$archivePath = Join-Path $outputRoot "VaultProspector-$Version-$Runtime.zip"

function Invoke-DotNet {
    param([Parameter(Mandatory)] [string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

Invoke-DotNet -Arguments @(
    'publish',
    (Join-Path $repoRoot 'src/VaultProspector.App/VaultProspector.App.csproj'),
    '--configuration', 'Release',
    '--runtime', $Runtime,
    '--self-contained', 'true',
    '--output', $publishDirectory,
    "-p:Version=$Version",
    '-p:VaultProspectorPackaging=true',
    '-p:RestoreLockedMode=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

Get-ChildItem -LiteralPath $publishDirectory -Recurse -File -Filter '*.pdb' | Remove-Item -Force

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$archivePath.sha256" -Value "$hash  $(Split-Path -Leaf $archivePath)" -Encoding utf8NoBOM

Write-Output $archivePath
