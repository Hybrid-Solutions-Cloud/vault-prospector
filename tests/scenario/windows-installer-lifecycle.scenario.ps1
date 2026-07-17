#Requires -Version 7.0
<#
.SYNOPSIS
    Exercises the Vault Prospector Windows Installer lifecycle end to end.

.DESCRIPTION
    Runs a fail-closed scenario covering checksum verification, clean installation of the
    previous Preview, transactional rollback of a deliberately failed major upgrade, successful
    major upgrade, forced repair of a deliberately changed non-secret application file, downgrade
    rejection, uninstall cleanup, and retained user state.
    The machine must start without Vault Prospector installed. The scenario restores that
    state in a best-effort cleanup block and writes structured JSON plus MSI logs.

.PARAMETER PreviousMsiPath
    Path to the previously published MSI used as the upgrade and downgrade source.

.PARAMETER PreviousSha256
    Expected SHA-256 hash for PreviousMsiPath.

.PARAMETER CurrentMsiPath
    Path to the candidate MSI under test.

.PARAMETER CurrentSha256
    Expected SHA-256 hash for CurrentMsiPath.

.PARAMETER OutputDirectory
    Repository-relative or absolute directory for MSI logs and the structured result.

.NOTES
    Author: Kristopher Turner
    Contact: kris@hybridsolutions.cloud
    Version: 1.1.0
    ScriptVersion    = "1.1.0"
    TaskReference    = "preview-readiness/P-09-installer-lifecycle"
    DocumentationRef = "docs/release-checklist.md"
    LastUpdated      = "2026-07-17"
    UpdatedBy        = "Codex"
    ChangeLog        = @(
        "1.1.0 - 2026-07-17 - Added deterministic post-InstallFiles failure and transactional rollback validation"
        "1.0.0 - 2026-07-16 - Added repeatable install, upgrade, repair, downgrade, uninstall, and retained-state scenario"
    )
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$PreviousMsiPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$PreviousSha256,

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$CurrentMsiPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$CurrentSha256,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory = 'artifacts/installer-lifecycle'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDirectory = Join-Path $outputRoot $runId
$script:logFile = Join-Path $runDirectory 'scenario.log'
$resultPath = Join-Path $runDirectory 'windows-installer-lifecycle.results.json'
$gates = [System.Collections.Generic.List[object]]::new()
$scenarioStartedAt = [DateTimeOffset]::UtcNow
$scenarioPassed = $false
$failureDetail = $null
$sentinelCreated = $false
$dataDirectoryExisted = $false
$installationMutationStarted = $false
$failedUpgradeMsiPath = $null
$failedUpgradeMsiHash = $null

function Write-Log {
    param(
        [Parameter(Mandatory)] [string]$Message,
        [Parameter()] [ValidateSet('INFO', 'PASS', 'FAIL', 'WARN', 'HEADER')] [string]$Level = 'INFO'
    )

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "[$timestamp] [$Level] $Message"
    $line | Out-File -LiteralPath $script:logFile -Append -Encoding utf8
    $color = switch ($Level) {
        'PASS' { 'Green' }
        'FAIL' { 'Red' }
        'WARN' { 'Yellow' }
        'HEADER' { 'Cyan' }
        default { 'Gray' }
    }
    Write-Host $line -ForegroundColor $color
}

function Add-Gate {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [bool]$Passed,
        [Parameter(Mandatory)] [string]$Detail
    )

    $gates.Add([pscustomobject]@{
            name = $Name
            result = if ($Passed) { 'PASS' } else { 'FAIL' }
            detail = $Detail
            observed_at = [DateTimeOffset]::UtcNow.ToString('O')
        })
    Write-Log -Message "$Name — $Detail" -Level $(if ($Passed) { 'PASS' } else { 'FAIL' })
    if (-not $Passed) {
        throw "Scenario stopped at gate '$Name': $Detail"
    }
}

function Get-MsiProperty {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Property
    )

    try {
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $database = $installer.OpenDatabase($Path, 0)
        $view = $database.OpenView("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = '$Property'")
        [void]$view.Execute()
        $record = $view.Fetch()
        if ($null -eq $record) {
            throw "MSI property '$Property' was not found."
        }
        return $record.StringData(1).Trim()
    } catch {
        throw "Failed to read MSI property '$Property' from '$Path': $($_.Exception.Message)"
    }
}

