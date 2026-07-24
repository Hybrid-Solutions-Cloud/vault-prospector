#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$Remove
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resourceGroupName = 'rg-hcs-vp-winbuild-eus2-01'
$vmName = 'vm-hcs-vp-winbuild-eus2-01'
$keyVaultName = 'kv-hcs-vault-01'
$temporarySecrets = @(
    'hcs-vault-prospector-windows-build-username',
    'hcs-vault-prospector-windows-build-password'
)

$resourceGroup = az group show --name $resourceGroupName -o json 2>$null | ConvertFrom-Json
if (-not $resourceGroup) {
    Write-Host "Resource group '$resourceGroupName' does not exist."
    return
}

$expectedTags = @{
    Project = 'vault-prospector'
    Lifecycle = 'ephemeral'
    Workload = 'windows-build-fallback'
}
foreach ($entry in $expectedTags.GetEnumerator()) {
    if ($resourceGroup.tags.($entry.Key) -ne $entry.Value) {
        throw "Refusing cleanup because tag '$($entry.Key)' does not match '$($entry.Value)'."
    }
}

if (-not $Remove) {
    Write-Host "Validated the exact ephemeral resource group '$resourceGroupName'."
    Write-Host 'Re-run with -Remove to delete its Key Vault role assignment, resource group, and temporary credentials.'
    return
}

if ($PSCmdlet.ShouldProcess($resourceGroupName, 'Remove HCS Tier 4 Windows build environment')) {
    $principalId = (az vm show `
        --resource-group $resourceGroupName `
        --name $vmName `
        --query identity.principalId `
        -o tsv).Trim()
    $keyVaultId = (az keyvault show --name $keyVaultName --query id -o tsv).Trim()

    if (-not [string]::IsNullOrWhiteSpace($principalId)) {
        az role assignment delete `
            --assignee-object-id $principalId `
            --scope $keyVaultId `
            --role 'Key Vault Secrets User'
        if ($LASTEXITCODE -ne 0) {
            throw 'The ephemeral VM Key Vault role assignment could not be removed.'
        }
    }

    az group delete --name $resourceGroupName --yes --no-wait
    if ($LASTEXITCODE -ne 0) {
        throw 'The ephemeral Windows build resource group deletion could not be started.'
    }

    foreach ($secretName in $temporarySecrets) {
        az keyvault secret delete --vault-name $keyVaultName --name $secretName --only-show-errors | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Temporary Key Vault secret '$secretName' could not be soft-deleted."
        }
    }

    Write-Host 'Cleanup started. Azure resource-group deletion is asynchronous; Key Vault secrets are soft-deleted and recoverable.'
}
