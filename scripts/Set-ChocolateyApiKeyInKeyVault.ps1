#Requires -Version 7.0
<#
.SYNOPSIS
    Stores the Vault Prospector Chocolatey publisher API key in the HCS platform Key Vault.

.DESCRIPTION
    Prompts for the Chocolatey Community Repository API key without echoing it, then creates
    or updates the canonical secret in kv-hcs-vault-01. The secret receives the HCS-required
    ownership, rotation, management, and lifecycle tags and a 180-day expiration by default.

.PARAMETER VaultName
    Azure Key Vault name. Defaults to kv-hcs-vault-01.

.PARAMETER SecretName
    Canonical HCS secret name. Defaults to
    hcs-vault-prospector-chocolatey-publisher-api-key.

.PARAMETER ExpirationDays
    Number of days until the secret expires. Defaults to 180.

.EXAMPLE
    pwsh ./scripts/Set-ChocolateyApiKeyInKeyVault.ps1

.NOTES
    Requires Azure CLI and an authenticated account with Key Vault Secrets Officer access.
    The API key is never written to disk or displayed by this script.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidatePattern('^[a-z0-9-]{3,24}$')]
    [string]$VaultName = 'kv-hcs-vault-01',

    [ValidatePattern('^[a-z0-9-]{1,127}$')]
    [string]$SecretName = 'hcs-vault-prospector-chocolatey-publisher-api-key',

    [ValidateRange(1, 3650)]
    [int]$ExpirationDays = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$az = Get-Command 'az' -ErrorAction SilentlyContinue
if ($null -eq $az) {
    throw 'Azure CLI is required. Install it, then run az login before retrying.'
}

& $az.Source account show --only-show-errors --output none
if ($LASTEXITCODE -ne 0) {
    throw 'No active Azure CLI session was found. Run az login, then retry.'
}

$expiresOn = (Get-Date).ToUniversalTime().AddDays($ExpirationDays)
$expiresValue = $expiresOn.ToString('yyyy-MM-ddTHH:mm:ssZ')
$target = "keyvault://$VaultName/$SecretName"

if ($PSCmdlet.ShouldProcess($target, 'Create or update Chocolatey publisher API key')) {
    $secureApiKey = Read-Host 'Enter the Chocolatey Community Repository API key' -AsSecureString
    $plainApiKey = [System.Net.NetworkCredential]::new('', $secureApiKey).Password

    try {
        if ([string]::IsNullOrWhiteSpace($plainApiKey)) {
            throw 'The Chocolatey API key cannot be empty.'
        }

        & $az.Source keyvault secret set `
            --vault-name $VaultName `
            --name $SecretName `
            --value $plainApiKey `
            --expires $expiresValue `
            --tags `
                'owner=kris@hybridsolutions.cloud' `
                'project=vault-prospector' `
                "rotation-days=$ExpirationDays" `
                'managed-by=script' `
                'lifecycle=permanent' `
            --only-show-errors `
            --output none

        if ($LASTEXITCODE -ne 0) {
            throw "Azure CLI failed to store '$SecretName' in '$VaultName' (exit code $LASTEXITCODE)."
        }

        Write-Output "Stored $target"
        Write-Output "Expires $($expiresOn.ToString('u'))"
    }
    finally {
        $plainApiKey = $null
        $secureApiKey.Dispose()
    }
}
