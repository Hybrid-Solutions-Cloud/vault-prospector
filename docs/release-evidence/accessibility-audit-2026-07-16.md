# Preliminary Accessibility Audit — 2026-07-16

**Scope:** Vault Prospector Release build, first-run identity screen

**Standard used:** WCAG 2.1 AA desktop-oriented review

**Status:** Partial internal evidence; not an independent conformance assessment

## Summary

The first-run content renders at the default 1180-by-760 window size, exposes meaningful names and roles through Windows UI Automation, and permits keyboard entry into the conditionally visible custom client-ID field. The newly introduced fixed colors exceed the applicable contrast thresholds.

P-15 does not pass from this review. Complete keyboard behavior, actual assistive-technology output, scaling, target-size measurement, high-contrast behavior, and every core task remain unverified. The left identity action group also permits focus on actions that have no useful effect before an identity is selected; this should be remediated and retested.

## Findings

| ID | Area | Criterion | Severity | Evidence and required action |
| --- | --- | --- | --- | --- |
| A11Y-01 | First-run keyboard path | 2.1.1, 2.4.3 | Pass for tested path | From the Identities tab, Tab reaches the organization-registration checkbox; Space reveals the custom client-ID field; the next Tab focuses it. Continue through sign-in, cancellation, recovery, and return focus without automating credentials. |
| A11Y-02 | Control names and live errors | 3.3.1, 3.3.2, 4.1.2 | Pass for inspected controls | UI Automation exposes the checkbox, `Custom Microsoft Entra application client ID`, friendly-label edit, and sign-in button. The global error region is assertive and contains an error, safe explanation, and recovery action. Validate announcements in NVDA and Narrator. |
| A11Y-03 | Empty-state action focus | 2.4.3, 3.2.4 | Major | Before an identity exists, keyboard focus reaches **Sync selected** and **Remove identity**, although those actions cannot complete useful work. Disable unavailable actions through command state, ensure disabled controls leave the Tab sequence, and retest after selection/removal. |
| A11Y-04 | Target size | 2.5.5 review target | Major evidence gap | Several desktop buttons and checkboxes visually appear below 44-by-44 logical pixels. Measure every interactive target, document the desktop exception/rationale where applicable, and enlarge blocking targets. |
| A11Y-05 | Screen reader and security prompts | 1.3.1, 4.1.2 | Major evidence gap | No NVDA or Narrator transcript exists for onboarding, Entra handoff/return, Windows Hello, errors, reveal masking, cache state, or purge confirmation. Test with real assistive technology on a clean supported machine. |
| A11Y-06 | Reflow, scaling, and contrast modes | 1.4.4, 1.4.10, 1.4.11 | Major evidence gap | Test Windows text scaling and display scaling through 200%, minimum window size, High Contrast themes, and focus visibility without clipping or loss of function. |
| A11Y-07 | Complete core-task usability | 2.1.1, 2.4.3, 3.3.1 | Release blocker | Structured tests must cover identity connection/removal, sync/cancel, search/filter, reveal, copy, cache/open/purge, settings, uninstall/exit, and failure recovery with representative users. |

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

1. Remediation commits and regression tests for command enabled/focus states.
2. Keyboard-only transcripts for every core task and failure recovery path.
3. NVDA and Narrator results, including focus return from Entra and Windows Hello surfaces.
4. Default, 200% scaling, minimum-size, and High Contrast screenshots with defect tracking.
5. Measured target sizes and documented exceptions.
6. Independent accessibility/usability reviewer sign-off with all blocking findings closed.
