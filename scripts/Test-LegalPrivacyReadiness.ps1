#Requires -Version 7.0

[CmdletBinding()]
param(
    [string]$OutputPath = 'artifacts/legal-privacy/report.json',

    [string]$PublishDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$checks = [System.Collections.Generic.List[object]]::new()
$errors = [System.Collections.Generic.List[object]]::new()

function Resolve-RepositoryPath {
    param([Parameter(Mandatory)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
}

function Add-Check {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][bool]$Passed,
        [Parameter(Mandatory)][string]$Detail
    )

    $checks.Add([ordered]@{ name = $Name; passed = $Passed; detail = $Detail })
    if (-not $Passed) {
        $errors.Add([ordered]@{ code = 'LEGAL_PRIVACY_CONTRACT'; message = "$Name`: $Detail" })
    }
}

$requiredFiles = @(
    'LICENSE'
    'THIRD-PARTY-NOTICES.md'
    'docs/legal/third-party-components.json'
    'docs/legal/license-overrides.json'
    'docs/legal/legal-privacy-review.md'
    'docs/legal/package-and-store-metadata.md'
    'docs/privacy.md'
    'SECURITY.md'
)
foreach ($file in $requiredFiles) {
    Add-Check -Name "Required file: $file" `
        -Passed (Test-Path -LiteralPath (Resolve-RepositoryPath $file) -PathType Leaf) `
        -Detail "Required legal/privacy source '$file' must exist."
}

& (Resolve-RepositoryPath 'scripts/Update-ThirdPartyNotices.ps1') -Check
Add-Check -Name 'Third-party inventory drift' -Passed ($LASTEXITCODE -eq 0) `
    -Detail 'Inventory and notices must match every committed lock file.'

$inventory = Get-Content -LiteralPath (Resolve-RepositoryPath 'docs/legal/third-party-components.json') -Raw | ConvertFrom-Json
$inventoryValid = $inventory.schemaVersion -eq 1 -and @($inventory.packages).Count -gt 0
Add-Check -Name 'Third-party inventory schema' -Passed $inventoryValid `
    -Detail 'Inventory schemaVersion must equal 1 and contain package records.'
$unreviewed = @($inventory.packages | Where-Object { $_.license -eq 'NOASSERTION' })
$unreviewedDeclared = $unreviewed.Count -eq 1 -and
    $unreviewed[0].id -eq 'AvaloniaUI.DiagnosticsSupport' -and
    $unreviewed[0].distribution -eq 'development-only' -and
    $unreviewed[0].reviewStatus -eq 'approval-required'
Add-Check -Name 'Unknown-license declaration' -Passed $unreviewedDeclared `
    -Detail 'The only NOASSERTION item must be the Release-excluded diagnostics package with approval required.'

$privacy = Get-Content -LiteralPath (Resolve-RepositoryPath 'docs/privacy.md') -Raw
$privacyMarkers = @(
    'Project-controlled telemetry is disabled'
    'Default retention'
    'Removal and device migration'
    'Mobile applications'
    'There is no automatic age or size deletion policy'
)
foreach ($marker in $privacyMarkers) {
    Add-Check -Name "Privacy disclosure: $marker" -Passed $privacy.Contains($marker) `
        -Detail "docs/privacy.md must retain '$marker'."
}

$productionProjects = @(
    Get-ChildItem -LiteralPath (Resolve-RepositoryPath 'src') -Recurse -Filter *.csproj -File
    Get-ChildItem -LiteralPath (Resolve-RepositoryPath 'mobile') -Recurse -Filter *.csproj -File |
        Where-Object { $_.FullName -notmatch 'Mobile\.Tests' }
)
$telemetryPatterns = 'ApplicationInsights|OpenTelemetry|Sentry|AppCenter'
$telemetryReferences = @(
    foreach ($project in $productionProjects) {
        Select-String -LiteralPath $project.FullName -Pattern $telemetryPatterns
    }
)
Add-Check -Name 'No production telemetry package' -Passed ($telemetryReferences.Count -eq 0) `
    -Detail 'Production project files must not reference Application Insights, OpenTelemetry, Sentry, or App Center.'

$iosPrivacy = Get-Content -LiteralPath (Resolve-RepositoryPath 'mobile/VaultProspector.Mobile.iOS/PrivacyInfo.xcprivacy') -Raw
Add-Check -Name 'iOS privacy baseline' `
    -Passed ($iosPrivacy.Contains('<key>NSPrivacyTracking</key>') -and
        $iosPrivacy.Contains('<key>NSPrivacyCollectedDataTypes</key>')) `
    -Detail 'The embedded iOS privacy manifest must declare tracking and collected-data keys.'

$androidManifest = Get-Content -LiteralPath (Resolve-RepositoryPath 'mobile/VaultProspector.Mobile.Android/Properties/AndroidManifest.xml') -Raw
Add-Check -Name 'Android privacy defaults' `
    -Passed ($androidManifest.Contains('android:allowBackup="false"') -and
        $androidManifest.Contains('android:usesCleartextTraffic="false"')) `
    -Detail 'Android backup and cleartext traffic must remain disabled.'

$packageScript = Get-Content -LiteralPath (Resolve-RepositoryPath 'scripts/Package.ps1') -Raw
foreach ($marker in @('LICENSE.txt', 'THIRD-PARTY-NOTICES.md', 'PRIVACY.md', 'Update-ThirdPartyNotices.ps1')) {
    Add-Check -Name "Package disclosure: $marker" -Passed $packageScript.Contains($marker) `
        -Detail "Package.ps1 must validate/copy '$marker'."
}

