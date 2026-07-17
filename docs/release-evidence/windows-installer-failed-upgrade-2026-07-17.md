# Windows Installer failed-upgrade rollback validation — 2026-07-17

**Scope:** Upgrade from immutable public Preview.2 to an unreleased corrected installer candidate

**Result:** The original installer failed rollback validation; the corrected installer passed all
27 lifecycle gates on the isolated Windows 11 guest. This is internal unsigned-candidate evidence,
not final signed-candidate or independent release approval.

## Defect found

The original WiX `MajorUpgrade` used its default `RemoveExistingProducts` placement after
`InstallValidate`. A test-only copy of the candidate injected a Windows Installer type-19 failure
immediately after `InstallFiles`. The upgrade returned `1603`, but the guest then contained neither
the previous nor candidate product registration. Cleanup found zero installed versions. This
proved that a failed update could remove the working Preview.2 installation.

The installer now schedules `RemoveExistingProducts` immediately after `InstallInitialize`, keeping
old-product removal inside the Windows Installer transaction. Microsoft documents this placement
as rollback-capable when the upgrade installation fails. The built MSI action sequence was inspected
directly before execution:

| Action | Sequence |
| --- | ---: |
| `InstallInitialize` | 1500 |
| `RemoveExistingProducts` | 1501 |
| `InstallFiles` | 4000 |
| `InstallFinalize` | 6600 |

Reference: [RemoveExistingProducts action](https://learn.microsoft.com/windows/win32/msi/removeexistingproducts-action).

## Artifact provenance

| Artifact | Purpose | SHA-256 |
| --- | --- | --- |
| `VaultProspector-0.1.0-preview.2-win-x64.msi` | Previous published version | `416D9558518EB094596F83CEB2236C77138403CAEABF5675488B006290B139B3` |
| unreleased `0.1.0-preview.70` MSI | Corrected source candidate | `11D87F24852A953113FF3AFA755AB5913B31DFD071D08B9DF8E8E4B94B9B96E8` |
| test-only modified candidate MSI | Injected failure after `InstallFiles` | `B4D94A9F6652762C0BD5E3139F43F75A24E3638A11C2333F3956BBD70AB0F822` |
| lifecycle scenario version 1.1.0 | Test harness transferred to guest | `9A6BD64E1DB73A3DB932C72FA8E83C6E6203FE3A9120FA2641F6AC9080B43848` |
| structured result | Sanitized committed evidence | `F07AA1D611DBA8AFEB44B743FF30A2768025687567392EF36025A81C3ADD6FCB` |

The guest independently recomputed the source MSI and scenario hashes after transfer. The injected
MSI is deliberately distinct, test-only, and was never published.

## Observed rollback and lifecycle behavior

The corrected run passed 27 of 27 gates:

- installed Preview.2 with one `0.1.2` Installed apps registration;
- injected the deterministic post-`InstallFiles` failure and observed exit code `1603`;
- restored exactly one Preview.2 registration;
- restored byte-identical Preview.2 executable and runtime configuration;
- preserved the Start-menu shortcut and pre-existing LocalApplicationData sentinel;
- completed the genuine upgrade to MSI version `0.1.70` with one registration;
- repaired a deliberately changed runtime configuration to its original hash;
- rejected downgrade with exit code `1603` while preserving the corrected candidate;
- uninstalled successfully, removed program files/shortcut/registration, and retained user state.

The machine-readable result is
[`windows-installer-failed-upgrade-2026-07-17.json`](windows-installer-failed-upgrade-2026-07-17.json).
Verbose MSI logs were retained only in restricted host scratch because they contain machine paths;
their hashes are recorded in the restricted run inventory.

## Cleanup and remaining boundary

After the run, the guest had zero Vault Prospector registrations, processes, `VP-*` scheduled tasks,
or test roots. The original encrypted user-state files were byte-identical:

- `vault-prospector.db`: `E9F47C102C0B2023A0B74F5B6C32B09C010DD71F203467F7AFEADE9C0C34B0D3`
- `keys/metadata-database.key`: `61AB767AB04345A2F7A6065BAB58E41F5F8D22DAB27B9D31516803D461D8F7C9`

P-09 remains in progress until the same rollback-capable installer behavior is repeated against the
final signed candidate and receives independent sign-off.
