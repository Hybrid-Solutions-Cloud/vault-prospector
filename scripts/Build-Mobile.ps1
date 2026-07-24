#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidateSet('All', 'Managed', 'Android', 'iOS')]
    [string]$Platform = 'All',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$AndroidSdkDirectory,

    [string]$JavaSdkDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$mobileRoot = Join-Path $repositoryRoot 'mobile'
$vulnerabilityScript = Join-Path $PSScriptRoot 'Test-VulnerablePackages.ps1'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-AndroidProperties {
    $properties = @()
    if (-not [string]::IsNullOrWhiteSpace($AndroidSdkDirectory)) {
        $resolvedAndroidSdk = (Resolve-Path -LiteralPath $AndroidSdkDirectory).Path
        $properties += "-p:AndroidSdkDirectory=$resolvedAndroidSdk"
    }
    if (-not [string]::IsNullOrWhiteSpace($JavaSdkDirectory)) {
        $resolvedJavaSdk = (Resolve-Path -LiteralPath $JavaSdkDirectory).Path
        $properties += "-p:JavaSdkDirectory=$resolvedJavaSdk"
    }
    return $properties
}

Push-Location $mobileRoot
try {
    if ($Platform -in @('All', 'Managed')) {
        Invoke-DotNet @(
            'restore',
            'VaultProspector.Mobile.Tests/VaultProspector.Mobile.Tests.csproj',
            '--locked-mode'
        )
        Invoke-DotNet @(
            'format',
            'VaultProspector.Mobile.Tests/VaultProspector.Mobile.Tests.csproj',
            '--verify-no-changes',
            '--no-restore'
        )
        & $vulnerabilityScript `
            -Solution 'VaultProspector.Mobile.Tests/VaultProspector.Mobile.Tests.csproj'
        Invoke-DotNet @(
            'test',
            'VaultProspector.Mobile.Tests/VaultProspector.Mobile.Tests.csproj',
            '--configuration',
            $Configuration,
            '--no-restore'
        )
    }

    if ($Platform -in @('All', 'Android')) {
        $androidProperties = @(Get-AndroidProperties)
        Invoke-DotNet (@(
            'restore',
            'VaultProspector.Mobile.Android/VaultProspector.Mobile.Android.csproj',
            '--locked-mode'
        ) + $androidProperties)
        & $vulnerabilityScript `
            -Solution 'VaultProspector.Mobile.Android/VaultProspector.Mobile.Android.csproj'
        Invoke-DotNet (@(
            'build',
            'VaultProspector.Mobile.Android/VaultProspector.Mobile.Android.csproj',
            '--configuration',
            $Configuration,
            '--no-restore'
        ) + $androidProperties)
    }

    if ($Platform -in @('All', 'iOS')) {
        $iosRuntimeIdentifier = if ($IsMacOS) {
            switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
                ([System.Runtime.InteropServices.Architecture]::Arm64) {
                    'iossimulator-arm64'
                }
                ([System.Runtime.InteropServices.Architecture]::X64) {
                    'iossimulator-x64'
                }
                default {
                    throw "Unsupported macOS build architecture '$($_)'."
                }
            }
        }
        else {
            'ios-arm64'
        }
        $iosProperties = @("-p:RuntimeIdentifier=$iosRuntimeIdentifier")

        Invoke-DotNet (@(
            'restore',
            'VaultProspector.Mobile.iOS/VaultProspector.Mobile.iOS.csproj',
            '--locked-mode'
        ))
        & $vulnerabilityScript `
            -Solution 'VaultProspector.Mobile.iOS/VaultProspector.Mobile.iOS.csproj'
        if ($IsMacOS) {
            Invoke-DotNet (@(
                'build',
                'VaultProspector.Mobile.iOS/VaultProspector.Mobile.iOS.csproj',
                '--configuration',
                $Configuration,
                '--no-restore',
                '-p:EnableCodeSigning=false'
            ) + $iosProperties)
        }
        else {
            Invoke-DotNet (@(
                'msbuild',
                'VaultProspector.Mobile.iOS/VaultProspector.Mobile.iOS.csproj',
                '-t:Compile',
                "-p:Configuration=$Configuration"
            ) + $iosProperties)
        }
    }
}
finally {
    Pop-Location
}
