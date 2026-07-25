#Requires -Version 7.0

[CmdletBinding()]
param(
    [string]$OutputPath = 'artifacts/enterprise-policy/report.json',

    [string]$PublishDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$checks = [System.Collections.Generic.List[object]]::new()
$findings = [System.Collections.Generic.List[object]]::new()

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

    $checks.Add([ordered]@{
        name = $Name
        passed = $Passed
        detail = $Detail
    })
    if (-not $Passed) {
        $findings.Add([ordered]@{
            code = 'ENTERPRISE_POLICY_CONTRACT'
            message = "$Name`: $Detail"
        })
    }
}

$requiredFiles = @(
    'docs/enterprise-policy.md'
    'policy/VaultProspector.admx'
    'policy/en-US/VaultProspector.adml'
    'src/VaultProspector.Application/EnterprisePolicy.cs'
    'src/VaultProspector.Platform/WindowsRegistryEnterprisePolicy.cs'
    'tests/VaultProspector.Platform.Tests/WindowsRegistryEnterprisePolicyTests.cs'
)
foreach ($file in $requiredFiles) {
    Add-Check -Name "Required source: $file" `
        -Passed (Test-Path -LiteralPath (Resolve-RepositoryPath $file) -PathType Leaf) `
        -Detail "Enterprise policy source '$file' must exist."
}

$admxPath = Resolve-RepositoryPath 'policy/VaultProspector.admx'
$admlPath = Resolve-RepositoryPath 'policy/en-US/VaultProspector.adml'
try {
    [xml]$admx = Get-Content -LiteralPath $admxPath -Raw
    [xml]$adml = Get-Content -LiteralPath $admlPath -Raw
    Add-Check -Name 'ADMX XML' -Passed ($null -ne $admx.SelectSingleNode("//*[local-name()='policy']")) `
        -Detail 'The ADMX file must parse and contain a machine policy.'
    Add-Check -Name 'ADML XML' -Passed ($null -ne $adml.SelectSingleNode("//*[local-name()='presentationTable']")) `
        -Detail 'The en-US ADML file must parse and contain its presentation.'

    $policyNamespace = 'http://schemas.microsoft.com/GroupPolicy/2006/07/PolicyDefinitions'
    Add-Check -Name 'ADMX policy namespace' `
        -Passed ($admx.DocumentElement.NamespaceURI -eq $policyNamespace) `
        -Detail 'The ADMX root must use the Microsoft Group Policy policy-definition namespace.'
    Add-Check -Name 'ADML policy namespace' `
        -Passed ($adml.DocumentElement.NamespaceURI -eq $policyNamespace) `
        -Detail 'The ADML root must use the Microsoft Group Policy policy-definition namespace.'

    $admxNamespaces = [System.Xml.XmlNamespaceManager]::new($admx.NameTable)
    $admxNamespaces.AddNamespace('p', $policyNamespace)
    $admlNamespaces = [System.Xml.XmlNamespaceManager]::new($adml.NameTable)
    $admlNamespaces.AddNamespace('p', $policyNamespace)

    $policy = $admx.SelectSingleNode('/p:policyDefinitions/p:policies/p:policy[@name="EnterpriseAccessPolicy"]', $admxNamespaces)
    Add-Check -Name 'Machine policy scope' `
        -Passed ($null -ne $policy -and $policy.GetAttribute('class') -eq 'Machine' -and
            $policy.GetAttribute('key') -eq 'SOFTWARE\Policies\Hybrid Solutions Cloud\Vault Prospector') `
        -Detail 'EnterpriseAccessPolicy must be a machine policy at the documented production registry path.'

    $elementIds = @(
        $admx.SelectNodes('//p:policy[@name="EnterpriseAccessPolicy"]/p:elements/*[@id]', $admxNamespaces) |
            ForEach-Object { $_.GetAttribute('id') }
    )
    $presentationReferenceIds = @(
        $adml.SelectNodes('//p:presentation[@id="Policy_EnterpriseAccess"]//*[@refId]', $admlNamespaces) |
            ForEach-Object { $_.GetAttribute('refId') }
    )
    $unpresentedElementIds = @($elementIds | Where-Object { $_ -notin $presentationReferenceIds })
    $unknownPresentationIds = @($presentationReferenceIds | Where-Object { $_ -notin $elementIds })
    Add-Check -Name 'ADML element references' `
        -Passed ($elementIds.Count -eq 7 -and $unpresentedElementIds.Count -eq 0 -and
            $unknownPresentationIds.Count -eq 0) `
        -Detail 'The policy presentation must reference all seven ADMX elements and no unknown element IDs.'

    $stringIds = @(
        $adml.SelectNodes('//p:stringTable/p:string[@id]', $admlNamespaces) |
            ForEach-Object { $_.GetAttribute('id') }
    )
    $stringReferenceIds = @(
        [regex]::Matches("$($admx.OuterXml)`n$($adml.OuterXml)", '\$\(string\.([^)]+)\)') |
            ForEach-Object { $_.Groups[1].Value } |
            Sort-Object -Unique
    )
    $missingStringIds = @($stringReferenceIds | Where-Object { $_ -notin $stringIds })
    Add-Check -Name 'ADML string references' -Passed ($missingStringIds.Count -eq 0) `
        -Detail 'Every $(string.*) reference in the policy templates must resolve to an en-US string.'

    $presentationIds = @(
        $adml.SelectNodes('//p:presentationTable/p:presentation[@id]', $admlNamespaces) |
            ForEach-Object { $_.GetAttribute('id') }
    )
    $presentationReferenceIdsFromAdmx = @(
        [regex]::Matches($admx.OuterXml, '\$\(presentation\.([^)]+)\)') |
            ForEach-Object { $_.Groups[1].Value } |
            Sort-Object -Unique
    )
    $missingPresentationIds = @($presentationReferenceIdsFromAdmx | Where-Object { $_ -notin $presentationIds })
    Add-Check -Name 'ADML presentation references' -Passed ($missingPresentationIds.Count -eq 0) `
        -Detail 'Every $(presentation.*) reference in the ADMX file must resolve to an en-US presentation.'
}
catch {
    Add-Check -Name 'ADMX XML' -Passed $false `
        -Detail 'The ADMX or ADML file is not well-formed XML.'
    Add-Check -Name 'ADML XML' -Passed $false `
        -Detail 'The ADMX or ADML file is not well-formed XML.'
}

