#Requires -Version 7.0

[CmdletBinding()]
param(
    [string]$ManifestPath = 'ops/operational-readiness.json',

    [string]$OutputPath = 'artifacts/operational-readiness/report.json',

    [switch]$CheckPublicEndpoints,

    [DateTimeOffset]$AsOfUtc = [DateTimeOffset]::UtcNow
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
}

$resolvedManifestPath = Resolve-RepositoryPath $ManifestPath
$resolvedOutputPath = Resolve-RepositoryPath $OutputPath
$findings = [System.Collections.Generic.List[object]]::new()
$checks = [System.Collections.Generic.List[object]]::new()
$endpointResults = [System.Collections.Generic.List[object]]::new()

function Add-Finding {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('error', 'warning')]
        [string]$Severity,

        [Parameter(Mandatory)]
        [string]$Code,

        [Parameter(Mandatory)]
        [string]$Message
    )

    $findings.Add([ordered]@{
        severity = $Severity
        code = $Code
        message = $Message
    })
}

function Add-Check {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [bool]$Passed,

        [Parameter(Mandatory)]
        [string]$Detail,

        [string]$FailureCode = 'OPERATIONAL_CONTRACT'
    )

    $checks.Add([ordered]@{
        name = $Name
        passed = $Passed
        detail = $Detail
    })

    if (-not $Passed) {
        Add-Finding -Severity error -Code $FailureCode -Message "$Name`: $Detail"
    }
}

function Test-NonEmptyValue {
    param([object]$Value)

    return $null -ne $Value -and -not [string]::IsNullOrWhiteSpace([string]$Value)
}

if (-not (Test-Path -LiteralPath $resolvedManifestPath -PathType Leaf)) {
    throw "Operational-readiness manifest not found: $resolvedManifestPath"
}

$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json

Add-Check -Name 'Manifest schema' -Passed ($manifest.schemaVersion -eq 1) `
    -Detail 'schemaVersion must equal 1.'
Add-Check -Name 'Lifecycle stage' -Passed ($manifest.lifecycleStage -eq 'preview') `
    -Detail 'The current lifecycleStage must be preview.'

foreach ($document in $manifest.documents.PSObject.Properties) {
    $documentPath = Resolve-RepositoryPath ([string]$document.Value)
    Add-Check -Name "Document: $($document.Name)" -Passed (Test-Path -LiteralPath $documentPath -PathType Leaf) `
        -Detail "Required document '$($document.Value)' must exist."
}

$requiredRoles = @('release', 'support', 'security')
foreach ($role in $requiredRoles) {
    $matchingOwners = @($manifest.owners | Where-Object { $_.role -eq $role })
    $validOwner = $matchingOwners.Count -eq 1 -and
        (Test-NonEmptyValue $matchingOwners[0].name) -and
        (Test-NonEmptyValue $matchingOwners[0].organization) -and
        (Test-NonEmptyValue $matchingOwners[0].channel)
    Add-Check -Name "Named $role owner" -Passed $validOwner `
        -Detail "Exactly one $role owner with a name, organization, and channel is required."
}

$targets = $manifest.serviceTargets
$targetsValid = $targets.contractualSla -eq $false -and
    $targets.previewQueueReviewBusinessDays -gt 0 -and
    $targets.previewClassificationBusinessDays -gt 0 -and
    $targets.securityAcknowledgementBusinessDays -gt 0 -and
    $targets.securityInitialAssessmentBusinessDays -gt 0
Add-Check -Name 'Non-contractual response targets' -Passed $targetsValid `
    -Detail 'All Preview response targets must be positive and contractualSla must remain false.'

foreach ($cadence in $manifest.cadences.PSObject.Properties) {
    $cadenceValid = $true
    try {
        $duration = [System.Xml.XmlConvert]::ToTimeSpan([string]$cadence.Value)
        $cadenceValid = $duration -gt [TimeSpan]::Zero
    }
    catch {
        $cadenceValid = $false
    }
    Add-Check -Name "Cadence: $($cadence.Name)" -Passed $cadenceValid `
        -Detail "Cadence '$($cadence.Value)' must be a positive ISO-8601 duration."
}

$release = $manifest.supportedRelease
$supportedVersions = @($release.supportedVersions)
$releaseValid = $release.channel -eq 'preview' -and
    (Test-NonEmptyValue $release.currentVersion) -and
    $supportedVersions.Count -eq 1 -and
    $supportedVersions[0] -eq $release.currentVersion -and
    $release.productionSupported -eq $false
