# Windows 200% Scaling Validation — 2026-07-17

**Scope:** Unreleased Vault Prospector source candidate after `0.1.0-preview.2`

**Status:** Internal remediation and runtime evidence; not independent accessibility sign-off

## Environment

- Windows 11 Enterprise Evaluation 25H2 x64, build 26200
- clean Hyper-V guest `vp-win11-preview-test`
- 1024-by-768 physical framebuffer
- Secure Boot and TPM enabled
- Windows per-user custom scaling set to 200% with `LogPixels=192` and `Win8DpiScaling=1`
- effective application work area approximately 512 by 360 logical pixels after the taskbar
- self-contained `win-x64` validation build labeled `0.1.0-preview.3`; it was not published or represented as a release candidate

## Baseline defect

The source at commit `09fc09f` still declared a 900-by-620 minimum window. At 200% scaling, Windows could not fit that logical size into the physical work area. The framebuffer showed the left, right, top, and bottom content outside the visible desktop, making core tasks unreachable.

Baseline framebuffer SHA-256:

`DB9F99BBCC276DAD6B59BDD392BA7C7C080251F4FFD77FDEEBC609733B11E903`

## Remediation

- Lowered the hard minimum to 320 by 300 logical pixels.
- Constrained initial width and height to the active screen's physical work area divided by its Windows scale factor.
- Selected a narrow layout below 720 logical pixels.
- Stacked search controls, metadata filters, results/details, identity panels, and workspace panels in the narrow layout.
- Added vertical task-region scrolling and horizontal scrolling only within the tabular search-result surface.
- Kept the normal two-column desktop layout at wider sizes.
- Added deterministic tests for 100% and 200% work-area calculations.
- Corrected first-run guidance to render with the verified white foreground on its dark green panel; the live run had exposed inherited black text that contradicted the earlier contrast assumption.

## Runtime results

The corrected candidate was launched through the real Windows Explorer desktop after a full sign-out/sign-in at 200%. Hyper-V synthetic mouse input scrolled each task region, and the framebuffer confirmed:

- the window fits the complete physical work area;
- all five tab headers remain available, with **About** wrapping onto a second line at this extreme width;
- Search controls stack vertically;
- the result surface and selected-object surface stack;
- all six selected-object actions and the Windows Hello guidance are reachable;
- the connected-identities panel and first-identity connection panel stack, and the Microsoft sign-in action is reachable;
- the first-run instructions render white on the dark green panel;
- the workspace list and create-workspace panel stack, and the create action is reachable;
- all Settings actions, including **Purge all offline values**, are reachable;
- About content remains vertically scrollable;
- the application generated no `.NET Runtime` or `Application Error` event during the run.

Representative framebuffer SHA-256 values:

| View | SHA-256 |
| --- | --- |
| Corrected 200% initial fit | `4AA5D95BAF532D22D22F47E4DB88D7C2A3616F51F19A2B4B82F44F4FC62FE0E8` |
| Search bottom actions and guidance | `84659B1CF4E3A5AE1885E06361AB5F3C289A109D02CAD9E26FF8CDCECE97ADB1` |
| Corrected first-run foreground | `7F63B8E954055F710126BB789A3C98F4A47196669B17EBCE763EB95F913E63A6` |
| Microsoft sign-in action | `53A26033F92AB8EBD77CA7B0CA7AF8E26ADBF199BF6703B8107BE4CF45181B65` |
| Create-workspace action | `A5F2F7AC15EB07DAE4CCDC45837AD39AD44BAF1906D5A489E4DD676588A4D11D` |
| Settings bottom actions | `05AB7FE67FAF956F7E5F8DA6D460E889E99D3134EA1AE271DE7EC9E58DD26C14` |
| About at 200% | `1316C34BCA42C0A94F11E1CD3F13EEF52423854FB1882460B40AA56F06560E48` |
| Restored 100% default window | `DAF666B4D72F6C9BD8A5E9C91A1C7CE3F2E11554ACFD4CEF94F06DC1A3787702` |

## Cleanup and limitations

The account was restored to default 100% scaling (`LogPixels` absent and `Win8DpiScaling=0`) through a full sign-out/sign-in. The corrected candidate was relaunched at 100%, then exited through Alt+F4 with no residual process. The exact `C:\VP-A11y` test root and host-side self-contained build were removed. Existing `%LOCALAPPDATA%\VaultProspector` state remained, matching the product's retained-data contract.

This evidence closes only the internal 200% display-scaling defect. A later [exact CI-packaged candidate run](ci-packaged-windows-candidate-2026-07-17.md) separately proved High Contrast Black, 200% Windows text size, and empty-state target-size behavior after MSI installation. Neither run proves NVDA or Narrator behavior, complete keyboard-only tasks, focus return from Entra or Windows Hello, usability with representative users, final signed-candidate behavior, or independent accessibility review.
