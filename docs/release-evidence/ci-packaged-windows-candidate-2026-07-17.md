# CI-packaged Windows candidate validation — 2026-07-17

**Scope:** Exact unsigned Windows package produced from `main` commit
`50f6e2f9321b4441830aee953809d879f099e267`

**Candidate:** `0.1.0-ci.52`

**Status:** Internal clean-machine package evidence; not a signed release candidate or independent
release sign-off

## Provenance and artifact identity

GitHub Actions run
[`29563316747`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/29563316747)
passed and retained artifact `windows-candidate-50f6e2f9321b4441830aee953809d879f099e267`
as artifact ID `8400198059` until 2026-07-31. The downloaded artifact's
`ci-candidate.json` recorded:

- source ref `refs/heads/main`;
- workflow attempt `1`;
- repository `Hybrid-Solutions-Cloud/vault-prospector`;
- creation time `2026-07-17T07:35:11.1721635+00:00`;
- provenance-file SHA-256
  `2AE4DAADD7C2EEFD649563DEB0C4AE57BE463E1A3283F50801486B35720B30B3`.

The archive was downloaded from the successful workflow rather than rebuilt locally. Independent
host and guest hashing matched the provenance and all three checksum sidecars:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `VaultProspector-0.1.0-ci.52-win-x64.msi` | 45,719,552 | `ECCACD05695FC66375C4DECE37BCF78176DC1DE3A4D5E34B55620A1EA9AD88F6` |
| `VaultProspector-0.1.0-ci.52-winget-manifests.zip` | 1,843 | `7E45C5BE4A728C0581A9E2A600450969CDFA3D075314EC26C007141CCB7995F4` |
| `vault-prospector.0.1.0-ci.52.nupkg` | 3,380 | `17E8317AF421BF1F826A37E0C30FCF767EFD5EC8A1D21E96E07148C1985FE44F` |

The MSI was deliberately unsigned (`Get-AuthenticodeSignature` returned `NotSigned`), consistent
with the documented CI-validation-only boundary. Its Windows Installer properties were:

| Property | Value |
| --- | --- |
| Product name | `Vault Prospector` |
| Manufacturer | `Hybrid Solutions Cloud` |
| Product version | `0.1.52` |
| Product code | `{06832240-5898-4C1C-AA07-E19469783C5D}` |
| Upgrade code | `{6E0981B0-6AD5-4B72-8ACA-ECEE660951B2}` |
| Install context | Per-machine (`ALLUSERS=1`) |

## Environment and clean precondition

The candidate was exercised on `vp-win11-preview-test`, a Windows 11 Enterprise Evaluation 25H2
x64 VM, build 26200, with UEFI Secure Boot and virtual TPM enabled. Before installation there was no
Vault Prospector ARP entry, installed directory, Start-menu shortcut, or process. Existing
`%LOCALAPPDATA%\VaultProspector` data was copied to an isolated test backup so candidate-created
state could be removed and the original byte-identical state restored afterward.

## Installation, launch, and packaged UI

A silent per-machine MSI installation completed successfully. The verbose Windows Installer log
recorded both `Installation success or error status: 0` and `MainEngineThread is returning 0`; its
SHA-256 was `11CC66CBE3E22C129CA82B5B88F0BCD2DB057DC0EA9DAA13C7928413C0A8FEE6`.
Post-install inspection proved:

- exactly one ARP entry with version `0.1.52` and the expected product code;
- `C:\Program Files\Vault Prospector\VaultProspector.App.exe` and the packaged runtime existed;
- the installed app executable SHA-256 was
  `93DBB79415B85195DE2FC1D877674F152174A99A103862EC76A3DC6C0255C056`;
- `VaultProspector.App.dll` was 1,425,408 bytes with SHA-256
  `6F31E975A4905AB84C4D02313CFD143B1E0F2B135A176E2A34380A27FA24D97A`;
- the Start-menu shortcut existed at
  `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Vault Prospector\Vault Prospector.lnk`.

The application was launched from Windows Start search, not by directly invoking the executable.
The running process path resolved to the installed `Program Files` package. It opened at 960 by 640
and rendered the five Search, Identities, Workspaces, Settings, and About views.

Windows UI Automation selected each packaged view and measured every visible focusable element in
the empty state. At the default Windows text size, all measured targets met the WCAG 2.2 AA
24-by-24-pixel floor:

