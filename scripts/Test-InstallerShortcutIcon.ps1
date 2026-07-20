#Requires -Version 7.0
<#
.SYNOPSIS
    Verifies that the Vault Prospector MSI assigns its product icon to the Start-menu shortcut.

.DESCRIPTION
    Reads the built MSI Shortcut and Icon tables and fails unless the advertised Vault Prospector
    Start-menu shortcut explicitly references the non-empty embedded product icon. This prevents
    Windows Search and Start from rendering the advertised shortcut as a blank document.

.NOTES
    Author: Kristopher Turner
    Contact: kris@hybridsolutions.cloud
    Version: 1.0.0
    ScriptVersion    = "1.0.0"
    TaskReference    = "preview-readiness/P-09-start-menu-icon"
    DocumentationRef = "docs/release-checklist.md"
    LastUpdated      = "2026-07-17"
    UpdatedBy        = "Codex"
    ChangeLog        = @(
        "1.0.0 - 2026-07-17 - Added MSI advertised-shortcut icon validation"
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
$shortcutView = $null
$iconView = $null
$shortcutRecord = $null
$iconRecord = $null

try {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($resolvedPath, 0)

    $shortcutView = $database.OpenView(
        "SELECT ``Icon_``, ``IconIndex`` FROM ``Shortcut`` " +
        "WHERE ``Shortcut`` = 'VaultProspectorStartMenuShortcut'")
    [void]$shortcutView.Execute()
    $shortcutRecord = $shortcutView.Fetch()
    if ($null -eq $shortcutRecord) {
        throw 'Installer does not contain the Vault Prospector Start-menu shortcut.'
    }

    $iconName = $shortcutRecord.StringData(1)
    $iconIndex = $shortcutRecord.IntegerData(2)
    if ($iconName -ne 'VaultProspector.ico' -or $iconIndex -ne 0) {
        throw "Start-menu shortcut must reference VaultProspector.ico at index 0; observed icon='$iconName', index=$iconIndex."
    }

    $iconView = $database.OpenView(
        "SELECT ``Data`` FROM ``Icon`` WHERE ``Name`` = 'VaultProspector.ico'")
    [void]$iconView.Execute()
    $iconRecord = $iconView.Fetch()
    if ($null -eq $iconRecord -or $iconRecord.DataSize(1) -le 0) {
        throw 'Installer does not contain a non-empty VaultProspector.ico resource.'
    }

    [pscustomobject]@{
        installer = [System.IO.Path]::GetFileName($resolvedPath)
        sha256 = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash
        shortcut = 'VaultProspectorStartMenuShortcut'
        icon = $iconName
        iconIndex = $iconIndex
        iconBytes = $iconRecord.DataSize(1)
        valid = $true
    } | ConvertTo-Json -Compress | Write-Host
} finally {
    foreach ($comObject in @($iconRecord, $shortcutRecord, $iconView, $shortcutView, $database, $installer)) {
        if ($null -ne $comObject -and [Runtime.InteropServices.Marshal]::IsComObject($comObject)) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($comObject)
        }
    }
}
