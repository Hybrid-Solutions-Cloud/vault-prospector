# Windows Target-Size Validation — 2026-07-17

**Scope:** Unreleased Vault Prospector source candidate after `0.1.0-preview.2`

**Status:** Internal empty-state runtime evidence; populated and external surfaces remain

## Requirement

The accessibility audit now uses WCAG 2.2 AA. [Success Criterion 2.5.8, Target Size (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum), requires pointer targets to contain at least a 24-by-24 CSS-pixel area or satisfy a defined exception. The separate [2.5.5 Target Size (Enhanced)](https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced) 44-by-44 target is Level AAA guidance, not the AA release floor.

At default 100% Windows display and text scaling, one Avalonia logical pixel maps to one physical framebuffer pixel in the tested guest. This run therefore used 24 physical pixels as the minimum authored-control target dimension.

## Environment and method

- Windows 11 Enterprise Evaluation 25H2 x64, build 26200
- clean Hyper-V guest `vp-win11-preview-test`
- 1024-by-768 physical framebuffer
- Secure Boot and TPM enabled
- self-contained `win-x64` validation build labeled `0.1.0-preview.3`; it was not published or represented as a release candidate
- corrected application DLL SHA-256 `E2C044136C214DC01E463CE7A3977008092F89DDF39048DE3F89CC8350BB445D`

The candidate was launched through the logged-in Windows Explorer desktop. A PowerShell process in the same interactive session used Windows UI Automation to enumerate the Vault Prospector window and every rendered button, checkbox, combo box, edit, list, spinner, and tab item. It recorded each element's control type, enabled/focusable/off-screen state, and physical bounding rectangle. Each tab was selected through synthetic pointer input before a fresh capture.

This method measures the actual accessibility tree and target rectangle produced by Windows, Avalonia, the selected theme, and the candidate. It does not infer dimensions from XAML declarations or screenshot pixels.

## Baseline defect and remediation

Search, Identities, Workspaces, and About had no authored keyboard-focusable target below 24 pixels in either dimension. Settings exposed four focusable numeric stepper buttons—increment and decrement for cache lifetime and clipboard delay—at 34 by 22 pixels. The paired buttons abutted one another, so the spacing exception could not be used to establish an AA pass.

The application now gives every `NumericUpDown` a 26-pixel minimum height. Its Fluent template consequently renders each internal stepper button at 34 by 24 pixels. A markup regression test prevents removal of this minimum.

## Runtime results

| View | UIA elements | Focusable elements | Smallest focusable width | Smallest focusable height | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Search | 33 | 24 | 58 | 24 | Pass for rendered controls |
| Identities | 16 | 11 | 60 | 24 | Pass for rendered controls |
| Workspaces | 15 | 10 | 60 | 24 | Pass for rendered controls |
| Settings before remediation | 18 | 18 | 34 | 22 | Fail |
| Settings after remediation | 18 | 18 | 34 | 24 | Pass for rendered controls |
| About | 5 | 5 | 80 | 30 | Pass for rendered controls |

The search-result and task-region scroll bars also expose 16-pixel non-keyboard-focusable template buttons. They are unmodified framework controls and have equivalent mouse-wheel, track, and keyboard scrolling. The W3C criterion explicitly provides user-agent-control and equivalent-function exceptions; these template internals were not treated as authored primary targets.

Evidence hashes:

| Evidence | SHA-256 |
| --- | --- |
| Search UI Automation JSON | `9130F6DCA5D47B83E282B0B64E6A92153EFEB549E88F58E2F8A9C82D3DFF0ED9` |
| Identities UI Automation JSON | `2151A4E71FB540B6E722EE1F4E55F249FEA1BE807C022169449776BDFAD7D3DC` |
| Workspaces UI Automation JSON | `606905B8DB3B975316F484C85FF13904E059229B3877D4C94385BBC44C14FB9C` |
| Settings baseline UI Automation JSON | `EADC45B3630C1F38D62A749E486F907132C8D75758611B955BE781913410DDD6` |
| Settings corrected UI Automation JSON | `15BE84CC30857E783518560D8118503DB99A0CDFF635BEEC2770F0B3D1EDEDA0` |
| About UI Automation JSON | `C919E3B04A170F988A68B7DAAC456239571E3B1D34714EB4CE3893EAFE32ACEC` |
| Corrected Settings framebuffer | `F1BCDEF048F967DDD760EE271012644535ED3503D1A4C5DDA7AB38DDF28FEFB6` |

## Automated verification

After the runtime remediation, locked restore and formatting verification passed, the structured direct/transitive NuGet vulnerability gate found no known vulnerable packages, and the Release solution build completed with 0 warnings and 0 errors. All seven test projects passed 76/76 tests, including 33 application tests and the new numeric-target regression.

## Limitations

This run proves the AA target-size floor for authored focusable controls rendered in the five-tab empty-state candidate, including disabled commands. It does not yet measure populated result/list items, combo-box popup items, confirmation dialogs, Entra or Windows Hello surfaces, or every control under every supported scale/theme. Those states must be sampled during the complete keyboard, assistive-technology, authentication, and representative-user runs. The 44-by-44 Level AAA enhanced target remains desirable guidance, not a claimed conformance level.

A later [exact CI-packaged candidate run](ci-packaged-windows-candidate-2026-07-17.md) repeated the five-tab empty-state sweep after MSI installation at default Windows text size and 200% text size; no measured target was undersized. It does not expand coverage to the unrendered states listed above or replace independent review.

P-15 remains in progress.