| View | Visible focusable elements | Minimum width | Minimum height | Undersized |
| --- | ---: | ---: | ---: | ---: |
| Search | 24 | 58 | 24 | 0 |
| Identities | 11 | 60 | 24 | 0 |
| Workspaces | 10 | 60 | 24 | 0 |
| Settings | 18 | 34 | 24 | 0 |
| About | 5 | 80 | 30 | 0 |

The UI Automation JSON SHA-256 was
`35094C3B84C8641E4288F719FE27BAB924FFE5E587AA8B6DA98EDBB14B13A7BC`.

## Real Windows accessibility settings

Windows High Contrast Black was enabled through the operating-system shortcut and confirmation
dialog. The packaged About view remained readable, and the focused Search selector presented
clearly visible white focus text on black. Representative private framebuffer hashes were:

- About in High Contrast Black:
  `FAD25201CC2E512750B153B361204E66C9ADC8EA1C52CAD04CBCCF60EC330FB0`;
- focused selector in High Contrast Black:
  `22124BFD5A88F0047F0E21A230D2D9155A4C80BAC294A1EC5671F9F1EF5708CF`.

High Contrast was then disabled and registry `Flags=126` was verified.

Windows **Text size** was next set to 200% with the real per-user `TextScaleFactor`, followed by a
complete sign-out and sign-in. The Start-menu-installed candidate was relaunched. Search and About
fit the 1024-by-768 desktop work area; every view remained selectable; About was already at its
bottom boundary and did not move on Page Down. The repeated UI Automation sweep reported:

| View | Visible focusable elements | Minimum width | Minimum height | Undersized |
| --- | ---: | ---: | ---: | ---: |
| Search | 24 | 77 | 30 | 0 |
| Identities | 11 | 80 | 30 | 0 |
| Workspaces | 10 | 80 | 30 | 0 |
| Settings | 18 | 34 | 30 | 0 |
| About | 5 | 80 | 30 | 0 |

The 200% UI Automation JSON SHA-256 was
`88E4900BB186BFB66D8D95829085FB6825C162F26889C47E76D7A7FED70F8248`.
Representative framebuffer SHA-256 values were:

| View | SHA-256 |
| --- | --- |
| Search at 200% text size | `F2C333441C517CF882C0BAEC2A088FC9AC83E11421F1CF838FABD764DE32766B` |
| About at 200% text size | `A12775B87935DC78C4BF421FF1ABFEE8EE8EF67089CE63C628CC3316E8634D05` |

`TextScaleFactor` was removed, another complete sign-out/sign-in applied the default, and its
absence plus High Contrast `Flags=126` were verified before installer cleanup.

## Repair, uninstall, and restoration

A forced MSI repair (`/fa`) returned process exit code `0`, retained one correct ARP entry, restored
the expected executable hash, and logged `Reconfiguration success or error status: 0`. The repair
log SHA-256 was `5A916741156B06F9CDCCF0AFF04E9C405311275E9F192F7BA2B8AF74E11BC226`.

Silent uninstall returned process exit code `0`; Windows Installer logged
`Removal success or error status: 0`. The uninstall log SHA-256 was
`7226C6346BB91E1DD8AACD42DCF984CBB57621A6D077A5D2C42AE56A6CCB3377`.
Post-uninstall inspection proved zero Vault Prospector ARP entries, no install directory, no
Start-menu shortcut, and no running application process. User-local state remained, matching the
installer's retained-data contract.

Candidate-created local state was removed and the pre-test backup restored. A recursive
relative-path, length, and SHA-256 inventory matched exactly; the canonicalized inventory SHA-256
was `7D2623DB8E300CC152510C532D4F3AFE542F7A92E6A5096D15546D416B3E4208`.
The two exact guest test roots were deleted. Final inspection reconfirmed no installation or
process, no test roots, default accessibility settings, and restored local data.

## Conclusions and remaining gates

This run closes the prior internal evidence gap for packaging the current accessibility
remediations and proves exact-CI-artifact silent install, Start-menu launch, repair, uninstall,
retained-data behavior, empty-state target sizes, High Contrast Black, and 200% Windows text size.

It does **not** pass P-09 or P-15 by itself. The package is unsigned and validation-only; the same
agent performed the run; upgrade and downgrade behavior came from the separate Preview.1-to-
Preview.2 lifecycle run; failed-update recovery remains; and the final signed candidate still needs
independent clean-machine validation. Accessibility gaps still include populated lists and dialogs,
NVDA and Narrator, full keyboard task transcripts, additional custom contrast palettes, real Entra
and Windows Hello surfaces, representative-user usability, and independent sign-off. WinGet and
Chocolatey installation paths were not exercised by this run.
