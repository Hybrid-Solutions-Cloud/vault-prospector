#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',

    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',

    [ValidatePattern('^[0-9A-Za-z._-]+$')]
    [string]$OutputDirectory = 'artifacts',

    [ValidatePattern('^[A-Za-z0-9.-]{3,50}$')]
    [string]$IdentityName = 'HybridSolutionsCloud.VaultProspector',

    [string]$Publisher = 'CN=Hybrid Solutions Cloud',

    [string]$PublisherDisplayName = 'Hybrid Solutions Cloud',

    [string]$DisplayName = 'Vault Prospector',

    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot $OutputDirectory
$publishDirectory = Join-Path $outputRoot "publish-$Runtime"
$stagingDirectory = Join-Path $outputRoot "msix-staging-$Runtime"
$packagePath = Join-Path $outputRoot "VaultProspector-$Version-$Runtime.msix"
$templatePath = Join-Path $repoRoot 'packaging/msix/AppxManifest.xml.template'
$sourceLogo = Join-Path $repoRoot 'src/VaultProspector.App/Assets/vault-prospector.png'
$sdkPackageVersion = '10.0.28000.2270'
$sdkPackageHash = 'd939fa052f9c80f878b2a28b7071a6f2c9a51029018bb87a835ebda6e535a002'
$toolsRoot = Join-Path $outputRoot ".tools/windows-sdk-buildtools-$sdkPackageVersion"
$makeAppx = Join-Path $toolsRoot 'bin/10.0.28000.0/x64/makeappx.exe'

function ConvertTo-XmlText {
    param([Parameter(Mandatory)] [string]$Value)

    return [System.Security.SecurityElement]::Escape($Value)
}

function Resolve-MsixVersion {
    param([Parameter(Mandatory)] [string]$SemanticVersion)

    if ($SemanticVersion -notmatch '^(\d+)\.(\d+)\.(\d+)(?:-([^+]+))?') {
        throw "Version '$SemanticVersion' cannot be converted to an MSIX version."
    }

    $parts = @(
        [int]$Matches[1],
        [int]$Matches[2],
        [int]$Matches[3]
    )
    $revision = 0
    if ($Matches[4] -match '(\d+)(?!.*\d)') {
        $revision = [int]$Matches[1]
    }
    $parts += $revision

    if (@($parts | Where-Object { $_ -lt 0 -or $_ -gt 65535 }).Count -gt 0) {
        throw "MSIX version components must be between 0 and 65535: '$SemanticVersion'."
    }

    return $parts -join '.'
}

function New-MsixLogo {
    param(
        [Parameter(Mandatory)] [string]$Source,
        [Parameter(Mandatory)] [string]$Destination,
        [Parameter(Mandatory)] [int]$Width,
        [Parameter(Mandatory)] [int]$Height
    )

    Add-Type -AssemblyName System.Drawing.Common
    $sourceImage = [System.Drawing.Image]::FromFile($Source)
    try {
        $bitmap = [System.Drawing.Bitmap]::new($Width, $Height)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                $scale = [Math]::Min($Width / $sourceImage.Width, $Height / $sourceImage.Height)
                $drawWidth = [Math]::Max(1, [int][Math]::Round($sourceImage.Width * $scale))
                $drawHeight = [Math]::Max(1, [int][Math]::Round($sourceImage.Height * $scale))
                $x = [int](($Width - $drawWidth) / 2)
                $y = [int](($Height - $drawHeight) / 2)
                $graphics.DrawImage($sourceImage, $x, $y, $drawWidth, $drawHeight)
            }
            finally {
                $graphics.Dispose()
            }

            $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bitmap.Dispose()
        }
    }
    finally {
        $sourceImage.Dispose()
    }
}

function Install-MakeAppx {
    if (Test-Path -LiteralPath $makeAppx -PathType Leaf) {
        return
    }

    $packageFile = Join-Path $outputRoot "microsoft.windows.sdk.buildtools.$sdkPackageVersion.nupkg"
    $packageUri = "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$sdkPackageVersion/microsoft.windows.sdk.buildtools.$sdkPackageVersion.nupkg"
    Invoke-WebRequest -Uri $packageUri -OutFile $packageFile
    $actualHash = (Get-FileHash -LiteralPath $packageFile -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $sdkPackageHash) {
        throw "Microsoft.Windows.SDK.BuildTools $sdkPackageVersion did not match its pinned SHA-256."
    }

    if (Test-Path -LiteralPath $toolsRoot) {
        Remove-Item -LiteralPath $toolsRoot -Recurse -Force
    }
    [System.IO.Compression.ZipFile]::ExtractToDirectory($packageFile, $toolsRoot)
    Remove-Item -LiteralPath $packageFile -Force

    if (-not (Test-Path -LiteralPath $makeAppx -PathType Leaf)) {
        throw "MakeAppx was not found after restoring Microsoft.Windows.SDK.BuildTools $sdkPackageVersion."
    }
}

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot 'Package.ps1') `
        -Version $Version `
        -Runtime $Runtime `
        -OutputDirectory $OutputDirectory `
        -SkipArchive
    if ($LASTEXITCODE -ne 0) {
        throw "Windows application publish failed with exit code $LASTEXITCODE."
    }
}
elseif (-not (Test-Path -LiteralPath $publishDirectory -PathType Container)) {
    throw "Published application directory '$publishDirectory' does not exist."
}

Install-MakeAppx

if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $publishDirectory '*') -Destination $stagingDirectory -Recurse -Force

$assetsDirectory = Join-Path $stagingDirectory 'Assets'
New-Item -ItemType Directory -Path $assetsDirectory -Force | Out-Null
New-MsixLogo -Source $sourceLogo -Destination (Join-Path $assetsDirectory 'StoreLogo.png') -Width 50 -Height 50
New-MsixLogo -Source $sourceLogo -Destination (Join-Path $assetsDirectory 'Square44x44Logo.png') -Width 44 -Height 44
New-MsixLogo -Source $sourceLogo -Destination (Join-Path $assetsDirectory 'Square150x150Logo.png') -Width 150 -Height 150
New-MsixLogo -Source $sourceLogo -Destination (Join-Path $assetsDirectory 'Wide310x150Logo.png') -Width 310 -Height 150

$manifest = Get-Content -LiteralPath $templatePath -Raw
$manifest = $manifest.
    Replace('{{IDENTITY_NAME}}', (ConvertTo-XmlText $IdentityName)).
    Replace('{{PUBLISHER}}', (ConvertTo-XmlText $Publisher)).
    Replace('{{PACKAGE_VERSION}}', (Resolve-MsixVersion $Version)).
    Replace('{{DISPLAY_NAME}}', (ConvertTo-XmlText $DisplayName)).
    Replace('{{PUBLISHER_DISPLAY_NAME}}', (ConvertTo-XmlText $PublisherDisplayName))
Set-Content `
    -LiteralPath (Join-Path $stagingDirectory 'AppxManifest.xml') `
    -Value $manifest `
    -Encoding utf8NoBOM

if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}
& $makeAppx pack /d $stagingDirectory /p $packagePath /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx failed with exit code $LASTEXITCODE."
}

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content `
    -LiteralPath "$packagePath.sha256" `
    -Value "$hash  $(Split-Path -Leaf $packagePath)" `
    -Encoding utf8NoBOM

Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
Write-Output $packagePath
