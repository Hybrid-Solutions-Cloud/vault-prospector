# Machine-managed enterprise policy

Vault Prospector reads machine policy from:

`HKLM\SOFTWARE\Policies\Hybrid Solutions Cloud\Vault Prospector`

The installed application never writes this key. Windows administrators deploy it through Group
Policy, Intune, configuration management, or another elevated system-management channel. Standard
users can see a safe policy summary in **Settings**, but cannot weaken the effective policy from
the application.

## Enforcement boundary

Policy is reread at each operation boundary. A newly tightened policy therefore blocks the next
connect, reauthentication, synchronization, workload-administration, reveal, copy, cache, offline
open, browser-fill, or CyberArk operation without waiting for application restart.

Enforcement occurs in application services as well as the UI:

- identity connection, enablement, reauthentication, directory authorization, and credential
  rotation enforce provider, identity-type, and home-tenant policy;
- Azure discovery receives the tenant allow-list before subscription/vault enumeration and the
  application filters the returned snapshot again before persistence;
- encrypted local search, tenant/subscription/vault displays, and value retrieval omit or deny
  tenants and providers that are no longer allowed;
- clipboard and offline-value policy applies to Azure, browser-fill, workspace, and CyberArk paths;
- workload identity discovery, authorization assessment, and dry-run plans enforce the selected
  administrator/target tenant and identity-type policy before network access; and
- disabling, revoking, purging, and removing existing profiles remains available so policy cannot
  prevent security cleanup.

Existing encrypted metadata is not silently deleted when policy changes. It becomes inaccessible
through governed views and operations, allowing an administrator to restore an accidental policy
change or the user to remove the profile deliberately.

## Registry values

An enabled policy requires `PolicyVersion=1` and `Enabled=1`.

| Value | Type | Meaning |
| --- | --- | --- |
| `PolicyVersion` | `REG_DWORD` | Required when `Enabled=1`; schema version must be `1`. |
| `Enabled` | `REG_DWORD` | `1` enables managed policy; `0` uses user/workspace defaults. |
| `AllowedTenantIds` | `REG_MULTI_SZ` | Optional Microsoft Entra tenant GUID allow-list. Missing permits all tenants. |
| `AllowedProviders` | `REG_MULTI_SZ` | Optional values: `AzureKeyVault`, `CyberArkPrivilegeCloud`. Missing permits both; present and empty denies both. |
| `AllowedIdentityTypes` | `REG_MULTI_SZ` | Optional values: `InteractiveUser`, `ManagedIdentity`, `ServicePrincipal`, `FederatedServicePrincipal`. Missing permits all; present and empty denies all. |
| `DisableClipboard` | `REG_DWORD` | Optional `0`/`1`; `1` blocks all protected-value clipboard paths. |
| `DisableOfflineCache` | `REG_DWORD` | Optional `0`/`1`; `1` blocks new storage and opening of existing offline values. Purge remains available. |
| `DisableRemoteCredentialVerification` | `REG_DWORD` | Optional `0`/`1`; `1` prevents the current-account Windows credential fallback in AVD and Remote Desktop sessions. Missing or `0` permits it when Windows Hello reports that no verification device is present. |
| `MaximumOfflineCacheMinutes` | `REG_DWORD` | Optional lifetime cap from `1` through `10080` (seven days). The strictest machine/user/workspace value wins. |
| `MaximumRevealVerificationGraceSeconds` | `REG_DWORD` | Optional cap from `0` through `120`. `0` forces verification before every Reveal. Missing allows the user's Off/30/60/120-second choice. This never applies to copy, offline cache/open, recovery, browser fill, or administration. |

Unknown enum values, non-GUID tenant entries, wrong registry types, unsupported versions, invalid
switches, out-of-range lifetimes, and unreadable enabled policy fail closed. The UI and diagnostics
report only a bounded reason; configured tenant identifiers are not copied into the policy status
or diagnostic events.

## Group Policy deployment

The release payload includes:

- `PolicyDefinitions\VaultProspector.admx`
- `PolicyDefinitions\en-US\VaultProspector.adml`

Copy these into the local `%SystemRoot%\PolicyDefinitions` folders or the domain Central Store.
Then enable:

**Computer Configuration → Administrative Templates → Vault Prospector → Configure enterprise
access boundaries**

Allow-list fields use one value per line. Leaving an optional list unconfigured permits all
currently supported values. Use a present empty provider or identity-type list only when the
intent is to disable every value in that category.

## Direct registry deployment example

The following example permits one tenant and Azure Key Vault, allows only interactive and
certificate service-principal identities, disables clipboard, and caps encrypted offline values
at eight hours:

```powershell
$policyPath = 'HKLM:\SOFTWARE\Policies\Hybrid Solutions Cloud\Vault Prospector'
New-Item -Path $policyPath -Force | Out-Null
New-ItemProperty -Path $policyPath -Name PolicyVersion -PropertyType DWord -Value 1 -Force | Out-Null
New-ItemProperty -Path $policyPath -Name Enabled -PropertyType DWord -Value 1 -Force | Out-Null
New-ItemProperty -Path $policyPath -Name AllowedTenantIds -PropertyType MultiString `
    -Value @('11111111-1111-1111-1111-111111111111') -Force | Out-Null
New-ItemProperty -Path $policyPath -Name AllowedProviders -PropertyType MultiString `
    -Value @('AzureKeyVault') -Force | Out-Null
New-ItemProperty -Path $policyPath -Name AllowedIdentityTypes -PropertyType MultiString `
    -Value @('InteractiveUser', 'ServicePrincipal') -Force | Out-Null
New-ItemProperty -Path $policyPath -Name DisableClipboard -PropertyType DWord -Value 1 -Force | Out-Null
New-ItemProperty -Path $policyPath -Name DisableOfflineCache -PropertyType DWord -Value 0 -Force | Out-Null
New-ItemProperty -Path $policyPath -Name DisableRemoteCredentialVerification -PropertyType DWord -Value 0 -Force | Out-Null
New-ItemProperty -Path $policyPath -Name MaximumOfflineCacheMinutes -PropertyType DWord -Value 480 -Force | Out-Null
New-ItemProperty -Path $policyPath -Name MaximumRevealVerificationGraceSeconds -PropertyType DWord -Value 30 -Force | Out-Null
```

Deploy registry changes through an elevated management process. Set `Enabled=0` to disable
enforcement without deleting the retained configuration. Test stricter changes with synthetic
identities and vault metadata before production rollout.

## Validation and evidence

Run:

```powershell
pwsh ./scripts/Test-EnterprisePolicyReadiness.ps1
```

The check validates source contracts, the ADMX/ADML pair, documentation, package wiring, tests, and
read-only live registry visibility. When a publish directory is supplied, it also proves both
Group Policy files are present in the distributable payload. It never creates, changes, or deletes
registry values.

Machine policy does not replace Azure/CyberArk authorization, Windows verification, independent
security review, or exact signed-candidate validation. The most restrictive applicable boundary
wins.
