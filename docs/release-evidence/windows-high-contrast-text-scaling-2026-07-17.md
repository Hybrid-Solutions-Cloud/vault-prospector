# Windows High Contrast and 200% Text Scaling Validation — 2026-07-17

**Scope:** Unreleased Vault Prospector source candidate after `0.1.0-preview.2`

**Status:** Internal remediation and runtime evidence; not independent accessibility sign-off

## Environment

- Windows 11 Enterprise Evaluation 25H2 x64, build 26200
- clean Hyper-V guest `vp-win11-preview-test`
- 1024-by-768 physical framebuffer
- Secure Boot and TPM enabled
- self-contained `win-x64` validation build labeled `0.1.0-preview.3`; it was not published or represented as a release candidate
- application DLL SHA-256 `3D5C5A7E7F6A21114D2A89F010D045D2E8D08F26440FA67F0D123A754854495D`

## Baseline defects

With Windows **High Contrast Black** enabled through the operating-system shortcut, the selector placeholder rendered at approximately RGB `(74, 76, 74)` on black, or about 2.4:1. A focused selector rendered white text on the system teal selection color, approximately 3.91:1. The placeholder failed the 4.5:1 text target, and the selector focus treatment did not provide a reliable high-contrast text result.

At the real Windows text-size setting of 200%, the application still used fixed font sizes and selected its responsive layout from unscaled logical width. Text grew outside those assumptions, so headings and task content could clip or become unreachable even though the earlier 200% display-scaling remediation passed.

## Remediation

- Detect Windows High Contrast from Avalonia platform color values at startup and when the operating-system color mode changes.
- Apply theme-resource-aware foreground and background colors to focused selectors and their placeholder, content, and glyph.
- Use the system text-control foreground for High Contrast text-entry placeholders.
- Read the Windows per-user `TextScaleFactor`, constrain it to the documented Windows 100–225% range, and apply it to centralized application font-size resources before constructing the main window.
- Select the narrow layout from effective text-scaled width so enlarged text receives the same stacked and scrollable presentation as a physically narrow viewport.
- Permit the product title to wrap instead of clipping.
- Add regression tests for High Contrast markup, text-scale normalization, centralized font resources, and text-scale-aware responsive layout selection.

## High Contrast runtime results

The candidate was launched in Windows High Contrast Black and navigated with the real interactive desktop. The framebuffer confirmed:

- the selector placeholder changed to approximately RGB `(123, 125, 123)` on black, or about 4.96:1;
- a keyboard-focused selector changed to white on black, or 21:1, with a visible system focus boundary;
- a focused text entry retained a visible focus indication and readable text;
- Search, Identities, Workspaces, Settings, and About remained visible and navigable in the tested contrast scheme; and
- the styles use Windows theme resources rather than hard-coded High Contrast Black colors, preserving support for inverted and custom contrast schemes.

Representative framebuffer SHA-256 values:

| View | SHA-256 |
| --- | --- |
| Baseline High Contrast selector placeholder | `4C8FF51536E71EB673BB6C8EEEFC37A11275722FAFB04A07004464EE7AAE4D35` |
| Corrected High Contrast selector placeholder | `B9D3EB917869D5829CDFEB2C129FC0C6C8767C01A8A61B5F2B7AFDF0B63CB3E7` |
| Focused High Contrast text entry | `8E7FA87D70D5775845B42ADE8712A4F7184242A9AC95779FFE2987EB3BFAED2E` |
| Baseline focused selector | `4FA8C5D6E8B7D46F4C1671AE10369AD75B4AB8D901277E70863A8FD7D32CB6C7` |
| Corrected focused selector | `B375F5207C6D6FA9D962A822AD42C3AE9A6CD615BA86CE0F8FCF5350BA30EDBD` |
| Identities | `882E89210A3460C6E65B85DA4DAD15A71D0D86BEBD38716457030E3949C60C95` |
| Workspaces | `3D51362F6A4EDF7189CF916F2CEE7FF7E223442B52AA015CC9E95D14E40122A2` |
| Settings | `EEB67F12ADF0538CBA4F88D3A7A28432E1B5430C4DBCE91CD9F456A1516B9123` |
| About | `C3F0B97B4CA818399D0A4178E29A26B5820FF2556BBA65CBAA2C6AD08C9D13D0` |

