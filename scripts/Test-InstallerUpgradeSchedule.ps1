#Requires -Version 7.0
<#
.SYNOPSIS
    Verifies that a Vault Prospector MSI keeps major-upgrade removal inside the install transaction.

.DESCRIPTION
    Reads the built MSI InstallExecuteSequence and fails unless RemoveExistingProducts runs
    immediately after InstallInitialize and before InstallFiles/InstallFinalize. This prevents the
    unsafe WiX default that can leave neither version installed after a failed major upgrade.

.NOTES
    Author: Kristopher Turner
    Contact: kris@hybridsolutions.cloud
    Version: 1.0.0
    ScriptVersion    = "1.0.0"
    TaskReference    = "preview-readiness/P-09-installer-rollback"
    DocumentationRef = "docs/release-evidence/windows-installer-failed-upgrade-2026-07-17.md"
    LastUpdated      = "2026-07-17"
    UpdatedBy        = "Codex"
    ChangeLog        = @(
        "1.0.0 - 2026-07-17 - Added rollback-safe major-upgrade action-sequence validation"
    )
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$InstallerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedPath = [System.IO.Path]::GetFullPath($InstallerPath)
$installer = $null
$database = $null
$view = $null
$records = [System.Collections.Generic.List[object]]::new()

try {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($resolvedPath, 0)
    $view = $database.OpenView(
        "SELECT ``Action``, ``Sequence`` FROM ``InstallExecuteSequence`` " +
        "WHERE ``Action`` = 'InstallInitialize' " +
        "OR ``Action`` = 'RemoveExistingProducts' " +
        "OR ``Action`` = 'InstallFiles' " +
        "OR ``Action`` = 'InstallFinalize'")
    [void]$view.Execute()
    while ($record = $view.Fetch()) {
        $records.Add([pscustomobject]@{
                Action = $record.StringData(1)
                Sequence = $record.IntegerData(2)
            })
        if ([Runtime.InteropServices.Marshal]::IsComObject($record)) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
        }
    }
} finally {
    foreach ($comObject in @($view, $database, $installer)) {
        if ($null -ne $comObject -and [Runtime.InteropServices.Marshal]::IsComObject($comObject)) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($comObject)
        }
    }
}

$sequence = @{}
foreach ($record in $records) {
    $sequence[$record.Action] = $record.Sequence
}

$requiredActions = @('InstallInitialize', 'RemoveExistingProducts', 'InstallFiles', 'InstallFinalize')
$missing = @($requiredActions | Where-Object { -not $sequence.ContainsKey($_) })
if ($missing.Count -gt 0) {
    throw "Installer is missing required execute-sequence action(s): $($missing -join ', ')."
}

if ($sequence.RemoveExistingProducts -ne ($sequence.InstallInitialize + 1)) {
    throw "RemoveExistingProducts must run immediately after InstallInitialize; observed RemoveExistingProducts=$($sequence.RemoveExistingProducts), InstallInitialize=$($sequence.InstallInitialize)."
}

if ($sequence.RemoveExistingProducts -ge $sequence.InstallFiles -or
    $sequence.InstallFiles -ge $sequence.InstallFinalize) {
    throw 'Installer execute sequence does not keep rollback-safe removal before file installation and finalization.'
}

[pscustomobject]@{
    installer = [System.IO.Path]::GetFileName($resolvedPath)
    sha256 = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash
    installInitialize = $sequence.InstallInitialize
    removeExistingProducts = $sequence.RemoveExistingProducts
    installFiles = $sequence.InstallFiles
    installFinalize = $sequence.InstallFinalize
    rollbackSafe = $true
} | ConvertTo-Json -Compress | Write-Host