function New-DeliberatelyFailingMsi {
    param(
        [Parameter(Mandatory)] [string]$SourcePath,
        [Parameter(Mandatory)] [string]$DestinationPath
    )

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
    $installer = $null
    $database = $null
    $tableView = $null
    $createTableView = $null
    $customActionView = $null
    $sequenceView = $null
    try {
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $database = $installer.OpenDatabase($DestinationPath, 1)
        $tableView = $database.OpenView(
            "SELECT ``Name`` FROM ``_Tables`` WHERE ``Name`` = 'CustomAction'")
        [void]$tableView.Execute()
        $hasCustomActionTable = $null -ne $tableView.Fetch()
        if (-not $hasCustomActionTable) {
            $createTableView = $database.OpenView(
                "CREATE TABLE ``CustomAction`` (" +
                "``Action`` CHAR(72) NOT NULL, ``Type`` SHORT NOT NULL, " +
                "``Source`` CHAR(72), ``Target`` CHAR(0), ``ExtendedType`` LONG " +
                "PRIMARY KEY ``Action``)")
            [void]$createTableView.Execute()
        }
        $customActionView = $database.OpenView(
            "INSERT INTO ``CustomAction`` (``Action``, ``Type``, ``Source``, ``Target``) " +
            "VALUES ('VP_TEST_FAIL_AFTER_FILES', 19, '', 'Deliberate update failure for rollback validation')")
        [void]$customActionView.Execute()
        $sequenceView = $database.OpenView(
            "INSERT INTO ``InstallExecuteSequence`` (``Action``, ``Condition``, ``Sequence``) " +
            "VALUES ('VP_TEST_FAIL_AFTER_FILES', 'NOT Installed', 4001)")
        [void]$sequenceView.Execute()
        [void]$database.Commit()
    } catch {
        throw "Failed to create the deliberate rollback-probe MSI: $($_.Exception.Message)"
    } finally {
        foreach ($comObject in @(
                $sequenceView,
                $customActionView,
                $createTableView,
                $tableView,
                $database,
                $installer)) {
            if ($null -ne $comObject -and [Runtime.InteropServices.Marshal]::IsComObject($comObject)) {
                [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($comObject)
            }
        }
    }
}

function Get-VaultProspectorInstallation {
    $registryPaths = @(
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    return @(
        Get-ItemProperty -Path $registryPaths -ErrorAction SilentlyContinue |
            Where-Object {
                $displayNameProperty = $_.PSObject.Properties['DisplayName']
                $null -ne $displayNameProperty -and $displayNameProperty.Value -eq 'Vault Prospector'
            } |
            Select-Object DisplayName, DisplayVersion, PSChildName, UninstallString
    )
}

function Invoke-MsiExec {
    param(
        [Parameter(Mandatory)] [string]$Step,
        [Parameter(Mandatory)] [string[]]$Arguments
    )

    $msiLog = Join-Path $runDirectory "$Step.msi.log"
    $allArguments = @($Arguments) + @('/qn', '/norestart', '/L*v', "`"$msiLog`"")
    Write-Log -Message "Running Windows Installer step '$Step'." -Level 'HEADER'
    try {
        $process = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\msiexec.exe') -ArgumentList $allArguments -Wait -PassThru -NoNewWindow
        return [pscustomobject]@{ ExitCode = $process.ExitCode; LogPath = $msiLog }
    } catch {
        throw "Windows Installer step '$Step' could not start: $($_.Exception.Message)"
    }
}

function Test-SuccessExitCode {
    param([Parameter(Mandatory)] [int]$ExitCode)
    return $ExitCode -in 0, 3010
}

function Write-ScenarioResult {
    $result = [ordered]@{
        schema_version = 1
        scenario = 'windows-installer-lifecycle'
        started_at = $scenarioStartedAt.ToString('O')
        completed_at = [DateTimeOffset]::UtcNow.ToString('O')
        passed = $scenarioPassed
        failure = $failureDetail
        windows_version = [System.Environment]::OSVersion.VersionString
        previous_msi = [ordered]@{
            file_name = [System.IO.Path]::GetFileName($previousPath)
            sha256 = $actualPreviousHash
            product_code = $previousProductCode
            product_version = $previousProductVersion
            upgrade_code = $previousUpgradeCode
        }
        current_msi = [ordered]@{
            file_name = [System.IO.Path]::GetFileName($currentPath)
            sha256 = $actualCurrentHash
            product_code = $currentProductCode
            product_version = $currentProductVersion
            upgrade_code = $currentUpgradeCode
        }
        failed_upgrade_probe = [ordered]@{
            file_name = if ($failedUpgradeMsiPath) { [System.IO.Path]::GetFileName($failedUpgradeMsiPath) } else { $null }
            sha256 = $failedUpgradeMsiHash
            failure_action = 'VP_TEST_FAIL_AFTER_FILES'
            failure_sequence = 4001
        }
        gates = @($gates)
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8NoBOM
}

# === Main ===

New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$previousPath = [System.IO.Path]::GetFullPath($PreviousMsiPath)
$currentPath = [System.IO.Path]::GetFullPath($CurrentMsiPath)
$actualPreviousHash = (Get-FileHash -LiteralPath $previousPath -Algorithm SHA256).Hash.ToUpperInvariant()
$actualCurrentHash = (Get-FileHash -LiteralPath $currentPath -Algorithm SHA256).Hash.ToUpperInvariant()
$previousProductCode = Get-MsiProperty -Path $previousPath -Property 'ProductCode'
$previousProductVersion = Get-MsiProperty -Path $previousPath -Property 'ProductVersion'
$previousUpgradeCode = Get-MsiProperty -Path $previousPath -Property 'UpgradeCode'
$currentProductCode = Get-MsiProperty -Path $currentPath -Property 'ProductCode'
$currentProductVersion = Get-MsiProperty -Path $currentPath -Property 'ProductVersion'
$currentUpgradeCode = Get-MsiProperty -Path $currentPath -Property 'UpgradeCode'
$installDirectory = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Vault Prospector'
$applicationPath = Join-Path $installDirectory 'VaultProspector.App.exe'
$repairProbePath = Join-Path $installDirectory 'VaultProspector.App.runtimeconfig.json'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Vault Prospector\Vault Prospector.lnk'
$localAppDataRoot = [System.IO.Path]::GetFullPath([Environment]::GetFolderPath('LocalApplicationData'))
$dataDirectory = [System.IO.Path]::GetFullPath((Join-Path $localAppDataRoot 'VaultProspector'))
if (-not $dataDirectory.StartsWith($localAppDataRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Resolved data directory '$dataDirectory' is outside LocalApplicationData."
}
$sentinelPath = Join-Path $dataDirectory 'installer-lifecycle-sentinel.txt'

try {
    Write-Log -Message 'Starting Vault Prospector Windows Installer lifecycle scenario.' -Level 'HEADER'
    $isAdministrator = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    Add-Gate -Name 'administrator-context' -Passed $isAdministrator -Detail 'Per-machine lifecycle runs in an elevated Windows session.'
    $existingInstallationCount = @(Get-VaultProspectorInstallation).Count
    $cleanStartDetail = if ($existingInstallationCount -eq 0) {
        'No pre-existing Vault Prospector MSI registration is present.'
    } else {
        "Refusing to continue because $existingInstallationCount pre-existing Vault Prospector MSI registration(s) are present."
    }
    Add-Gate -Name 'clean-start' -Passed ($existingInstallationCount -eq 0) -Detail $cleanStartDetail
    Add-Gate -Name 'previous-checksum' -Passed ($actualPreviousHash -eq $PreviousSha256.ToUpperInvariant()) -Detail "Previous MSI SHA-256 is $actualPreviousHash."
    Add-Gate -Name 'current-checksum' -Passed ($actualCurrentHash -eq $CurrentSha256.ToUpperInvariant()) -Detail "Current MSI SHA-256 is $actualCurrentHash."
    Add-Gate -Name 'upgrade-code-stability' -Passed ($previousUpgradeCode -eq $currentUpgradeCode) -Detail "UpgradeCode is $currentUpgradeCode."
    Add-Gate -Name 'product-code-change' -Passed ($previousProductCode -ne $currentProductCode) -Detail 'Major-upgrade candidates use distinct ProductCodes.'
    Add-Gate -Name 'version-order' -Passed ([version]$currentProductVersion -gt [version]$previousProductVersion) -Detail "Version advances from $previousProductVersion to $currentProductVersion."

    $dataDirectoryExisted = Test-Path -LiteralPath $dataDirectory -PathType Container
    New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null
    if (Test-Path -LiteralPath $sentinelPath) {
        throw "Test sentinel already exists at '$sentinelPath'; remove only that file before retrying."
    }
    [Guid]::NewGuid().ToString('D') | Set-Content -LiteralPath $sentinelPath -Encoding utf8NoBOM
    $sentinelCreated = $true

    $installationMutationStarted = $true
    $installPrevious = Invoke-MsiExec -Step 'install-previous' -Arguments @('/i', "`"$previousPath`"")
    Add-Gate -Name 'install-previous-exit' -Passed (Test-SuccessExitCode $installPrevious.ExitCode) -Detail "msiexec returned $($installPrevious.ExitCode)."
    $previousRegistrations = @(Get-VaultProspectorInstallation)
    Add-Gate -Name 'install-previous-registration' -Passed ($previousRegistrations.Count -eq 1 -and $previousRegistrations[0].DisplayVersion -eq $previousProductVersion) -Detail "Exactly one Installed apps entry reports $previousProductVersion."
    Add-Gate -Name 'install-previous-files' -Passed ((Test-Path -LiteralPath $applicationPath -PathType Leaf) -and (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) -Detail 'Executable and Start menu shortcut are present.'

    $previousApplicationHash = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $previousRuntimeConfigHash = (Get-FileHash -LiteralPath $repairProbePath -Algorithm SHA256).Hash.ToUpperInvariant()
    $failedUpgradeMsiPath = Join-Path $runDirectory 'VaultProspector-deliberate-failed-upgrade.msi'
    New-DeliberatelyFailingMsi -SourcePath $currentPath -DestinationPath $failedUpgradeMsiPath
    $failedUpgradeMsiHash = (Get-FileHash -LiteralPath $failedUpgradeMsiPath -Algorithm SHA256).Hash.ToUpperInvariant()
    Add-Gate -Name 'failed-upgrade-probe-distinct' -Passed ($failedUpgradeMsiHash -ne $actualCurrentHash) -Detail "Test-only rollback probe has distinct SHA-256 $failedUpgradeMsiHash."

    $failedUpgrade = Invoke-MsiExec -Step 'upgrade-current-deliberate-failure' -Arguments @('/i', "`"$failedUpgradeMsiPath`"")
    Add-Gate -Name 'failed-upgrade-exit' -Passed ($failedUpgrade.ExitCode -eq 1603) -Detail "Injected post-InstallFiles failure returned Windows Installer exit code $($failedUpgrade.ExitCode)."
    $afterFailedUpgrade = @(Get-VaultProspectorInstallation)
    Add-Gate -Name 'failed-upgrade-registration-rollback' -Passed ($afterFailedUpgrade.Count -eq 1 -and $afterFailedUpgrade[0].DisplayVersion -eq $previousProductVersion) -Detail "Rollback restored exactly one Installed apps entry at $previousProductVersion."
    Add-Gate -Name 'failed-upgrade-file-rollback' -Passed (
        (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToUpperInvariant() -eq $previousApplicationHash -and
        (Get-FileHash -LiteralPath $repairProbePath -Algorithm SHA256).Hash.ToUpperInvariant() -eq $previousRuntimeConfigHash
    ) -Detail 'Rollback restored byte-identical previous executable and runtime configuration.'
    Add-Gate -Name 'failed-upgrade-shortcut-rollback' -Passed (Test-Path -LiteralPath $shortcutPath -PathType Leaf) -Detail 'Rollback preserved the previous Start menu shortcut.'
    Add-Gate -Name 'failed-upgrade-state-rollback' -Passed (Test-Path -LiteralPath $sentinelPath -PathType Leaf) -Detail 'Rollback preserved pre-existing LocalApplicationData state.'

    $upgradeCurrent = Invoke-MsiExec -Step 'upgrade-current' -Arguments @('/i', "`"$currentPath`"")
    Add-Gate -Name 'upgrade-current-exit' -Passed (Test-SuccessExitCode $upgradeCurrent.ExitCode) -Detail "msiexec returned $($upgradeCurrent.ExitCode)."
    $currentRegistrations = @(Get-VaultProspectorInstallation)
    Add-Gate -Name 'upgrade-current-registration' -Passed ($currentRegistrations.Count -eq 1 -and $currentRegistrations[0].DisplayVersion -eq $currentProductVersion) -Detail "Exactly one Installed apps entry reports $currentProductVersion."

    $repairHash = (Get-FileHash -LiteralPath $repairProbePath -Algorithm SHA256).Hash.ToUpperInvariant()
    '{}' | Set-Content -LiteralPath $repairProbePath -Encoding utf8NoBOM
    Add-Gate -Name 'repair-probe-changed' -Passed ((Get-FileHash -LiteralPath $repairProbePath -Algorithm SHA256).Hash.ToUpperInvariant() -ne $repairHash) -Detail 'A non-secret runtime configuration file was deliberately changed.'
    $repairCurrent = Invoke-MsiExec -Step 'repair-current' -Arguments @('/fa', "`"$currentPath`"")
    Add-Gate -Name 'repair-current-exit' -Passed (Test-SuccessExitCode $repairCurrent.ExitCode) -Detail "msiexec returned $($repairCurrent.ExitCode)."
    Add-Gate -Name 'repair-current-restored-file' -Passed ((Get-FileHash -LiteralPath $repairProbePath -Algorithm SHA256).Hash.ToUpperInvariant() -eq $repairHash) -Detail 'Forced repair restored the original packaged runtime configuration.'

    $downgradePrevious = Invoke-MsiExec -Step 'downgrade-previous' -Arguments @('/i', "`"$previousPath`"")
    Add-Gate -Name 'downgrade-rejected' -Passed ($downgradePrevious.ExitCode -eq 1603) -Detail "Older MSI was rejected with the expected Windows Installer exit code $($downgradePrevious.ExitCode)."
    $afterDowngrade = @(Get-VaultProspectorInstallation)
    Add-Gate -Name 'downgrade-preserved-current' -Passed ($afterDowngrade.Count -eq 1 -and $afterDowngrade[0].DisplayVersion -eq $currentProductVersion) -Detail "Installed version remains $currentProductVersion."

    $uninstallCurrent = Invoke-MsiExec -Step 'uninstall-current' -Arguments @('/x', $currentProductCode)
    Add-Gate -Name 'uninstall-current-exit' -Passed (Test-SuccessExitCode $uninstallCurrent.ExitCode) -Detail "msiexec returned $($uninstallCurrent.ExitCode)."
    Add-Gate -Name 'uninstall-registration-cleanup' -Passed (@(Get-VaultProspectorInstallation).Count -eq 0) -Detail 'Installed apps registration is removed.'
    Add-Gate -Name 'uninstall-file-cleanup' -Passed (-not (Test-Path -LiteralPath $applicationPath) -and -not (Test-Path -LiteralPath $shortcutPath)) -Detail 'Executable and Start menu shortcut are removed.'
    Add-Gate -Name 'uninstall-retained-state' -Passed (Test-Path -LiteralPath $sentinelPath -PathType Leaf) -Detail 'Uninstall retained pre-existing LocalApplicationData state.'

    $scenarioPassed = $true
    Write-Log -Message 'Windows Installer lifecycle scenario passed.' -Level 'PASS'
} catch {
    $failureDetail = $_.Exception.Message
    Write-Log -Message $failureDetail -Level 'FAIL'
    throw
} finally {
    try {
        if ($installationMutationStarted) {
            $scenarioProductCodes = @($previousProductCode, $currentProductCode)
            $remainingScenarioInstallations = @(
                Get-VaultProspectorInstallation |
                    Where-Object { $_.PSChildName -in $scenarioProductCodes }
            )
            if ($remainingScenarioInstallations.Count -gt 0) {
                Write-Log -Message 'Cleanup is removing only products installed by this scenario.' -Level 'WARN'
                foreach ($installation in $remainingScenarioInstallations) {
                    $cleanup = Invoke-MsiExec -Step "cleanup-$($installation.PSChildName.Trim('{}'))" -Arguments @('/x', $installation.PSChildName)
                    if (-not (Test-SuccessExitCode $cleanup.ExitCode)) {
                        throw "Cleanup uninstall for $($installation.PSChildName) returned $($cleanup.ExitCode)."
                    }
                }
            }
        }
        if ($sentinelCreated -and (Test-Path -LiteralPath $sentinelPath -PathType Leaf)) {
            Remove-Item -LiteralPath $sentinelPath -Force
        }
        if (-not $dataDirectoryExisted -and (Test-Path -LiteralPath $dataDirectory -PathType Container)) {
            $remainingData = @(Get-ChildItem -LiteralPath $dataDirectory -Force)
            if ($remainingData.Count -eq 0) {
                Remove-Item -LiteralPath $dataDirectory -Force
            }
        }
    } catch {
        $scenarioPassed = $false
        $cleanupFailure = "Scenario cleanup failed: $($_.Exception.Message)"
        $failureDetail = if ($failureDetail) { "$failureDetail $cleanupFailure" } else { $cleanupFailure }
        Write-Log -Message $cleanupFailure -Level 'FAIL'
    }
    Write-ScenarioResult
    Write-Host "Scenario result: $resultPath" -ForegroundColor Cyan
}