Ratios are approximate framebuffer measurements using the WCAG relative-luminance formula. They validate the tested Windows scheme, not every user-defined High Contrast palette.

## 200% text-only scaling runtime results

Windows **Text size** was set to 200% through the real per-user `TextScaleFactor`, followed by a complete restart/sign-in. This is distinct from the separately recorded 200% display-scaling test. The corrected candidate applied the enlarged type before window construction and selected its stacked layout from effective text-scaled width. Hyper-V input scrolled each task region to both boundaries. The framebuffer confirmed:

- the complete product heading wraps and remains readable;
- all five tabs remain reachable;
- Search filters and the complete selected-object action region remain reachable;
- Identities reaches **Continue to Microsoft sign-in**;
- Workspaces reaches its selected-workspace actions and **Create** action;
- Settings reaches **Save settings**, **Purge all offline values**, and the telemetry disclosure; and
- About content fits within its scrollable region.

Representative framebuffer SHA-256 values:

| View | SHA-256 |
| --- | --- |
| Search representative state | `AA6C1C262C5A1C637CE21A16FFA334F66994D98471A3247466C259C6B725E132` |
| Identities top | `F5E3828D5B228F3D76F3653E1A3E2D1C9D8711B1B2C211C11E4ABE560DDF6136` |
| Identities bottom | `DAB57AF5EC151F441D49AFDBEFBA5368D7CC8EB3909B021BA839ADC181ADCAA3` |
| Workspaces top | `CE4ED0F05C3008994E5B2098127863EC5FD2A25526D4264AF403072FA4618A6B` |
| Workspaces bottom | `8A40FF290DE2F2B7AD70292CA150759EC034674C7C29999DC350D9DDB5AC2B8E` |
| Settings top | `9911421896305764A7181EF0F950A78EBFBE8192BED2CDA280818C552388A514` |
| Settings bottom | `E2D4B53686B0F052D25FE4BD68BE7B004E10D3D71E095AD3200D47D83C6AA6E5` |
| About top | `4194DEADAD0B4D42C89206E56FF9D92CD4F38A5C48E30D1093C5AD04D440B8D8` |
| About bottom | `540B2450FBBF8EB758A0090640D2EAA758DE054ABB838F1EE9E6E6BFD1BDB360` |

## Automated verification

After restoring all packages in locked mode, the repository's structured direct/transitive NuGet vulnerability gate found no known vulnerable packages. Formatting verification passed, the Release solution build completed with 0 warnings and 0 errors, and all seven test projects passed 75/75 tests. The application tests include 32 passing checks, including the new High Contrast and Windows text-scale regressions.

## Cleanup and limitations

High Contrast was disabled (`Flags=126`), `TextScaleFactor` was removed, the application process was stopped, the pre-test `%LOCALAPPDATA%\VaultProspector` data was restored, and the guest test roots were removed. The VM was left at its default desktop.

This evidence closes the internally observed High Contrast placeholder/focus defects and 200% Windows text-only scaling defect. A later [exact CI-packaged candidate run](ci-packaged-windows-candidate-2026-07-17.md) repeated High Contrast Black, 200% Windows text size, and empty-state target-size checks after MSI installation. The combined internal evidence does not prove every custom contrast palette, NVDA or Narrator output, complete keyboard-only task flows, populated/dialog targets, focus return from Entra or Windows Hello, usability with representative users, final signed-candidate behavior, or independent accessibility review. P-15 therefore remains in progress.
