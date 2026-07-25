# Release scope, HCS runner, and MSIX evidence — 2026-07-25

## Decision

- The supported Windows release remains Azure-focused.
- CyberArk and native mobile are future-roadmap products and do not block Windows GA.
- Azure DevOps Boards remains the authoritative Epic → Feature → User Story → Task hierarchy.
- GitHub Actions on HCS-owned runners owns current build and release execution.
- Microsoft Store–signed MSIX is the free publicly trusted Windows channel.
- Direct MSI, ZIP, and pre-ingestion MSIX packages remain explicitly unsigned.
- G-01 has no arbitrary 30-day, evaluator/task quota, or 14-day waiting requirement.

## HCS runner evidence

- Tier 2 job: `caj-hcs-vp-gh-runner-eus2-01`
- Resource group: `rg-hcs-gh-runners-eus2-01`
- Labels: `self-hosted,linux,ubuntu-22.04,hcs`
- Pull request: [#26](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/26)
- Exact HCS validation:
  [GitHub Actions run 30146204363](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30146204363)
- Result: portable source validation passed; full-history secret scan passed.
- Azure job executions `caj-hcs-vp-gh-runner-eus2-01-8f2dx` and
  `caj-hcs-vp-gh-runner-eus2-01-zz682` completed the two ephemeral jobs.

The initial workflow attempt correctly failed because the minimal HCS runner image did not contain
Node. The workflow now installs pinned Node 24 with `actions/setup-node`; the replacement run
passed. This is retained as proof that the workflow depends only on declared toolchain setup.

## Local Windows evidence

The exact functional branch source was validated on Windows 11 build `10.0.26100` with .NET SDK
`10.0.302`:

| Check | Result |
| --- | --- |
| Locked restore, vulnerability check, format, Release build | Passed; 0 warnings and 0 errors |
| .NET tests | 371 passed, 0 failed |
| Browser extension | 6 passed; build passed |
| Design prototype | Build passed; high-severity audit reported 0 vulnerabilities |
| Operational readiness | 34/34 passed |
| Performance and scale | 8/8 targets passed with 50,000 objects |
| Legal/privacy | 29/29 passed; deterministic 245-component inventory |
| Enterprise policy | 44/44 passed |
| MSI schedule, shortcut icon, browser host | Passed |
| Action workflow and PowerShell syntax | Passed |

## Package evidence

Development candidate `0.3.0-ci.1`:

| Artifact | SHA-256 | Result |
| --- | --- | --- |
| `VaultProspector-0.3.0-ci.1-win-x64.msi` | `202f9909aed227e1af87b187d80811586eb0dc2c7c59603fa8c0d476fd3efc50` | Built; rollback-safe schedule and package contracts passed |
| `VaultProspector-0.3.0-ci.1-win-x64.msix` | `d2832a8c32a7a7c399e523f788e54f563731e8452199511bf31ecdeed14801c9` | Built and unpacked; x64 identity, entry point, four assets, and 474 entries verified; `NotSigned` before Store ingestion |
| `VaultProspector-0.3.0-ci.1-win-x64.zip` | `2bfc1a1aa3c89f6e21b2c90a6c17bc7d45bc47aaf69d4d302afca327cd7bf275` | Built with packaged legal and policy files |

The default MSIX development identity is not a Store identity. P-13 and G-07 remain open until the
exact Partner Center identity values are reserved, Microsoft certifies/signs the package, and the
Store-delivered package passes clean-machine install, launch, upgrade, and uninstall.
