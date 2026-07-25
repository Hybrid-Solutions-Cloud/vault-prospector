# CI build environments

Vault Prospector uses GitHub Actions with HCS-owned compute. Azure DevOps Boards remains the
governed system for epics, features, user stories, tasks, acceptance criteria, and evidence links;
Azure DevOps Pipelines is not used for current delivery automation.

## Workflows and HCS routing

| Workflow | Trigger | HCS environment | Purpose |
| --- | --- | --- | --- |
| `.github/workflows/ci.yml` portable jobs | Pull requests and `main` | Tier 2 Azure Container Apps runner: `self-hosted,linux,ubuntu-22.04,hcs` | Locked restore, formatting, build, platform-neutral tests, browser build/tests, dependency audit, operational contract, and full-history secret scan |
| `.github/workflows/ci.yml` Windows candidate | `main` and manual | Tier 4 ephemeral Azure Windows VM: `self-hosted,windows,hcs,vault-prospector` | Full Windows tests, performance, MSI/MSIX/package candidates, browser host, legal/privacy, enterprise policy, and operational evidence |
| `.github/workflows/operational-readiness.yml` | Mondays at 18:17 UTC and manual | Tier 2 Azure Container Apps runner | Dependency, runtime-lifecycle, ownership, support-channel, and public-endpoint verification |
| `.github/workflows/release.yml` | Immutable `v*` tags | Tier 4 ephemeral Azure Windows VM | Exact-tag rebuild, package validation, MSIX, SPDX SBOM, Sigstore bundles, and public binary-only publication |

The repo-specific Tier 2 runner is the Azure Container Apps job
`caj-hcs-vp-gh-runner-eus2-01` in `rg-hcs-gh-runners-eus2-01`. It scales to zero and creates
ephemeral runners only when matching work is queued.

Windows packaging uses the isolated resource group `rg-hcs-vp-winbuild-eus2-01`. The deployment
script creates a random temporary administrator credential in HCS Key Vault, runs the required
what-if, provisions one ephemeral runner, and leaves the credential only until cleanup.

## Validate a pull request

1. Confirm both Tier 2 jobs use the PR merge ref and pass.
2. Run the full Windows suite locally when Windows-specific source or packaging changes.
3. After merge, provision the Tier 4 runner for the queued `main` Windows candidate:

```powershell
pwsh ./scripts/Deploy-HcsWindowsFallback.ps1 -Deploy -Confirm:$false
```

4. Confirm the exact `main` Windows candidate succeeds and retains its artifacts.
5. Remove the ephemeral environment:

```powershell
pwsh ./scripts/Remove-HcsWindowsFallback.ps1 -Remove -Confirm:$false
```

Cleanup validates exact resource tags, removes the VM identity's Key Vault assignment, starts
deletion of the isolated resource group, and soft-deletes the temporary credential pair.

Historical ADO builds remain immutable evidence for the commits they tested. They are not evidence
for later commits and do not define the current execution system.
