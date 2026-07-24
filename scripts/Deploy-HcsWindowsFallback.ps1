#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$Deploy
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$templateRoot = Join-Path $repoRoot 'infrastructure\windows-fallback'
$templateFile = Join-Path $templateRoot 'main.bicep'
$parameterFile = Join-Path $templateRoot 'parameters.prod.json'
$deploymentName = "vault-prospector-windows-fallback-$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))"

$keyVaultId = (az keyvault show --name 'kv-hcs-vault-01' --query id -o tsv).Trim()
if ([string]::IsNullOrWhiteSpace($keyVaultId)) {
    throw 'The HCS Key Vault resource ID could not be resolved.'
}

$parameterContent = Get-Content -LiteralPath $parameterFile -Raw
$resolvedContent = $parameterContent.Replace('{{HCS_KEY_VAULT_ID}}', $keyVaultId)
$temporaryParameterFile = Join-Path ([System.IO.Path]::GetTempPath()) "$deploymentName.parameters.json"

try {
    Set-Content -LiteralPath $temporaryParameterFile -Value $resolvedContent -Encoding utf8NoBOM

    $commonArguments = @(
        '--location', 'eastus2',
        '--template-file', $templateFile,
        '--parameters', "@$temporaryParameterFile",
        '--name', $deploymentName
    )

    Write-Host 'Running the required HCS Tier 4 infrastructure plan (what-if)...'
    az deployment sub what-if @commonArguments
    if ($LASTEXITCODE -ne 0) {
        throw 'The HCS Tier 4 what-if failed.'
    }

    if (-not $Deploy) {
        Write-Host 'Plan completed. Re-run with -Deploy to provision the ephemeral Windows runner.'
        return
    }

    if ($PSCmdlet.ShouldProcess('Vault Prospector HCS Tier 4 Windows runner', 'Deploy')) {
        az deployment sub create @commonArguments --only-show-errors
        if ($LASTEXITCODE -ne 0) {
            throw 'The HCS Tier 4 deployment failed.'
        }
    }
}
finally {
    Remove-Item -LiteralPath $temporaryParameterFile -Force -ErrorAction SilentlyContinue
}
