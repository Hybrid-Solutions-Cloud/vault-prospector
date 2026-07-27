#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$InstallerPath,

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$PublishDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedInstaller = [System.IO.Path]::GetFullPath($InstallerPath)
$resolvedPublish = [System.IO.Path]::GetFullPath($PublishDirectory)
$hostDirectory = Join-Path $resolvedPublish 'BrowserHost'
$hostName = 'com.hybridsolutionscloud.vaultprospector'
$chromiumId = 'fmkdaepdbgdbhdhcednhppbhhejeabin'
$firefoxId = 'vault-prospector@hybrid-solutions.cloud'
$chromiumManifestName = "$hostName.chromium.json"
$firefoxManifestName = "$hostName.firefox.json"
$policyName = 'browser-fill-policy.json'
$extensionDirectory = Join-Path $resolvedPublish 'BrowserExtension'

$policyPath = Join-Path $resolvedPublish $policyName
if (-not (Test-Path -LiteralPath $policyPath -PathType Leaf)) {
    throw "Machine browser-fill policy '$policyName' is missing."
}
$policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json -Depth 8
if ($policy.version -ne 1 -or
    $policy.enabled -ne $false -or
    @($policy.allowedDestinations).Count -ne 0) {
    throw 'Packaged machine browser-fill policy must be fail-closed by default.'
}

$requiredFiles = @(
    'VaultProspector.BrowserHost.exe',
    'browser-host.json',
    $chromiumManifestName,
    $firefoxManifestName
)
foreach ($fileName in $requiredFiles) {
    $path = Join-Path $hostDirectory $fileName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Browser host package file '$fileName' is missing."
    }
}
$requiredExtensionFiles = @(
    'chromium\manifest.json',
    'chromium\background.js',
    'firefox\manifest.json',
    'firefox\background.js'
)
foreach ($relativePath in $requiredExtensionFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $extensionDirectory $relativePath) -PathType Leaf)) {
        throw "Packaged browser extension file '$relativePath' is missing."
    }
}
$chromium = Get-Content -LiteralPath (Join-Path $hostDirectory $chromiumManifestName) -Raw |
    ConvertFrom-Json -Depth 8
$firefox = Get-Content -LiteralPath (Join-Path $hostDirectory $firefoxManifestName) -Raw |
    ConvertFrom-Json -Depth 8
$configuration = Get-Content -LiteralPath (Join-Path $hostDirectory 'browser-host.json') -Raw |
    ConvertFrom-Json -Depth 8

if ($chromium.name -ne $hostName -or
    $chromium.type -ne 'stdio' -or
    $chromium.path -ne 'VaultProspector.BrowserHost.exe' -or
    @($chromium.allowed_origins).Count -ne 1 -or
    $chromium.allowed_origins[0] -ne "chrome-extension://$chromiumId/") {
    throw 'Chromium native-host manifest violates the exact host/extension allowlist.'
}
if ($firefox.name -ne $hostName -or
    $firefox.type -ne 'stdio' -or
    $firefox.path -ne 'VaultProspector.BrowserHost.exe' -or
    @($firefox.allowed_extensions).Count -ne 1 -or
    $firefox.allowed_extensions[0] -ne $firefoxId) {
    throw 'Firefox native-host manifest violates the exact host/extension allowlist.'
}
if ($configuration.protocolVersion -ne 1 -or
    $configuration.pipeName -ne 'VaultProspector.BrowserBroker.v1' -or
    @($configuration.chromiumExtensionIds).Count -ne 1 -or
    $configuration.chromiumExtensionIds[0] -ne $chromiumId -or
    @($configuration.firefoxExtensionIds).Count -ne 1 -or
    $configuration.firefoxExtensionIds[0] -ne $firefoxId) {
    throw 'Browser host configuration does not match the reviewed protocol and identities.'
}

$installer = $null
$database = $null
$registryView = $null
$fileView = $null
$registryRows = [System.Collections.Generic.List[object]]::new()
$fileNames = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
try {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($resolvedInstaller, 0)

    $registryView = $database.OpenView(
        'SELECT `Root`, `Key`, `Value` FROM `Registry`')
    [void]$registryView.Execute()
    while ($record = $registryView.Fetch()) {
        $registryRows.Add([pscustomobject]@{
                Root = $record.IntegerData(1)
                Key = $record.StringData(2)
                Value = $record.StringData(3)
            })
        if ([Runtime.InteropServices.Marshal]::IsComObject($record)) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
        }
    }

    $fileView = $database.OpenView('SELECT `FileName` FROM `File`')
    [void]$fileView.Execute()
    while ($record = $fileView.Fetch()) {
        $fileName = $record.StringData(1)
        $longName = ($fileName -split '\|')[-1]
        [void]$fileNames.Add($longName)
        if ([Runtime.InteropServices.Marshal]::IsComObject($record)) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
        }
    }
}
finally {
    foreach ($comObject in @($fileView, $registryView, $database, $installer)) {
        if ($null -ne $comObject -and
            [Runtime.InteropServices.Marshal]::IsComObject($comObject)) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($comObject)
        }
    }
}

foreach ($fileName in $requiredFiles) {
    if (-not $fileNames.Contains($fileName)) {
        throw "Installer File table does not contain browser host file '$fileName'."
    }
}
if (-not $fileNames.Contains($policyName)) {
    throw "Installer File table does not contain machine policy '$policyName'."
}
foreach ($relativePath in $requiredExtensionFiles) {
    $fileName = Split-Path -Leaf $relativePath
    if (-not $fileNames.Contains($fileName)) {
        throw "Installer File table does not contain browser extension file '$relativePath'."
    }
}

$expectedRegistrations = @{
    "SOFTWARE\Google\Chrome\NativeMessagingHosts\$hostName" =
        "[INSTALLFOLDER]BrowserHost\$chromiumManifestName"
    "SOFTWARE\Microsoft\Edge\NativeMessagingHosts\$hostName" =
        "[INSTALLFOLDER]BrowserHost\$chromiumManifestName"
    "SOFTWARE\Mozilla\NativeMessagingHosts\$hostName" =
        "[INSTALLFOLDER]BrowserHost\$firefoxManifestName"
}
foreach ($expected in $expectedRegistrations.GetEnumerator()) {
    $matches = @($registryRows | Where-Object {
            $_.Root -eq 2 -and
            $_.Key -eq $expected.Key -and
            $_.Value -eq $expected.Value
        })
    if ($matches.Count -ne 1) {
        throw "Installer must contain exactly one HKLM native-host registration for '$($expected.Key)'."
    }
}

[pscustomobject]@{
    installer = [System.IO.Path]::GetFileName($resolvedInstaller)
    sha256 = (Get-FileHash -LiteralPath $resolvedInstaller -Algorithm SHA256).Hash
    hostExecutable = 'BrowserHost\VaultProspector.BrowserHost.exe'
    chromiumExtensionId = $chromiumId
    firefoxExtensionId = $firefoxId
    registryEntries = $expectedRegistrations.Count
    defaultMachinePolicyEnabled = $policy.enabled
    valid = $true
} | ConvertTo-Json -Compress | Write-Host
