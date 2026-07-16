#Requires -Version 7.0

[CmdletBinding()]
param(
    [string]$Solution = 'VaultProspector.sln',

    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$output = if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $dotnetOutput = & dotnet list $Solution package --vulnerable --include-transitive --format json
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet vulnerability inspection failed with exit code $LASTEXITCODE."
    }
    $dotnetOutput
}
else {
    Get-Content -LiteralPath $ReportPath -Raw
}

$report = $output | ConvertFrom-Json
$findings = @(
    foreach ($project in @($report.projects)) {
        $frameworksProperty = $project.PSObject.Properties['frameworks']
        if ($null -eq $frameworksProperty) { continue }

        foreach ($framework in @($frameworksProperty.Value)) {
            $packages = @()
            foreach ($propertyName in @('topLevelPackages', 'transitivePackages')) {
                $packageProperty = $framework.PSObject.Properties[$propertyName]
                if ($null -ne $packageProperty) { $packages += @($packageProperty.Value) }
            }

            foreach ($package in $packages) {
                $vulnerabilitiesProperty = $package.PSObject.Properties['vulnerabilities']
                if ($null -ne $vulnerabilitiesProperty -and @($vulnerabilitiesProperty.Value).Count -gt 0) {
                    [pscustomobject]@{
                        Project = $project.path
                        Framework = $framework.framework
                        Package = $package.id
                        Version = $package.resolvedVersion
                        Vulnerabilities = @($vulnerabilitiesProperty.Value)
                    }
                }
            }
        }
    }
)

if ($findings.Count -gt 0) {
    $findings | ConvertTo-Json -Depth 8 | Write-Host
    throw "$($findings.Count) vulnerable NuGet package reference(s) detected."
}

Write-Host 'No known vulnerable direct or transitive NuGet packages were detected.'
