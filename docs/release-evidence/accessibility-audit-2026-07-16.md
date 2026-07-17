# Preliminary Accessibility Audit — 2026-07-16

**Scope:** Vault Prospector Release build, first-run identity screen

**Standard used:** WCAG 2.2 AA desktop-oriented review

**Status:** Partial internal evidence; not an independent conformance assessment

## Summary

The first-run content exposes meaningful names and roles through Windows UI Automation and permits keyboard entry into the conditionally visible custom client-ID field. The unreleased source candidate assigns explicit automation names to every text-entry, selector, list, and numeric control and exposes application status changes as a polite live region; markup regression tests enforce those semantics. A live 200% display-scaling run found that first-run guidance inherited black text on its dark green panel; the actual foreground is now fixed to the verified white color. The original 1180-by-760 startup window and its later 900-by-620 minimum both clipped controls under tested Windows work areas. The candidate now fits the scaled work area, applies the separate Windows text-size preference through centralized font resources, adapts from effective text-scaled width, and keeps all five tabs plus the inspected task boundaries reachable at both 200% display scaling and 200% text-only scaling. High Contrast testing also exposed and verified remediation of selector placeholder and focus contrast defects.

P-15 does not pass from this review. Complete keyboard behavior, actual assistive-technology output, populated/dialog target-size sampling, all custom contrast palettes, packaged-candidate behavior, representative-user usability, and every core task remain unverified. The empty-state identity action focus defect found during the initial run was remediated and passed a Windows keyboard retest.

## Findings

| ID | Area | Criterion | Severity | Evidence and required action |
| --- | --- | --- | --- | --- |
| A11Y-01 | First-run keyboard path | 2.1.1, 2.4.3 | Pass for tested path | From the Identities tab, Tab reaches the organization-registration checkbox; Space reveals the custom client-ID field; the next Tab focuses it. Continue through sign-in, cancellation, recovery, and return focus without automating credentials. |
| A11Y-02 | Control names and live regions | 3.3.1, 3.3.2, 4.1.2 | Implemented and regression tested; assistive-technology validation pending | Every text-entry, selector, list, and numeric control has an explicit automation name. The global error region is assertive and contains an error, safe explanation, and recovery action; application status is a polite live region. Markup tests prevent removal of these semantics and of the corrected first-run foreground. Validate actual announcements and verbosity in NVDA and Narrator. |
| A11Y-03 | Empty-state action focus | 2.4.3, 3.2.4 | Remediated and tested | Before remediation, keyboard focus reached **Sync selected** and **Remove identity** even though those actions could not complete useful work. Both are now reported disabled by UI Automation without a selected identity and are skipped by Tab; the observed sequence proceeds from the Identities tab to the registration checkbox, friendly-label field, and sign-in button. A regression test covers no selection, selected identity, busy state, and removal of selection. |
| A11Y-04 | Target size | 2.5.8 | Empty-state AA surface measured and remediated; dynamic sampling remains | Windows UI Automation measured every authored focusable control rendered across all five empty-state tabs at default scaling. Four numeric stepper buttons failed at 34x22; a 26-pixel control minimum raised them to 34x24. Every other rendered target was already at least 24 pixels in both dimensions. Populated list items, combo popups, dialogs, and live Entra/Hello surfaces remain to sample. The 44x44 criterion in 2.5.5 is Level AAA enhanced guidance, not the AA floor. See [target-size validation](windows-target-size-2026-07-17.md). |
| A11Y-05 | Screen reader and security prompts | 1.3.1, 4.1.2 | Major evidence gap | No NVDA or Narrator transcript exists for onboarding, Entra handoff/return, Windows Hello, errors, reveal masking, cache state, or purge confirmation. Test with real assistive technology on a clean supported machine. |
| A11Y-06 | Reflow, scaling, and contrast modes | 1.4.4, 1.4.10, 1.4.11 | Observed defects internally remediated; major evidence gaps remain | At 200% display scaling on the clean 1024x768 Windows 11 guest, the 900x620 minimum made most content unreachable. Source now fits the scaled work area and stacks task panels with vertical navigation. A separate real 200% Windows text-size run confirmed centralized font scaling, title wrapping, and top-to-bottom reachability across all five tabs. High Contrast Black testing improved the selector placeholder from approximately 2.4:1 to 4.96:1 and focused selector text from approximately 3.91:1 to 21:1 while using system theme resources. Retest a fresh immutable MSI, custom contrast palettes, complete keyboard tasks, and independent review. See [200% display-scaling validation](windows-200-percent-scaling-2026-07-17.md) and [High Contrast and text-scaling validation](windows-high-contrast-text-scaling-2026-07-17.md). |
| A11Y-07 | Complete core-task usability | 2.1.1, 2.4.3, 3.3.1 | Release blocker | Structured tests must cover identity connection/removal, sync/cancel, search/filter, reveal, copy, cache/open/purge, settings, uninstall/exit, and failure recovery with representative users. |
| A11Y-08 | Context-dependent command state | 2.1.1, 2.4.3, 3.2.4 | Implemented and unit tested; runtime retest pending | Command availability now follows identity/workspace/result selection, secret object type, offline-cache policy, non-empty workspace name, and busy state. Selected filters reset and disable when their context disappears; stale result and identity selections are reconciled after refresh. The app test suite covers the state matrix, and the clean Windows run confirmed the empty-identity state plus selected-object action reachability. A complete runtime focus-state audit remains required; do not treat this row as runtime-passed yet. |

## Fixed-color contrast checks

| Element | Foreground | Background | Ratio | Required | Result |
| --- | --- | --- | ---: | ---: | --- |
| First-run panel text | `#FFFFFF` | `#123F36` | 11.73:1 | 4.5:1 | Pass |
| Header text | `#FFFFFF` | `#073B34` | 12.47:1 | 4.5:1 | Pass |
| Error-panel body | `#FFFFFF` | `#3F1616` | 15.70:1 | 4.5:1 | Pass |
| Error icon | `#FCA5A5` | `#3F1616` | 8.27:1 | 4.5:1 | Pass |
| Recovery text | `#FECACA` | `#3F1616` | 10.85:1 | 4.5:1 | Pass |
| Error border | `#DC6B6B` | `#3F1616` | 4.75:1 | 3:1 | Pass |

Ratios use the WCAG relative-luminance formula. Dynamic theme colors still require inspection in every supported Windows theme.

## Required completion evidence

1. Continue auditing and testing command enabled/focus states beyond the remediated identity actions.
2. Keyboard-only transcripts for every core task and failure recovery path.
3. NVDA and Narrator results, including focus return from Entra and Windows Hello surfaces.
4. Retest the corrected adaptive viewport, High Contrast behavior, focus visibility, and text-only scaling in a fresh immutable packaged candidate; exercise additional custom contrast palettes.
5. Sample target sizes in populated results/lists, combo popups, confirmation dialogs, and live authentication/verification surfaces.
6. Independent accessibility/usability reviewer sign-off with all blocking findings closed.