$admxText = Get-Content -LiteralPath $admxPath -Raw
foreach ($marker in @(
        'SOFTWARE\Policies\Hybrid Solutions Cloud\Vault Prospector'
        'PolicyVersion'
        'AllowedTenantIds'
        'AllowedProviders'
        'AllowedIdentityTypes'
        'DisableClipboard'
        'DisableOfflineCache'
        'DisableRemoteCredentialVerification'
        'MaximumOfflineCacheMinutes')) {
    Add-Check -Name "ADMX policy value: $marker" -Passed $admxText.Contains($marker) `
        -Detail "The Group Policy template must define '$marker'."
}

$sourceContracts = [ordered]@{
    'src/VaultProspector.Application/Services.cs' = @(
        'EnsureIdentityAllowed'
        'VaultDiscoveryConstraints'
        'EnsureClipboardAllowed'
        'EnsureOfflineCacheAllowed'
        'ApplyTenantConstraints')
    'src/VaultProspector.Application/CyberArkService.cs' = @(
        'CyberArkPrivilegeCloud'
        'EnsureClipboardAllowed')
    'src/VaultProspector.Providers.Azure/WorkloadIdentityDiscoveryService.cs' = @(
        'EnsureAdministratorAllowed'
        'EnsureTenantAllowed')
    'src/VaultProspector.App/Views/MainWindow.axaml' = @(
        'EnterprisePolicyStatus'
        'IsEnterpriseOfflineCacheAllowed'
        'IsEnterpriseClipboardAllowed')
}
foreach ($entry in $sourceContracts.GetEnumerator()) {
    $content = Get-Content -LiteralPath (Resolve-RepositoryPath $entry.Key) -Raw
    foreach ($marker in $entry.Value) {
        Add-Check -Name "Enforcement marker: $marker" -Passed $content.Contains($marker) `
            -Detail "$($entry.Key) must retain '$marker'."
    }
}

