# CI build environments

Vault Prospector uses the HCS build-environment tiers. Linux work runs on an ephemeral
Azure Container Apps job owned by this repository; the job scales to zero when no matching
workflow is queued.

## Workload routing

| Workload | Environment |
|---|---|
| Secret scanning, shared mobile tests, Android packaging | HCS Tier 2 Linux runner: `self-hosted, linux, ubuntu-22.04, hcs` |
| Windows desktop build, tests, and packaging | HCS Tier 3 `bld-01`; Tier 4 Windows fallback only when Tier 3 is unavailable |
| iOS simulator compilation | Governed macOS/Xcode runner |

GitHub coordinates workflow jobs, but HCS compute executes jobs carrying the HCS
self-hosted labels. The repo-scoped Linux job cannot accept work from another repository.

## Linux runner deployment

Prerequisites:

- an active Azure session with deployment access to `rg-hcs-gh-runners-eus2-01`;
- read access for Azure Resource Manager to `kv-hcs-vault-01`;
- the `hcs-platform-github-org-pat` secret registered under the HCS Key Vault standard.

Run a plan:

```powershell
pwsh ./scripts/Deploy-HcsCiRunner.ps1
```

Provision the planned resource:

```powershell
pwsh ./scripts/Deploy-HcsCiRunner.ps1 -Deploy
```

The script always runs `az deployment group what-if` before provisioning. The committed
parameter file contains only a Key Vault reference; the GitHub credential is not written to
the repository or a temporary parameter file.

## Validation

Confirm that Azure reports a successful job:

```powershell
az containerapp job show `
  --resource-group rg-hcs-gh-runners-eus2-01 `
  --name caj-hcs-vp-gh-runner-eus2-01 `
  --query "{state:properties.provisioningState,trigger:properties.configuration.triggerType}" `
  --output table
```

Queue a pull-request workflow and confirm that its Linux jobs report all four HCS labels.
The Container Apps execution history and Log Analytics workspace
`log-hcs-gh-runners-eus2-01` provide the infrastructure-side audit trail.

## Tier 4 Windows fallback

Use Tier 4 only when the HCS Tier 3 `bld-01` runner is unavailable:

```powershell
pwsh ./scripts/Deploy-HcsWindowsFallback.ps1
pwsh ./scripts/Deploy-HcsWindowsFallback.ps1 -Deploy
```

The deployment creates only `rg-hcs-vp-winbuild-eus2-01`. Its Windows Server 2025 VM has no
public IP, uses a system-assigned managed identity to read the runner-registration credential,
and registers for one job with `self-hosted, windows, hcs, vault-prospector`. The temporary
local-administrator username and password are paired Key Vault secrets with a 30-day expiry.

After the Windows workflow job reaches a terminal state, validate and remove the fallback:

```powershell
pwsh ./scripts/Remove-HcsWindowsFallback.ps1
pwsh ./scripts/Remove-HcsWindowsFallback.ps1 -Remove
```

Cleanup first removes the VM identity's Key Vault assignment, then starts deletion of the exact
tag-validated resource group and soft-deletes the temporary credential pair. The secrets remain
recoverable under Key Vault soft-delete policy.

## Recovery and retirement

Redeploy the committed Bicep to repair configuration drift. If the job must be retired,
remove the workflow references first, review a Bicep deletion plan, and use the governed
infrastructure approval process; do not delete the shared Container Apps environment or the
unrelated runner jobs it hosts.