Add-Check -Name 'Preview support declaration' -Passed $releaseValid `
    -Detail 'Exactly the current Preview must be supported and productionSupported must be false.'

$releaseScope = Get-Content -LiteralPath (Resolve-RepositoryPath 'docs/product/release-scope.md') -Raw
$securityPolicy = Get-Content -LiteralPath (Resolve-RepositoryPath 'SECURITY.md') -Raw
$runbook = Get-Content -LiteralPath (Resolve-RepositoryPath 'docs/release-operations-runbook.md') -Raw
Add-Check -Name 'Release-scope version agreement' `
    -Passed ($releaseScope.Contains([string]$release.currentVersion)) `
    -Detail "release-scope.md must name current version '$($release.currentVersion)'."
Add-Check -Name 'Security support boundary' `
    -Passed ($securityPolicy.Contains('latest published Preview') -and $securityPolicy.Contains('No version is supported for production use')) `
    -Detail 'SECURITY.md must retain latest-Preview-only and no-production-support boundaries.'
Add-Check -Name 'Incident and rotation runbook' `
    -Passed ($runbook.Contains('## Security incident procedure') -and $runbook.Contains('## Credential rotation')) `
    -Detail 'The operations runbook must include incident and credential-rotation procedures.'

$dependabotPath = Resolve-RepositoryPath '.github/dependabot.yml'
$dependabotExists = Test-Path -LiteralPath $dependabotPath -PathType Leaf
Add-Check -Name 'Dependabot configuration' -Passed $dependabotExists `
    -Detail '.github/dependabot.yml must exist.'
if ($dependabotExists) {
    $dependabot = Get-Content -LiteralPath $dependabotPath -Raw
    foreach ($automation in @($manifest.dependencyAutomation)) {
        $ecosystemMarker = "package-ecosystem: $($automation.ecosystem)"
        $directoryMarker = if ($automation.directory -eq '/') {
            'directory: /'
        }
        else {
            "directory: $($automation.directory)"
        }
        Add-Check -Name "Dependency automation: $($automation.ecosystem) $($automation.directory)" `
            -Passed ($dependabot.Contains($ecosystemMarker) -and $dependabot.Contains($directoryMarker)) `
            -Detail "Dependabot must contain '$ecosystemMarker' and '$directoryMarker'."
    }
}

$monitorWorkflowPath = Resolve-RepositoryPath '.ado/operational-readiness.yml'
$monitorWorkflowExists = Test-Path -LiteralPath $monitorWorkflowPath -PathType Leaf
Add-Check -Name 'Scheduled operational monitor' -Passed $monitorWorkflowExists `
    -Detail '.ado/operational-readiness.yml must exist.'
if ($monitorWorkflowExists) {
    $monitorWorkflow = Get-Content -LiteralPath $monitorWorkflowPath -Raw
    $workflowValid = $monitorWorkflow.Contains('schedules:') -and
        $monitorWorkflow.Contains('trigger: none') -and
        $monitorWorkflow.Contains('Test-OperationalReadiness.ps1') -and
        $monitorWorkflow.Contains('-CheckPublicEndpoints') -and
        $monitorWorkflow.Contains('Test-VulnerablePackages.ps1') -and
        $monitorWorkflow.Contains('publish: artifacts/operational-readiness/report.json')
    Add-Check -Name 'Operational monitor contract' -Passed $workflowValid `
        -Detail 'The monitor must be scheduled/manual, test public endpoints and vulnerabilities, and retain a report.'
}

foreach ($runtime in @($manifest.runtimeDependencies)) {
    $versionFilePath = Resolve-RepositoryPath ([string]$runtime.versionFile)
    $versionFileExists = Test-Path -LiteralPath $versionFilePath -PathType Leaf
    Add-Check -Name "Runtime version file: $($runtime.name)" -Passed $versionFileExists `
        -Detail "Runtime version file '$($runtime.versionFile)' must exist."
    if (-not $versionFileExists) { continue }

    $versionManifest = Get-Content -LiteralPath $versionFilePath -Raw | ConvertFrom-Json
    $sdkVersion = [version]$versionManifest.sdk.version
    Add-Check -Name "Runtime major: $($runtime.name)" -Passed ($sdkVersion.Major -eq $runtime.expectedMajor) `
        -Detail "Expected major $($runtime.expectedMajor), observed SDK '$sdkVersion'."

    $endOfSupport = [DateTimeOffset]::ParseExact(
        [string]$runtime.endOfSupportDate,
        'yyyy-MM-dd',
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AssumeUniversal)
    $daysRemaining = [Math]::Floor(($endOfSupport - $AsOfUtc.ToUniversalTime()).TotalDays)
    if ($daysRemaining -lt 0) {
        Add-Finding -Severity error -Code 'RUNTIME_END_OF_SUPPORT' `
            -Message "$($runtime.name) support ended on $($runtime.endOfSupportDate)."
    }
    elseif ($daysRemaining -le 120) {
        Add-Finding -Severity warning -Code 'RUNTIME_SUPPORT_WINDOW' `
            -Message "$($runtime.name) reaches end of support on $($runtime.endOfSupportDate) ($daysRemaining days remaining)."
    }
}

