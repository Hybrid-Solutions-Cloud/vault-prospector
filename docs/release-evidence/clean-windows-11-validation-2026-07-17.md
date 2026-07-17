# Clean Windows 11 Validation — 2026-07-17

**Candidate exercised:** immutable `0.1.0-preview.2` public MSI plus an unreleased
`0.1.0-preview.3` source build used only to validate the viewport remediation

**Result:** Clean-machine MSI lifecycle passed 21 of 21 gates; direct interactive launch passed;
one viewport defect was found and remediated in source. This is internal clean-machine evidence,
not the required independent final-candidate sign-off.

## Environment attestation

- Official Microsoft Windows 11 Enterprise Evaluation 25H2 x64 ISO:
  `Windows11EnterpriseEval-25H2-x64-en-us.iso`.
- ISO SHA-256: `A61ADEAB895EF5A4DB436E0A7011C92A2FF17BB0357F58B13BBC4062E535E7B9`,
  matched against Microsoft's published checksum before provisioning.
- Clean guest: Windows 11 Enterprise Evaluation, version `10.0.26200`, build `26200`, 64-bit.
- Generation 2 Hyper-V VM with Microsoft Windows Secure Boot enabled and a virtual TPM.
- Guest attestation reported Secure Boot `true`; TPM present, ready, enabled, and activated;
  Microsoft Defender antivirus and real-time protection enabled; and
  `SystemSetupInProgress = 0`.
- The final disk layout contained exactly one 16 MiB reserved partition, one 260 MiB system
  partition, and one basic Windows partition. The earlier duplicate-MSR provisioning defect was
  absent.
- The unattended setup credential was disposable and stored on the host only in a DPAPI-protected
  CLIXML file. The one residual guest answer file was removed before artifact transfer; a recursive
  post-cleanup check found zero `unattend` or `autounattend` XML files.
- The isolated guest used `10.10.10.20`; no Azure CLI, Azure PowerShell, development environment,
  or prior Vault Prospector installation was present.

## Artifact acquisition and identity

Preview.2 was downloaded anonymously inside the guest from the public binary release. The guest
observed 45,711,360 bytes and SHA-256
`416D9558518EB094596F83CEB2236C77138403CAEABF5675488B006290B139B3`, exactly matching the release
record. Preview.1 is not published in the public binary repository, so the immutable host copy was
transferred into the guest and independently rehashed there as
`0F959136B701FE5831AD36CB4913EDDE785B3892BD74E23E96635FEED79E7C88`.

The governed scenario requires PowerShell 7. The otherwise clean image supplied WinGet but not
`pwsh`, so `Microsoft.PowerShell` 7.6.3 was installed from the WinGet community source as a test-only
prerequisite. This did not create a Vault Prospector registration; the scenario's clean-start gate
still found zero product registrations before mutation.

## MSI lifecycle result

The unchanged `windows-installer-lifecycle` scenario ran from
`2026-07-17T02:54:30.9353635Z` through `2026-07-17T02:56:39.4890973Z` on Windows
`10.0.26200.0` and passed all 21 gates:

- administrator context and clean start;
- both immutable artifact hashes;
- stable UpgradeCode, distinct ProductCodes, and increasing MSI versions (`0.1.1` to `0.1.2`);
- silent Preview.1 install with exactly one registration, executable, and Start menu shortcut;
- silent Preview.2 major upgrade with exactly one current registration;
- forced repair restored a deliberately changed non-secret runtime configuration file;
- Preview.1 downgrade was rejected with Windows Installer code `1603` while Preview.2 remained;
- silent uninstall removed registration, executable, and shortcut while retaining the explicit
  LocalApplicationData sentinel;
- scenario cleanup removed its sentinel and left zero Vault Prospector registrations.

The exact structured result is committed as
[`clean-windows-11-installer-lifecycle-2026-07-17.json`](clean-windows-11-installer-lifecycle-2026-07-17.json).
Its SHA-256 is `B9EAAD0CD8DC9288FBC92E2933D6C5DB7BC06C987C51634D4C9E9A43F0E2A590`.
Verbose MSI logs remain in the disposable test environment because they contain machine-local paths.

## Interactive launch and protected local state

Preview.2 was installed again from the guest-downloaded MSI and launched in the logged-in desktop
session. The process was responsive in session 2 and the Vault Prospector UI rendered. No
Vault Prospector-related Windows Application error was present. First launch created:

- `vault-prospector.db`, 147,456 bytes, without the plaintext SQLite file header; and
- `keys/metadata-database.key`, 262 bytes, protected for the current Windows user.

No identity was connected and no Azure value, token, production identifier, or secret was used.

## Viewport defect and source remediation

The immutable Preview.2 UI defaulted to `1180x760`, causing right and bottom controls to extend
beyond the 1024x768 clean-VM work area. The unreleased source candidate now:

- opens at `960x640` with a `900x620` minimum;
- explicitly centers on the active screen so Windows cascade placement cannot put the initial
  window outside the work area; and
- gives the selected-object detail card a vertical scrollbar.

The 900x620 minimum recorded in this run was subsequently shown to be unusable at 200% display
scaling and was replaced by a work-area-aware adaptive layout. See the same-day
[200% scaling validation](windows-200-percent-scaling-2026-07-17.md); the observations below remain
the historical evidence for this earlier 100% viewport run.

A self-contained `0.1.0-preview.3` validation build was launched through the guest's real Explorer
desktop by Hyper-V synthetic keyboard input. The centered window fit completely within the work
area. Hyper-V synthetic mouse input scrolled the detail card and exposed all six actions, including
**Purge offline copy**, plus the Windows Hello guidance. The app remained responsive. The final
framebuffer hashes were:

- centered initial view: `B4683870F5F87927C37320D5F3C2843591769F2DE101A825D58041049E1980DF`;
- scrolled detail view: `4B4BF49E6B2FAB9FA538C1B3E0B3E8C0E8B7302F9A1F5DD1D01888D91139434E`.

These screenshots were retained only in the private scratch evidence area because public screenshots
require an explicit privacy review. The remediation must be included in a fresh immutable candidate
and rerun through MSI, signature, package-manager, scaling, keyboard, screen-reader, and independent
review gates before P-15 can pass.

## Boundaries and remaining gates

This run proves a clean supported Windows environment and repeatable installer behavior, but the same
agent provisioned and exercised the VM. It does not satisfy independent review. This installer run also did not prove
live Entra/MFA/Conditional Access, Azure Key Vault discovery or retrieval, Windows Hello presentation,
WinGet or Chocolatey catalog acceptance, Authenticode trust, High Contrast, NVDA/Narrator, or
final-candidate upgrade recovery. A later internal run now covers 200% display scaling, but packaged-candidate
and independent validation remain open in the authoritative readiness matrix.