$packageScript = Get-Content -LiteralPath (Resolve-RepositoryPath 'scripts/Package.ps1') -Raw
foreach ($marker in @(
        'PolicyDefinitions'
        'VaultProspector.admx'
        'VaultProspector.adml')) {
    Add-Check -Name "Package marker: $marker" -Passed $packageScript.Contains($marker) `
        -Detail "Package.ps1 must include '$marker'."
}

$documentation = Get-Content -LiteralPath (Resolve-RepositoryPath 'docs/enterprise-policy.md') -Raw
foreach ($marker in @(
        'fail closed'
        'never writes this key'
        'most restrictive applicable boundary'
        'It never creates, changes, or deletes')) {
    Add-Check -Name "Documentation marker: $marker" -Passed $documentation.Contains($marker) `
        -Detail "The enterprise policy guide must retain '$marker'."
}

$registryPath = 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Hybrid Solutions Cloud\Vault Prospector'
$liveRegistry = [ordered]@{
    keyPresent = $false
    readable = $true
    valueNames = @()
    valueKinds = [ordered]@{}
    multiStringCounts = [ordered]@{}
}
try {
    if (Test-Path -LiteralPath $registryPath) {
        $liveRegistry.keyPresent = $true
        $key = Get-Item -LiteralPath $registryPath
        try {
            $liveRegistry.valueNames = @($key.GetValueNames() | Sort-Object)
            foreach ($name in $liveRegistry.valueNames) {
                $liveRegistry.valueKinds[$name] = $key.GetValueKind($name).ToString()
                if ($key.GetValueKind($name).ToString() -eq 'MultiString') {
                    $liveRegistry.multiStringCounts[$name] = @($key.GetValue($name)).Count
                }
            }
        }
        finally {
            $key.Close()
        }
    }
}
catch {
    $liveRegistry.readable = $false
}
Add-Check -Name 'Live HKLM policy is readable' -Passed $liveRegistry.readable `
    -Detail 'The current machine policy must be readable; absence is a valid unmanaged state.'

if (-not [string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $resolvedPublishDirectory = Resolve-RepositoryPath $PublishDirectory
    foreach ($relativePath in @(
            'PolicyDefinitions/VaultProspector.admx'
            'PolicyDefinitions/en-US/VaultProspector.adml')) {
        Add-Check -Name "Published policy: $relativePath" `
            -Passed (Test-Path -LiteralPath (Join-Path $resolvedPublishDirectory $relativePath) -PathType Leaf) `
            -Detail "Published payload must contain '$relativePath'."
    }
}

$sourceCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
$report = [ordered]@{
    schemaVersion = 1
    status = if ($findings.Count -eq 0) { 'passed' } else { 'failed' }
    observedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    sourceCommit = $sourceCommit
    checks = @($checks)
    findings = @($findings)
    liveRegistry = $liveRegistry
    summary = [ordered]@{
        passed = @($checks | Where-Object { $_.passed }).Count
        failed = @($checks | Where-Object { -not $_.passed }).Count
        livePolicyPresent = $liveRegistry.keyPresent
    }
}

$resolvedOutputPath = Resolve-RepositoryPath $OutputPath
$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}
$report | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8NoBOM
Write-Host "Enterprise policy report: $resolvedOutputPath"
Write-Host "Checks: $($report.summary.passed) passed, $($report.summary.failed) failed; live policy present: $($report.summary.livePolicyPresent)."

if ($findings.Count -gt 0) {
    foreach ($finding in $findings) {
        Write-Error "[$($finding.code)] $($finding.message)" -ErrorAction Continue
    }
    throw "Enterprise policy validation failed with $($findings.Count) error(s)."
}