foreach ($control in @($manifest.credentialAndSigningControls)) {
    $controlValid = (Test-NonEmptyValue $control.name) -and
        (Test-NonEmptyValue $control.rotation) -and
        @('documented', 'implemented', 'blocked').Contains([string]$control.status)
    Add-Check -Name "Credential/signing control: $($control.name)" -Passed $controlValid `
        -Detail 'Every control requires a name, rotation rule, and documented/implemented/blocked status.'
}

if ($CheckPublicEndpoints) {
    foreach ($monitor in @($manifest.publicMonitors)) {
        $endpoint = [uri]$monitor.url
        $endpointValid = $endpoint.Scheme -eq 'https'
        if (-not $endpointValid) {
            Add-Finding -Severity error -Code 'PUBLIC_ENDPOINT_SCHEME' `
                -Message "$($monitor.name) must use HTTPS."
            continue
        }

        try {
            $response = Invoke-WebRequest -Uri $endpoint -Method Get -MaximumRedirection 5 `
                -TimeoutSec 30 -Headers @{ 'User-Agent' = 'VaultProspector-OperationalReadiness/1.0' }
            $passed = $response.StatusCode -ge 200 -and $response.StatusCode -lt 400
            $endpointResults.Add([ordered]@{
                name = $monitor.name
                url = $monitor.url
                statusCode = $response.StatusCode
                passed = $passed
            })
            if (-not $passed) {
                Add-Finding -Severity error -Code 'PUBLIC_ENDPOINT_STATUS' `
                    -Message "$($monitor.name) returned HTTP $($response.StatusCode)."
            }
        }
        catch {
            $endpointResults.Add([ordered]@{
                name = $monitor.name
                url = $monitor.url
                statusCode = $null
                passed = $false
            })
            Add-Finding -Severity error -Code 'PUBLIC_ENDPOINT_UNREACHABLE' `
                -Message "$($monitor.name) could not be reached: $($_.Exception.Message)"
        }
    }
}

$sourceCommit = ''
try {
    $sourceCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
}
catch {
    $sourceCommit = ''
}

$errors = @($findings | Where-Object { $_.severity -eq 'error' })
$warnings = @($findings | Where-Object { $_.severity -eq 'warning' })
$report = [ordered]@{
    schemaVersion = 1
    status = if ($errors.Count -eq 0) { 'passed' } else { 'failed' }
    observedAtUtc = $AsOfUtc.ToUniversalTime().ToString('O')
    sourceCommit = $sourceCommit
    manifest = $ManifestPath
    publicEndpointsChecked = [bool]$CheckPublicEndpoints
    currentSupportedVersion = $release.currentVersion
    checks = @($checks)
    publicEndpoints = @($endpointResults)
    findings = @($findings)
    summary = [ordered]@{
        passedChecks = @($checks | Where-Object { $_.passed }).Count
        failedChecks = @($checks | Where-Object { -not $_.passed }).Count
        warnings = $warnings.Count
        errors = $errors.Count
        declaredOpenEvidence = @($manifest.openEvidence).Count
    }
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8NoBOM

Write-Host "Operational-readiness report: $resolvedOutputPath"
Write-Host "Checks: $($report.summary.passedChecks) passed, $($report.summary.failedChecks) failed; findings: $($warnings.Count) warning(s), $($errors.Count) error(s)."
foreach ($warning in $warnings) {
    Write-Warning "[$($warning.code)] $($warning.message)"
}

if ($errors.Count -gt 0) {
    foreach ($errorFinding in $errors) {
        Write-Error "[$($errorFinding.code)] $($errorFinding.message)" -ErrorAction Continue
    }
    throw "Operational-readiness validation failed with $($errors.Count) error(s)."
}
