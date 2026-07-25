#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$PackagePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    $requiredEntries = @(
        'AppxManifest.xml',
        'AppxBlockMap.xml',
        'VaultProspector.App.exe',
        'Assets/StoreLogo.png',
        'Assets/Square44x44Logo.png',
        'Assets/Square150x150Logo.png',
        'Assets/Wide310x150Logo.png'
    )
    $missingEntries = @($requiredEntries | Where-Object { $_ -notin $entries })
    if ($missingEntries.Count -gt 0) {
        throw "MSIX is missing required entries: $($missingEntries -join ', ')."
    }

    $manifestEntry = $archive.GetEntry('AppxManifest.xml')
    if ($null -eq $manifestEntry) {
        throw 'MSIX manifest was not found.'
    }
    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try {
        [xml]$manifest = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $namespaceManager.AddNamespace(
        'f',
        'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $identity = $manifest.SelectSingleNode('/f:Package/f:Identity', $namespaceManager)
    $application = $manifest.SelectSingleNode(
        '/f:Package/f:Applications/f:Application',
        $namespaceManager)

    if ($null -eq $identity -or $identity.ProcessorArchitecture -ne 'x64') {
        throw 'MSIX identity must target x64.'
    }
    if ($null -eq $application -or $application.Executable -ne 'VaultProspector.App.exe') {
        throw 'MSIX application entry point is invalid.'
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $resolvedPackage
    if ($signature.Status -ne 'NotSigned') {
        throw "Pre-ingestion MSIX must be unsigned; observed '$($signature.Status)'."
    }

    [ordered]@{
        package = Split-Path -Leaf $resolvedPackage
        sha256 = (Get-FileHash -LiteralPath $resolvedPackage -Algorithm SHA256).Hash
        identity = $identity.Name
        publisher = $identity.Publisher
        version = $identity.Version
        architecture = $identity.ProcessorArchitecture
        application = $application.Executable
        entryCount = $entries.Count
        signatureStatus = $signature.Status.ToString()
        valid = $true
    } | ConvertTo-Json -Compress
}
finally {
    $archive.Dispose()
}
