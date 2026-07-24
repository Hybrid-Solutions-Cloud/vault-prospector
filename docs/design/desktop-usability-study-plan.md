# Desktop redesign usability study plan

**Status:** Ready to recruit  
**Prototype:** [`vault-prospector-ui-concepts`](vault-prospector-ui-concepts/README.md)  
**Target sample:** 8 participants

## Objectives

1. Select a navigation/search concept using observed task outcomes rather than visual preference.
2. Verify that identity, tenant, subscription, vault, and permission state are understood.
3. Identify setup, reveal/copy, recovery, and settings errors before production implementation.
4. Validate keyboard, Narrator/NVDA, High Contrast, text scaling, and target behavior.

## Participant mix

- 3 Azure administrators or platform engineers managing multiple tenants/subscriptions;
- 2 application operators who retrieve secrets but do not administer Key Vault;
- 1 security or identity administrator;
- 1 keyboard-only or motor-access participant; and
- 1 screen-reader participant using Narrator or NVDA.

At least two participants should be unfamiliar with Vault Prospector. Do not substitute internal
maintainers for the full sample.

## Session structure (60 minutes)

1. Consent, recording choice, and context interview — 8 minutes.
2. Baseline mental-model questions — 5 minutes.
3. Counterbalanced concept tasks — 35 minutes.
4. Concept comparison and confidence ranking — 7 minutes.
5. Wrap-up and missing-workflow prompt — 5 minutes.

## Tasks

| ID | Task | Success condition |
| --- | --- | --- |
| T1 | Connect an interactive Contoso identity without using Azure CLI context. | Participant identifies system-browser sign-in and explains the separate app identity. |
| T2 | Limit sync to Production but exclude one visible vault. | Correct subscription and vault scope are changed without interpreting exclusion as deletion. |
| T3 | Find `sql-admin-password` and identify the exact access path before reveal. | Correct identity, tenant, subscription, and vault are stated. |
| T4 | Explain whether sync proved secret-value read access. | Participant answers “no/not tested” and distinguishes metadata listing. |
| T5 | Reveal, hide, then copy the value. | Participant predicts Windows verification and clipboard clearing. |
| T6 | Create a workspace containing one tenant and one subscription with a 24-hour cache limit and clipboard disabled. | Correct links and secure policy are configured. |
| T7 | Choose what happens when the window closes while keeping values locked. | Participant selects the intended background behavior and distinguishes it from exit. |
| T8 | Recover from failed local encrypted state without silently deleting evidence. | Participant locates archive/reset guidance and understands confirmation/restart. |

## Measures

- task completion and critical-error rate;
- time on task (descriptive, not a speed contest);
- first action and navigation reversals;
- source-path accuracy before reveal;
- permission-model comprehension;
- confidence on a 1–5 scale after each task;
- SEQ (Single Ease Question) after T2, T3, T5, T6, and T8;
- keyboard focus loss, inaccessible name/role/state, and screen-reader announcement defects;
- qualitative preference with rationale after all concepts, never before task completion.

## Accessibility passes

- keyboard-only: logical focus order, visible focus, no trap, activation, cancellation, return focus;
- Narrator and NVDA: names, roles, states, list position, live status, complete safe errors;
- High Contrast: all text, focus, selection, disabled, warning, and status states;
- 200% Windows text size and 200% display scaling: reflow with no clipped task boundary;
- target measurement: WCAG 2.2 AA 24 CSS-pixel minimum or documented spacing exception.

## Analysis

Record observations separately from interpretations. Use affinity grouping across source-context,
search, permission comprehension, high-risk action, setup/recovery, and accessibility. Report
prevalence as counts (for example, 5 of 8), not “most.” The selected concept requires:

- no source-identity critical error;
- at least 7 of 8 successful search/reveal-path completions;
- at least 6 of 8 correct permission-model explanations;
- no unresolved keyboard or screen-reader blocker; and
- a documented rationale for every borrowed pattern from another concept.

## Evidence template

Create `docs/release-evidence/desktop-usability-YYYY-MM-DD.md` with participant identifiers P1–P8,
environment/assistive technology, task outcomes, anonymized quotes, defects, severity, changes,
retest results, and the final selection decision. Do not record tenant names, real vault/object
names, credentials, tokens, or secret values.
