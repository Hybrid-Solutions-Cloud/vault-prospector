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
$keyVaultName = 'kv-hcs-vault-01'
$usernameSecretName = 'hcs-vault-prospector-windows-build-username'
$passwordSecretName = 'hcs-vault-prospector-windows-build-password'
$temporarySecretsCreated = $false
$deploymentSucceeded = $false

function Restore-SoftDeletedSecretName {
    param([Parameter(Mandatory)] [string]$SecretName)

    az keyvault secret show-deleted `
        --vault-name $keyVaultName `
        --name $SecretName `
        --output none 2>$null
    if ($LASTEXITCODE -ne 0) {
        return
    }

    az keyvault secret recover `
        --vault-name $keyVaultName `
        --name $SecretName `
        --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Soft-deleted secret '$SecretName' could not be recovered for reuse."
    }

    for ($attempt = 1; $attempt -le 30; $attempt++) {
        az keyvault secret show `
            --vault-name $keyVaultName `
            --name $SecretName `
            --output none 2>$null
        if ($LASTEXITCODE -eq 0) {
            return
        }
        Start-Sleep -Seconds 2
    }

    throw "Recovered secret '$SecretName' did not become available."
}

$keyVaultId = (az keyvault show --name $keyVaultName --query id -o tsv).Trim()
if ([string]::IsNullOrWhiteSpace($keyVaultId)) {
    throw 'The HCS Key Vault resource ID could not be resolved.'
}

$parameterContent = Get-Content -LiteralPath $parameterFile -Raw
$resolvedContent = $parameterContent.Replace('{{HCS_KEY_VAULT_ID}}', $keyVaultId)
$temporaryParameterFile = Join-Path ([System.IO.Path]::GetTempPath()) "$deploymentName.parameters.json"
$temporaryPasswordFile = Join-Path ([System.IO.Path]::GetTempPath()) "$deploymentName.password.txt"

try {
    Restore-SoftDeletedSecretName -SecretName $usernameSecretName
    Restore-SoftDeletedSecretName -SecretName $passwordSecretName

    $randomBytes = [byte[]]::new(30)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($randomBytes)
    $adminPassword = 'Vp!' + [Convert]::ToBase64String($randomBytes).Replace('/', '7').Replace('+', 'A')
    [System.IO.File]::WriteAllText($temporaryPasswordFile, $adminPassword)
    $adminPassword = $null

    az keyvault secret set `
        --vault-name $keyVaultName `
        --name $usernameSecretName `
        --value 'buildadmin' `
        --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Temporary build username secret '$usernameSecretName' could not be created."
    }
    $temporarySecretsCreated = $true

    az keyvault secret set `
        --vault-name $keyVaultName `
        --name $passwordSecretName `
        --file $temporaryPasswordFile `
        --encoding utf-8 `
        --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Temporary build password secret '$passwordSecretName' could not be created."
    }
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
        $deploymentSucceeded = $true
    }
}
finally {
    Remove-Item -LiteralPath $temporaryParameterFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $temporaryPasswordFile -Force -ErrorAction SilentlyContinue

    if ($temporarySecretsCreated -and -not $deploymentSucceeded) {
        foreach ($secretName in @($usernameSecretName, $passwordSecretName)) {
            az keyvault secret delete `
                --vault-name $keyVaultName `
                --name $secretName `
                --only-show-errors | Out-Null
        }
        Write-Host 'The unused temporary credentials were soft-deleted from Key Vault.'
    }
}
