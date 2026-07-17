# Preliminary Accessibility Audit — 2026-07-16

**Scope:** Vault Prospector Release build, first-run identity screen

**Standard used:** WCAG 2.1 AA desktop-oriented review

**Status:** Partial internal evidence; not an independent conformance assessment

## Summary

The first-run content exposes meaningful names and roles through Windows UI Automation and permits keyboard entry into the conditionally visible custom client-ID field. The newly introduced fixed colors exceed the applicable contrast thresholds. A later clean Windows 11 run found that the original 1180-by-760 startup window clipped controls in a 1024-by-768 work area. The unreleased source candidate now opens centered at 960-by-640, and its selected-object pane scrolls to every action.

P-15 does not pass from this review. Complete keyboard behavior, actual assistive-technology output, scaling, target-size measurement, high-contrast behavior, and every core task remain unverified. The empty-state identity action focus defect found during the initial run was remediated and passed a Windows keyboard retest.

## Findings

| ID | Area | Criterion | Severity | Evidence and required action |
| --- | --- | --- | --- | --- |
| A11Y-01 | First-run keyboard path | 2.1.1, 2.4.3 | Pass for tested path | From the Identities tab, Tab reaches the organization-registration checkbox; Space reveals the custom client-ID field; the next Tab focuses it. Continue through sign-in, cancellation, recovery, and return focus without automating credentials. |
| A11Y-02 | Control names and live errors | 3.3.1, 3.3.2, 4.1.2 | Pass for inspected controls | UI Automation exposes the checkbox, `Custom Microsoft Entra application client ID`, friendly-label edit, and sign-in button. The global error region is assertive and contains an error, safe explanation, and recovery action. Validate announcements in NVDA and Narrator. |
| A11Y-03 | Empty-state action focus | 2.4.3, 3.2.4 | Remediated and tested | Before remediation, keyboard focus reached **Sync selected** and **Remove identity** even though those actions could not complete useful work. Both are now reported disabled by UI Automation without a selected identity and are skipped by Tab; the observed sequence proceeds from the Identities tab to the registration checkbox, friendly-label field, and sign-in button. A regression test covers no selection, selected identity, busy state, and removal of selection. |
| A11Y-04 | Target size | 2.5.5 review target | Major evidence gap | Several desktop buttons and checkboxes visually appear below 44-by-44 logical pixels. Measure every interactive target, document the desktop exception/rationale where applicable, and enlarge blocking targets. |
| A11Y-05 | Screen reader and security prompts | 1.3.1, 4.1.2 | Major evidence gap | No NVDA or Narrator transcript exists for onboarding, Entra handoff/return, Windows Hello, errors, reveal masking, cache state, or purge confirmation. Test with real assistive technology on a clean supported machine. |
| A11Y-06 | Reflow, scaling, and contrast modes | 1.4.4, 1.4.10, 1.4.11 | Partially remediated; major evidence gap remains | A clean 1024x768 Windows 11 run found the Preview.2 default window outside the work area. Source now defaults to a centered 960x640 window, and Hyper-V framebuffer plus synthetic wheel input proved the selected-object pane can expose all six actions and Windows Hello guidance. The fix must ship in a fresh immutable candidate. Test Windows text/display scaling through 200%, minimum window size, High Contrast themes, and focus visibility without clipping or loss of function. See [clean Windows 11 validation](clean-windows-11-validation-2026-07-17.md). |
| A11Y-07 | Complete core-task usability | 2.1.1, 2.4.3, 3.3.1 | Release blocker | Structured tests must cover identity connection/removal, sync/cancel, search/filter, reveal, copy, cache/open/purge, settings, uninstall/exit, and failure recovery with representative users. |
| A11Y-08 | Context-dependent command state | 2.1.1, 2.4.3, 3.2.4 | Implemented and unit tested; runtime retest pending | Command availability now follows identity/workspace/result selection, secret object type, offline-cache policy, non-empty workspace name, and busy state. Selected filters reset and disable when their context disappears; stale result and identity selections are reconciled after refresh. Eleven app tests cover the state matrix. The broader Windows keyboard retest could not run because the desktop-control helper returned access denied; do not treat this row as runtime-passed yet. |

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
4. Retest the corrected default viewport, then capture 200% scaling, minimum-size, and High Contrast evidence with defect tracking.
5. Measured target sizes and documented exceptions.
6. Independent accessibility/usability reviewer sign-off with all blocking findings closed.
