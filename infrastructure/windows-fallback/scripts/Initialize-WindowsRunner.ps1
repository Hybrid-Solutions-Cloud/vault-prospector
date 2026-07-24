[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$KeyVaultName,

    [switch]$InstallWinget
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Invoke-WithRetry {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Operation,
        [int]$Attempts = 20,
        [int]$DelaySeconds = 15
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            return & $Operation
        }
        catch {
            if ($attempt -eq $Attempts) {
                throw
            }
            Start-Sleep -Seconds $DelaySeconds
        }
    }
}

function Get-ManagedIdentitySecret {
    param(
        [Parameter(Mandatory)]
        [string]$SecretName
    )

    $identityToken = Invoke-WithRetry -Operation {
        (Invoke-RestMethod `
            -Headers @{ Metadata = 'true' } `
            -Uri 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https%3A%2F%2Fvault.azure.net').access_token
    }

    Invoke-WithRetry -Operation {
        (Invoke-RestMethod `
            -Headers @{ Authorization = "Bearer $identityToken" } `
            -Uri "https://$KeyVaultName.vault.azure.net/secrets/$SecretName`?api-version=7.4").value
    }
}

if ($InstallWinget) {
    & "$env:ProgramData\chocolatey\bin\choco.exe" install winget-cli --yes --no-progress --limit-output
    $wingetInstallExitCode = $LASTEXITCODE
    if ($wingetInstallExitCode -notin @(0, 2)) {
        throw "WinGet installation failed with exit code $wingetInstallExitCode."
    }
    return
}

if (-not (Get-Command choco.exe -ErrorAction SilentlyContinue)) {
    Set-ExecutionPolicy Bypass -Scope Process -Force
    Invoke-Expression ((New-Object Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
}

& "$env:ProgramData\chocolatey\bin\choco.exe" install `
    git `
    powershell-core `
    nodejs-lts `
    vcredist140 `
    --yes `
    --no-progress `
    --limit-output
$chocolateyExitCode = $LASTEXITCODE
if ($chocolateyExitCode -notin @(0, 2)) {
    throw "Build prerequisite installation failed with exit code $chocolateyExitCode."
}

$adminPassword = Get-ManagedIdentitySecret -SecretName 'hcs-vault-prospector-windows-build-password'
$secureAdminPassword = ConvertTo-SecureString -String $adminPassword -AsPlainText -Force
Set-LocalUser -Name 'buildadmin' -Password $secureAdminPassword
Enable-LocalUser -Name 'buildadmin'
$credential = [PSCredential]::new("$env:COMPUTERNAME\buildadmin", $secureAdminPassword)
$adminPassword = $null

$scriptPath = 'C:\Windows\Temp\Initialize-WindowsRunner.ps1'
Copy-Item -LiteralPath $PSCommandPath -Destination $scriptPath -Force
Set-Service -Name WinRM -StartupType Manual
Start-Service -Name WinRM
Enable-PSRemoting -SkipNetworkProfileCheck -Force
Invoke-Command `
    -ComputerName localhost `
    -Authentication Negotiate `
    -Credential $credential `
    -ScriptBlock {
        param(
            [string]$RemoteScriptPath,
            [string]$RemoteKeyVaultName
        )
        & $RemoteScriptPath -KeyVaultName $RemoteKeyVaultName -InstallWinget
    } `
    -ArgumentList @($scriptPath, $KeyVaultName)

$credential = $null
$secureAdminPassword.Dispose()

$wingetPackage = Get-ChildItem `
    -LiteralPath 'C:\Program Files\WindowsApps' `
    -Directory `
    -Filter 'Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe' |
    Sort-Object Name -Descending |
    Select-Object -First 1
if (-not $wingetPackage) {
    throw 'The WinGet package directory was not found after installation.'
}

$machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$env:Path = "$machinePath;$userPath;$($wingetPackage.FullName)"
& (Join-Path $wingetPackage.FullName 'winget.exe') --version
if ($LASTEXITCODE -ne 0) {
    throw "WinGet verification failed with exit code $LASTEXITCODE."
}

$runnerVersion = '2.336.0'
$runnerArchive = 'C:\Windows\Temp\actions-runner.zip'
$runnerUri = "https://github.com/actions/runner/releases/download/v$runnerVersion/actions-runner-win-x64-$runnerVersion.zip"
$runnerSha256 = 'D59123A43003E357B0805B5D0F611D0BD2F65AB67D51BD070DD4E7A0F685C162'
$runnerDirectory = 'C:\actions-runner'

Invoke-WebRequest -Uri $runnerUri -OutFile $runnerArchive
$actualRunnerHash = (Get-FileHash -LiteralPath $runnerArchive -Algorithm SHA256).Hash
if ($actualRunnerHash -ne $runnerSha256) {
    throw 'The downloaded GitHub Actions runner did not match the pinned SHA-256.'
}
if (Test-Path -LiteralPath $runnerDirectory) {
    Remove-Item -LiteralPath $runnerDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $runnerDirectory -Force | Out-Null
Expand-Archive -LiteralPath $runnerArchive -DestinationPath $runnerDirectory -Force

$githubPat = Get-ManagedIdentitySecret -SecretName 'hcs-platform-github-org-pat'
$registration = Invoke-RestMethod `
    -Method Post `
    -Headers @{
        Authorization = "Bearer $githubPat"
        Accept = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    } `
    -Uri 'https://api.github.com/repos/Hybrid-Solutions-Cloud/vault-prospector/actions/runners/registration-token'

$githubPat = $null
Set-Location -LiteralPath $runnerDirectory
& .\config.cmd `
    --unattended `
    --ephemeral `
    --disableupdate `
    --url 'https://github.com/Hybrid-Solutions-Cloud/vault-prospector' `
    --token $registration.token `
    --name "hcs-vp-win-$env:COMPUTERNAME-system" `
    --labels 'hcs,vault-prospector' `
    --work '_work'
if ($LASTEXITCODE -ne 0) {
    throw "Runner registration failed with exit code $LASTEXITCODE."
}

$registration = $null
& .\run.cmd
$runnerExitCode = $LASTEXITCODE

shutdown.exe /s /t 30 /d p:0:0 /c 'Vault Prospector ephemeral CI runner completed.'
exit $runnerExitCode
