#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$Deploy
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$runnerRoot = Join-Path $repoRoot 'infrastructure\ci-runners'
$templateFile = Join-Path $runnerRoot 'main.bicep'
$parameterFile = Join-Path $runnerRoot 'parameters.prod.json'
$resourceGroup = 'rg-hcs-gh-runners-eus2-01'
$deploymentName = "vault-prospector-ci-runner-$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))"

$subscriptionId = (az account show --query id -o tsv).Trim()
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'No active Azure subscription was found.'
}

$keyVaultId = (az keyvault show --name 'kv-hcs-vault-01' --query id -o tsv).Trim()
if ([string]::IsNullOrWhiteSpace($keyVaultId)) {
    throw 'The HCS Key Vault resource ID could not be resolved.'
}

$parameterContent = Get-Content -LiteralPath $parameterFile -Raw
$resolvedContent = $parameterContent.
    Replace('{{HCS_AZURE_SUBSCRIPTION_ID}}', $subscriptionId).
    Replace('{{HCS_KEY_VAULT_ID}}', $keyVaultId)
$temporaryParameterFile = Join-Path ([System.IO.Path]::GetTempPath()) "$deploymentName.parameters.json"

try {
    Set-Content -LiteralPath $temporaryParameterFile -Value $resolvedContent -Encoding utf8NoBOM

    $commonArguments = @(
        '--resource-group', $resourceGroup,
        '--template-file', $templateFile,
        '--parameters', "@$temporaryParameterFile",
        '--name', $deploymentName
    )

    Write-Host 'Running the required HCS infrastructure plan (what-if)...'
    az deployment group what-if @commonArguments
    if ($LASTEXITCODE -ne 0) {
        throw 'The HCS runner what-if failed.'
    }

    if (-not $Deploy) {
        Write-Host 'Plan completed. Re-run with -Deploy to provision the runner.'
        return
    }

    if ($PSCmdlet.ShouldProcess('Vault Prospector HCS Container Apps runner', 'Deploy')) {
        az deployment group create @commonArguments --only-show-errors
        if ($LASTEXITCODE -ne 0) {
            throw 'The HCS runner deployment failed.'
        }
    }
}
finally {
    Remove-Item -LiteralPath $temporaryParameterFile -Force -ErrorAction SilentlyContinue
}
