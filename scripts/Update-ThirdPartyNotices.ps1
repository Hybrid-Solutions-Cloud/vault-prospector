#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$Check,

    [string]$InventoryPath = 'docs/legal/third-party-components.json',

    [string]$NoticesPath = 'THIRD-PARTY-NOTICES.md',

    [string]$OverridesPath = 'docs/legal/license-overrides.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Resolve-RepositoryPath {
    param([Parameter(Mandatory)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
}

function ConvertTo-RepositoryPath {
    param([Parameter(Mandatory)][string]$Path)

    return [System.IO.Path]::GetRelativePath($repositoryRoot, $Path).Replace('\', '/')
}

function Get-Scope {
    param([Parameter(Mandatory)][string]$RelativePath)

    if ($RelativePath -match '(^|/)tests/' -or $RelativePath -match 'Mobile\.Tests/') {
        return 'development'
    }
    if ($RelativePath.StartsWith('installer/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'build'
    }
    if ($RelativePath.StartsWith('mobile/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'production-mobile'
    }
    return 'production-desktop'
}

function Get-PackageShape {
    $packages = @{}
    $lockRoots = @('src', 'tests', 'mobile', 'installer')

    foreach ($lockRoot in $lockRoots) {
        $absoluteRoot = Resolve-RepositoryPath $lockRoot
        foreach ($lockFile in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter packages.lock.json -File) {
            $relativePath = ConvertTo-RepositoryPath $lockFile.FullName
            $scope = Get-Scope $relativePath
            $lock = Get-Content -LiteralPath $lockFile.FullName -Raw | ConvertFrom-Json

            foreach ($framework in $lock.dependencies.PSObject.Properties) {
                foreach ($package in $framework.Value.PSObject.Properties) {
                    if ($package.Value.type -eq 'Project') { continue }
                    $version = [string]$package.Value.resolved
                    if ([string]::IsNullOrWhiteSpace($version)) {
                        throw "Package '$($package.Name)' in '$relativePath' has no resolved version."
                    }

                    $key = "nuget|$($package.Name.ToLowerInvariant())|$version"
                    if (-not $packages.ContainsKey($key)) {
                        $packages[$key] = [ordered]@{
                            ecosystem = 'nuget'
                            id = $package.Name
                            version = $version
                            scopes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                            dependencyTypes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                            sourceFiles = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                        }
                    }

                    [void]$packages[$key].scopes.Add($scope)
                    [void]$packages[$key].dependencyTypes.Add(([string]$package.Value.type).ToLowerInvariant())
                    [void]$packages[$key].sourceFiles.Add($relativePath)
                }
            }
        }
    }

    $npmLockPath = Resolve-RepositoryPath 'docs/design/vault-prospector-ui-concepts/package-lock.json'
    $npmLock = Get-Content -LiteralPath $npmLockPath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($package in $npmLock['packages'].GetEnumerator()) {
        if ([string]::IsNullOrWhiteSpace([string]$package.Key)) { continue }
        $version = [string]$package.Value['version']
        if ([string]::IsNullOrWhiteSpace($version)) { continue }

        $packagePath = ([string]$package.Key).Replace('\', '/')
        $lastNodeModules = $packagePath.LastIndexOf('node_modules/', [StringComparison]::Ordinal)
        $id = $packagePath.Substring($lastNodeModules + 'node_modules/'.Length)
        $key = "npm|$($id.ToLowerInvariant())|$version"
        if (-not $packages.ContainsKey($key)) {
            $packages[$key] = [ordered]@{
                ecosystem = 'npm'
                id = $id
                version = $version
                scopes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                dependencyTypes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                sourceFiles = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                npmLicenses = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                npmResolvedValues = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            }
        }
        [void]$packages[$key].scopes.Add('design-prototype')
        [void]$packages[$key].dependencyTypes.Add($(if ($package.Value['dev']) { 'development' } else { 'production' }))
        [void]$packages[$key].sourceFiles.Add('docs/design/vault-prospector-ui-concepts/package-lock.json')
        if (-not [string]::IsNullOrWhiteSpace([string]$package.Value['license'])) {
            [void]$packages[$key].npmLicenses.Add([string]$package.Value['license'])
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$package.Value['resolved'])) {
            [void]$packages[$key].npmResolvedValues.Add([string]$package.Value['resolved'])
        }
    }

    $shape = @(
        foreach ($package in $packages.Values) {
            [ordered]@{
                ecosystem = $package.ecosystem
                id = $package.id
                version = $package.version
                scopes = @($package.scopes | Sort-Object)
                dependencyTypes = @($package.dependencyTypes | Sort-Object)
                sourceFiles = @($package.sourceFiles | Sort-Object)
                npmLicense = if ($package.Contains('npmLicenses')) {
                    @($package.npmLicenses | Sort-Object)[0]
                } else { $null }
                npmResolved = if ($package.Contains('npmResolvedValues')) {
                    @($package.npmResolvedValues | Sort-Object)[0]
                } else { $null }
            }
        }
    )
    return @($shape | Sort-Object `
        @{ Expression = { $_['ecosystem'] } },
        @{ Expression = { $_['id'] } },
        @{ Expression = { $_['version'] } })
}

function Get-ComparableShape {
    param([Parameter(Mandatory)][object[]]$Packages)

    return @(
        foreach ($package in $Packages) {
            [ordered]@{
                ecosystem = $package.ecosystem
                id = $package.id
                version = $package.version
                scopes = @($package.scopes)
                dependencyTypes = @($package.dependencyTypes)
                sourceFiles = @($package.sourceFiles)
            }
        }
    ) | ConvertTo-Json -Depth 6
}

function Get-NuGetCachePath {
    $output = & dotnet nuget locals global-packages --list
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to resolve the NuGet global-packages directory."
    }
    $line = @($output | Where-Object { $_ -match '^global-packages:' })[0]
    return ($line -split ':', 2)[1].Trim()
}

function Get-GeneratedInventory {
    param(
        [Parameter(Mandatory)][object[]]$PackageShape,
        [Parameter(Mandatory)][object]$Overrides
    )

    $overrideMap = @{}
    foreach ($override in @($Overrides.overrides)) {
        $overrideMap["$($override.ecosystem)|$($override.id.ToLowerInvariant())|$($override.version)"] = $override
    }
    $nugetCache = Get-NuGetCachePath

    $records = @(
        foreach ($package in $PackageShape) {
            $overrideKey = "$($package.ecosystem)|$($package.id.ToLowerInvariant())|$($package.version)"
            $override = $overrideMap[$overrideKey]
            $license = ''
            $licenseSource = ''
            $projectUrl = ''
            $packageUrl = ''
            $distribution = 'potential-candidate'
            $reviewStatus = 'metadata-recorded'
            $reason = ''

            if ($package.ecosystem -eq 'nuget') {
                $idLower = $package.id.ToLowerInvariant()
                $nuspecPath = Join-Path $nugetCache "$idLower/$($package.version)/$idLower.nuspec"
                if (-not (Test-Path -LiteralPath $nuspecPath -PathType Leaf)) {
                    throw "NuGet metadata is missing for $($package.id) $($package.version). Restore all lock files before updating notices."
                }

                [xml]$nuspec = Get-Content -LiteralPath $nuspecPath -Raw
                $metadata = $nuspec.package.metadata
                $licenseNode = $metadata.SelectSingleNode("*[local-name()='license']")
                $licenseUrlNode = $metadata.SelectSingleNode("*[local-name()='licenseUrl']")
                $repositoryNode = $metadata.SelectSingleNode("*[local-name()='repository']")
                $projectUrlNode = $metadata.SelectSingleNode("*[local-name()='projectUrl']")
                if ($null -ne $licenseNode) {
                    $licenseText = [string]$licenseNode.InnerText
                    if ($licenseNode.GetAttribute('type') -eq 'expression') {
                        $license = $licenseText
                        $licenseSource = 'nuspec-expression'
                    }
                    else {
                        $license = "file:$licenseText"
                        $licenseSource = 'nuspec-file'
                    }
                }
                elseif ($null -ne $licenseUrlNode -and
                    -not [string]::IsNullOrWhiteSpace([string]$licenseUrlNode.InnerText)) {
                    $license = [string]$licenseUrlNode.InnerText
                    $licenseSource = 'nuspec-url'
                }

                $projectUrl = if ($null -ne $repositoryNode -and
                    -not [string]::IsNullOrWhiteSpace($repositoryNode.GetAttribute('url'))) {
                    $repositoryNode.GetAttribute('url')
                }
                elseif ($null -ne $projectUrlNode) {
                    [string]$projectUrlNode.InnerText
                }
                else {
                    ''
                }
                $packageUrl = "https://www.nuget.org/packages/$($package.id)/$($package.version)"
            }
            else {
                $license = [string]$package.npmLicense
                $licenseSource = 'package-lock'
                $packageUrl = "https://www.npmjs.com/package/$($package.id)/v/$($package.version)"
                $projectUrl = [string]$package.npmResolved
            }

            if ($null -ne $override) {
                $license = [string]$override.license
                $licenseSource = [string]$override.licenseSource
                $projectUrl = [string]$override.projectUrl
                $distribution = [string]$override.distribution
                $reviewStatus = [string]$override.reviewStatus
                $reason = [string]$override.reason
            }

            if ([string]::IsNullOrWhiteSpace($license)) {
                throw "No license metadata or explicit override exists for $($package.ecosystem) $($package.id) $($package.version)."
            }

            [ordered]@{
                ecosystem = $package.ecosystem
                id = $package.id
                version = $package.version
                scopes = @($package.scopes)
                dependencyTypes = @($package.dependencyTypes)
                sourceFiles = @($package.sourceFiles)
                license = $license
                licenseSource = $licenseSource
                projectUrl = $projectUrl
                packageUrl = $packageUrl
                distribution = $distribution
                reviewStatus = $reviewStatus
                reviewReason = $reason
            }
        }
    )
    $records = @($records | Sort-Object `
        @{ Expression = { $_['ecosystem'] } },
        @{ Expression = { $_['id'] } },
        @{ Expression = { $_['version'] } })

    return [ordered]@{
        schemaVersion = 1
        generatedFrom = @(
            'src/**/packages.lock.json'
            'tests/**/packages.lock.json'
            'mobile/**/packages.lock.json'
            'installer/packages.lock.json'
            'docs/design/vault-prospector-ui-concepts/package-lock.json'
        )
        truthBoundary = 'Package metadata inventory; not legal approval. Exact release SBOM and counsel review remain required.'
        packages = $records
    }
}

function Get-NoticesMarkdown {
    param([Parameter(Mandatory)][object]$Inventory)

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('# Third-party component notices')
    $lines.Add('')
    $lines.Add('This file is generated from committed lock files by `scripts/Update-ThirdPartyNotices.ps1`.')
    $lines.Add('It records package-declared license metadata; it is not legal approval and does not replace')
    $lines.Add('the exact release SBOM, upstream license/NOTICE files, or counsel review.')
    $lines.Add('')
    $lines.Add('The Vault Prospector source itself is licensed under the repository [MIT license](LICENSE).')
    $lines.Add('')
    $lines.Add('## Production lock-graph components')
    $lines.Add('')
    $lines.Add('This is a conservative list from production project lock graphs. Build-only assets can appear')
    $lines.Add('transitively; the exact release SBOM determines which files are actually distributed.')
    $lines.Add('')
    $lines.Add('| Ecosystem | Component | Version | Declared license | Metadata |')
    $lines.Add('| --- | --- | --- | --- | --- |')

    $distributed = @($Inventory.packages | Where-Object {
        $_.distribution -eq 'potential-candidate' -and
        (@($_.scopes) -contains 'production-desktop' -or @($_.scopes) -contains 'production-mobile')
    } | Sort-Object `
        @{ Expression = { if ($_ -is [System.Collections.IDictionary]) { $_['ecosystem'] } else { $_.ecosystem } } },
        @{ Expression = { if ($_ -is [System.Collections.IDictionary]) { $_['id'] } else { $_.id } } },
        @{ Expression = { if ($_ -is [System.Collections.IDictionary]) { $_['version'] } else { $_.version } } })
    foreach ($package in $distributed) {
        $name = "$($package.id)".Replace('|', '\|')
        $license = "$($package.license)".Replace('|', '\|')
        $lines.Add("| $($package.ecosystem) | [$name]($($package.packageUrl)) | $($package.version) | $license | $($package.licenseSource) |")
    }

    $lines.Add('')
    $lines.Add('## Items requiring approval')
    $lines.Add('')
    $approvalRequired = @($Inventory.packages | Where-Object { $_.reviewStatus -eq 'approval-required' })
    if ($approvalRequired.Count -eq 0) {
        $lines.Add('None recorded.')
    }
    else {
        foreach ($package in $approvalRequired) {
            $lines.Add("- **$($package.id) $($package.version):** $($package.reviewReason)")
        }
    }
    $lines.Add('')
    $lines.Add('The complete production, build, test, and design-prototype inventory is in')
    $lines.Add('`docs/legal/third-party-components.json`.')

    return ($lines -join "`n") + "`n"
}

$resolvedInventoryPath = Resolve-RepositoryPath $InventoryPath
$resolvedNoticesPath = Resolve-RepositoryPath $NoticesPath
$resolvedOverridesPath = Resolve-RepositoryPath $OverridesPath
$packageShape = @(Get-PackageShape)

if ($Check) {
    if (-not (Test-Path -LiteralPath $resolvedInventoryPath -PathType Leaf)) {
        throw "Third-party inventory is missing: $resolvedInventoryPath"
    }
    if (-not (Test-Path -LiteralPath $resolvedNoticesPath -PathType Leaf)) {
        throw "Third-party notices are missing: $resolvedNoticesPath"
    }

    $inventory = Get-Content -LiteralPath $resolvedInventoryPath -Raw | ConvertFrom-Json
    $expectedMap = @{}
    foreach ($package in $packageShape) {
        $expectedMap["$($package.ecosystem)|$($package.id.ToLowerInvariant())|$($package.version)"] = $package
    }
    $actualMap = @{}
    foreach ($package in @($inventory.packages)) {
        $actualMap["$($package.ecosystem)|$($package.id.ToLowerInvariant())|$($package.version)"] = $package
    }
    if ($expectedMap.Count -ne $actualMap.Count) {
        throw "Third-party inventory has $($actualMap.Count) records but lock files require $($expectedMap.Count). Run scripts/Update-ThirdPartyNotices.ps1."
    }
    foreach ($key in $expectedMap.Keys) {
        if (-not $actualMap.ContainsKey($key)) {
            throw "Third-party inventory is missing '$key'. Run scripts/Update-ThirdPartyNotices.ps1."
        }
        $expected = $expectedMap[$key]
        $actual = $actualMap[$key]
        foreach ($field in @('scopes', 'dependencyTypes', 'sourceFiles')) {
            $expectedValues = @($expected[$field] | Sort-Object) -join '|'
            $actualValues = @($actual.$field | Sort-Object) -join '|'
            if ($expectedValues -cne $actualValues) {
                throw "Third-party inventory field '$field' is stale for '$key'. Run scripts/Update-ThirdPartyNotices.ps1."
            }
        }
    }

    foreach ($package in @($inventory.packages)) {
        if ([string]::IsNullOrWhiteSpace([string]$package.license)) {
            throw "Inventory package '$($package.id) $($package.version)' has no license metadata."
        }
        if ($package.license -eq 'NOASSERTION' -and $package.reviewStatus -ne 'approval-required') {
            throw "NOASSERTION package '$($package.id) $($package.version)' must require approval."
        }
    }

    $expectedNotices = Get-NoticesMarkdown $inventory
    $actualNotices = (Get-Content -LiteralPath $resolvedNoticesPath -Raw).Replace("`r`n", "`n")
    if ($expectedNotices -cne $actualNotices) {
        throw 'THIRD-PARTY-NOTICES.md is stale. Run scripts/Update-ThirdPartyNotices.ps1.'
    }

    Write-Host "Third-party inventory is current: $(@($inventory.packages).Count) package/version records."
    exit 0
}

$overrides = Get-Content -LiteralPath $resolvedOverridesPath -Raw | ConvertFrom-Json
$generatedInventory = Get-GeneratedInventory -PackageShape $packageShape -Overrides $overrides
$inventoryDirectory = Split-Path -Parent $resolvedInventoryPath
if (-not (Test-Path -LiteralPath $inventoryDirectory)) {
    New-Item -ItemType Directory -Path $inventoryDirectory -Force | Out-Null
}
$generatedInventory | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedInventoryPath -Encoding utf8NoBOM
Get-NoticesMarkdown $generatedInventory | Set-Content -LiteralPath $resolvedNoticesPath -Encoding utf8NoBOM -NoNewline
Write-Host "Updated $InventoryPath and $NoticesPath with $(@($generatedInventory.packages).Count) package/version records."