$distributionScript = Get-Content -LiteralPath (Resolve-RepositoryPath 'scripts/PackageDistribution.ps1') -Raw
$distributionValid = $distributionScript.Contains('License: MIT') -and
    $distributionScript.Contains('Publisher: Hybrid Solutions Cloud') -and
    $distributionScript.Contains('PackageIdentifier: HybridSolutionsCloud.VaultProspector')
Add-Check -Name 'Windows package metadata' -Passed $distributionValid `
    -Detail 'WinGet/Chocolatey source must retain publisher, identifier, and MIT license metadata.'

$review = Get-Content -LiteralPath (Resolve-RepositoryPath 'docs/legal/legal-privacy-review.md') -Raw
$reviewTruthful = $review.Contains('Status:** In progress') -and
    $review.Contains('L-01') -and $review.Contains('L-07') -and
    $review.Contains('Automated checks prove source consistency')
Add-Check -Name 'Approval truth boundary' -Passed $reviewTruthful `
    -Detail 'The review must remain In progress and disclose every L-01 through L-07 approval gap.'

if (-not [string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $resolvedPublishDirectory = Resolve-RepositoryPath $PublishDirectory
    foreach ($file in @('LICENSE.txt', 'THIRD-PARTY-NOTICES.md', 'PRIVACY.md')) {
        Add-Check -Name "Published disclosure: $file" `
            -Passed (Test-Path -LiteralPath (Join-Path $resolvedPublishDirectory $file) -PathType Leaf) `
            -Detail "Published payload must contain '$file'."
    }
    if (Test-Path -LiteralPath (Join-Path $resolvedPublishDirectory 'THIRD-PARTY-NOTICES.md')) {
        $sourceHash = (Get-FileHash -LiteralPath (Resolve-RepositoryPath 'THIRD-PARTY-NOTICES.md') -Algorithm SHA256).Hash
        $publishedHash = (Get-FileHash -LiteralPath (Join-Path $resolvedPublishDirectory 'THIRD-PARTY-NOTICES.md') -Algorithm SHA256).Hash
        Add-Check -Name 'Published notice integrity' -Passed ($sourceHash -eq $publishedHash) `
            -Detail 'Published third-party notices must match the committed generated file.'
    }
}

$sourceCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
$report = [ordered]@{
    schemaVersion = 1
    status = if ($errors.Count -eq 0) { 'passed' } else { 'failed' }
    observedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    sourceCommit = $sourceCommit
    checks = @($checks)
    findings = @($errors)
    summary = [ordered]@{
        passed = @($checks | Where-Object passed).Count
        failed = @($checks | Where-Object { -not $_.passed }).Count
        inventoryRecords = @($inventory.packages).Count
        approvalRequired = @($inventory.packages | Where-Object { $_.reviewStatus -eq 'approval-required' }).Count
    }
}

$resolvedOutputPath = Resolve-RepositoryPath $OutputPath
$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8NoBOM
Write-Host "Legal/privacy report: $resolvedOutputPath"
Write-Host "Checks: $($report.summary.passed) passed, $($report.summary.failed) failed; inventory: $($report.summary.inventoryRecords)."

if ($errors.Count -gt 0) {
    foreach ($finding in $errors) {
        Write-Error "[$($finding.code)] $($finding.message)" -ErrorAction Continue
    }
    throw "Legal/privacy validation failed with $($errors.Count) error(s)."
}
