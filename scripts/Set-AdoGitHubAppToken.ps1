#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AppId,

    [Parameter(Mandatory)]
    [string]$PrivateKeyPem,

    [Parameter(Mandatory)]
    [string]$Organization
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:TF_BUILD)) {
    throw 'This script may run only inside Azure Pipelines.'
}

function ConvertTo-Base64Url {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Bytes
    )

    return [Convert]::ToBase64String($Bytes).
        TrimEnd('=').
        Replace('+', '-').
        Replace('/', '_')
}

$rsa = [System.Security.Cryptography.RSA]::Create()
try {
    $keyBytes = [Convert]::FromBase64String(
        $PrivateKeyPem.
            Replace('-----BEGIN RSA PRIVATE KEY-----', '').
            Replace('-----END RSA PRIVATE KEY-----', '') -replace '\s', ''
    )
    $rsa.ImportRSAPrivateKey($keyBytes, [ref]$null)

    $header = ConvertTo-Base64Url -Bytes (
        [Text.Encoding]::UTF8.GetBytes('{"alg":"RS256","typ":"JWT"}')
    )
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $payload = ConvertTo-Base64Url -Bytes (
        [Text.Encoding]::UTF8.GetBytes(
            "{`"iat`":$($now - 60),`"exp`":$($now + 600),`"iss`":`"$AppId`"}"
        )
    )
    $signature = ConvertTo-Base64Url -Bytes (
        $rsa.SignData(
            [Text.Encoding]::UTF8.GetBytes("$header.$payload"),
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1
        )
    )
}
finally {
    $rsa.Dispose()
}

$jwt = "$header.$payload.$signature"
$headers = @{
    Authorization = "Bearer $jwt"
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
$installation = Invoke-RestMethod `
    -Uri 'https://api.github.com/app/installations' `
    -Headers $headers |
    Where-Object { $_.account.login -eq $Organization } |
    Select-Object -First 1
if (-not $installation) {
    throw "The GitHub App is not installed for '$Organization'."
}

$token = (
    Invoke-RestMethod `
        -Method Post `
        -Uri "https://api.github.com/app/installations/$($installation.id)/access_tokens" `
        -Headers $headers `
        -ContentType 'application/json'
).token
if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'GitHub returned an empty App installation token.'
}

Write-Host "##vso[task.setsecret]$token"
Write-Host "##vso[task.setvariable variable=GH_TOKEN;issecret=true]$token"
