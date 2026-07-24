[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$KeyVaultName,

    [switch]$RunAsBuildUser
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

if ($RunAsBuildUser) {
    & "$env:ProgramData\chocolatey\bin\choco.exe" install winget-cli --yes --no-progress --limit-output
    if ($LASTEXITCODE -ne 0) {
        throw "WinGet installation failed with exit code $LASTEXITCODE."
    }

    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machinePath;$userPath;$env:LOCALAPPDATA\Microsoft\WindowsApps"

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
    Set-Location -LiteralPath 'C:\actions-runner'
    & .\config.cmd `
        --unattended `
        --ephemeral `
        --disableupdate `
        --url 'https://github.com/Hybrid-Solutions-Cloud/vault-prospector' `
        --token $registration.token `
        --name "hcs-vp-win-$env:COMPUTERNAME" `
        --labels 'hcs,vault-prospector' `
        --work '_work'
    if ($LASTEXITCODE -ne 0) {
        throw "Runner registration failed with exit code $LASTEXITCODE."
    }

    $registration = $null
    & .\run.cmd
    exit $LASTEXITCODE
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
if ($LASTEXITCODE -ne 0) {
    throw "Build prerequisite installation failed with exit code $LASTEXITCODE."
}

$machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$env:Path = "$machinePath;$userPath"

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

New-Item -ItemType Directory -Path $runnerDirectory -Force | Out-Null
Expand-Archive -LiteralPath $runnerArchive -DestinationPath $runnerDirectory -Force

$adminPassword = Get-ManagedIdentitySecret -SecretName 'hcs-vault-prospector-windows-build-password'
$secureAdminPassword = ConvertTo-SecureString -String $adminPassword -AsPlainText -Force
Set-LocalUser -Name 'buildadmin' -Password $secureAdminPassword
Enable-LocalUser -Name 'buildadmin'

$buildUserSid = (Get-LocalUser -Name 'buildadmin').SID.Value
$securityPolicyPath = 'C:\Windows\Temp\vault-prospector-security-policy.inf'
$securityDatabasePath = 'C:\Windows\Temp\vault-prospector-security-policy.sdb'
& secedit.exe /export /cfg $securityPolicyPath /areas USER_RIGHTS /quiet
if ($LASTEXITCODE -ne 0) {
    throw "Security policy export failed with exit code $LASTEXITCODE."
}

$securityPolicy = Get-Content -LiteralPath $securityPolicyPath
$batchRightIndex = -1
for ($index = 0; $index -lt $securityPolicy.Count; $index++) {
    if ($securityPolicy[$index] -like 'SeBatchLogonRight*') {
        $batchRightIndex = $index
        break
    }
}

if ($batchRightIndex -ge 0) {
    if ($securityPolicy[$batchRightIndex] -notmatch [regex]::Escape($buildUserSid)) {
        $securityPolicy[$batchRightIndex] = "$($securityPolicy[$batchRightIndex]),*$buildUserSid"
    }
}
else {
    $privilegeIndex = [Array]::IndexOf($securityPolicy, '[Privilege Rights]')
    if ($privilegeIndex -lt 0) {
        throw 'The exported security policy did not contain a Privilege Rights section.'
    }
    $securityPolicy = @(
        $securityPolicy[0..$privilegeIndex]
        "SeBatchLogonRight = *$buildUserSid"
        $securityPolicy[($privilegeIndex + 1)..($securityPolicy.Count - 1)]
    )
}

Set-Content -LiteralPath $securityPolicyPath -Value $securityPolicy -Encoding Unicode
& secedit.exe /configure /db $securityDatabasePath /cfg $securityPolicyPath /areas USER_RIGHTS /quiet
if ($LASTEXITCODE -ne 0) {
    throw "Security policy configuration failed with exit code $LASTEXITCODE."
}

$taskName = 'VaultProspectorEphemeralRunner'
$taskAction = New-ScheduledTaskAction `
    -Execute 'powershell.exe' `
    -Argument "-NoLogo -NoProfile -ExecutionPolicy Bypass -File C:\Windows\Temp\Initialize-WindowsRunner.ps1 -KeyVaultName $KeyVaultName -RunAsBuildUser"
$taskSettings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 3) `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries
Register-ScheduledTask `
    -TaskName $taskName `
    -Action $taskAction `
    -Settings $taskSettings `
    -User "$env:COMPUTERNAME\buildadmin" `
    -Password $adminPassword `
    -RunLevel Highest `
    -Force | Out-Null

$adminPassword = $null
$secureAdminPassword.Dispose()
Start-ScheduledTask -TaskName $taskName
do {
    Start-Sleep -Seconds 10
    $task = Get-ScheduledTask -TaskName $taskName
} while ($task.State -eq 'Running')

$taskInfo = Get-ScheduledTaskInfo -TaskName $taskName
$runnerExitCode = $taskInfo.LastTaskResult

shutdown.exe /s /t 30 /d p:0:0 /c 'Vault Prospector ephemeral CI runner completed.'
exit $runnerExitCode
