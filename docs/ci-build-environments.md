# CI build environments

Vault Prospector uses Azure DevOps Pipelines in the private **Vault Prospector** project, as
required by the HCS ADO project strategy. Pipeline YAML lives in `.ado/`; GitHub Actions is not
used for CI or release automation.

## Pipelines

| Pipeline | YAML | Trigger | Purpose |
| --- | --- | --- | --- |
| Vault Prospector CI | `.ado/ci.yml` | Pull requests and `main` | Windows build/test/package validation, full-history secret scanning, managed mobile tests, Android packaging, and unsigned iOS simulator compilation |
| Vault Prospector Operational Readiness | `.ado/operational-readiness.yml` | Mondays at 18:17 UTC and manual | Dependency, runtime-lifecycle, ownership, support-channel, and public-endpoint verification |
| Vault Prospector Release | `.ado/release.yml` | Immutable `v*` tags | Preview rebuild, package validation, SPDX SBOM, HCS Key Vault-backed Cosign signatures, public release publication, and Chocolatey submission |

The project uses the Key Vault-linked `vp-prd-secrets` variable group. Pipeline definitions
reference secret names only; values remain in `kv-hcs-vault-01`.

## Workload routing

| Workload | Environment |
| --- | --- |
| Windows desktop build, DPAPI tests, and desktop package validation | Azure Pipelines Windows Server 2025 hosted image |
| Full-history secret scanning, shared mobile tests, Android packaging | Azure Pipelines Ubuntu 24.04 hosted image |
| iOS simulator compilation | Azure Pipelines macOS 15 image with pinned Xcode 26.0.1 |

The macOS build selects `iossimulator-x64` on Intel agents and `iossimulator-arm64` on Apple
Silicon. The .NET iOS workload is pinned by `global.json` and the locked mobile dependency graph.

## HCS tier evidence and fallback

The migration candidate was independently validated on the HCS build-environment tiers before the
ADO cutover:

- HCS Tier 2 Container Apps completed secret scanning, managed mobile tests, and Android packaging.
- HCS Tier 4 Windows Server 2025 completed the full Windows validation suite under LocalSystem,
  including DPAPI CurrentUser tests and installer/package validation.
- HCS Tier 3 `bld-01` was unavailable because its registered Key Vault credential no longer matched
  the VM; the machine was returned to its original powered-off state.

The committed runner Bicep and deployment scripts remain a break-glass diagnostic fallback. They
do not define a second CI/CD system and must not be attached to normal GitHub workflows.

## Pull-request evidence

Every PR build checks out the synthetic pull-request merge ref. A successful run therefore proves
both the submitted commit and its merge with the current target branch. Retained test results,
package candidates, SBOMs, checksums, and signing bundles are Azure Pipeline artifacts.

Before merging:

1. Confirm every `Vault Prospector CI` job succeeded.
2. Confirm the run source is the PR merge ref and the source version matches the current PR.
3. Resolve any security, package, or platform failure without suppressing its gate.

## Ephemeral Windows cleanup

The HCS Tier 4 validation VM is temporary. After its job reaches a terminal state:

```powershell
pwsh ./scripts/Remove-HcsWindowsFallback.ps1
pwsh ./scripts/Remove-HcsWindowsFallback.ps1 -Remove
```

Cleanup validates the exact resource tags, removes the VM identity's Key Vault assignment, starts
deletion of the isolated resource group, and soft-deletes the temporary credential pair.
