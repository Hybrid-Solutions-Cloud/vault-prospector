# Desktop UI comparative research and design synthesis

**Method:** Comparative desk research and internal task analysis  
**Participants:** 0; representative-user research is scheduled, not fabricated  
**Date:** 2026-07-23  
**Researcher:** Vault Prospector maintainers

## Executive summary

Password-manager interfaces consistently optimize for fast search inside a clearly selected vault,
account, or collection. Enterprise secret-management tasks add a boundary that consumer password
managers rarely need: the operator must always know which identity, tenant, subscription, and vault
will authorize an action. Windows guidance favors predictable adaptive navigation, standard
keyboard behavior, explicit accessible names, and a full-page settings destination.

The initial recommendation is the **Source-first** concept because it keeps the authorization path
and read-only state visible while preserving fast local search. This is a hypothesis, not a final
selection. It must be tested against the Search-first, Guided tasks, and Operations console
concepts with representative Windows users and assistive technologies.

## Sources reviewed

- [Bitwarden Password Manager web app](https://bitwarden.com/help/getting-started-webvault/) —
  all-items landing view, filters, favorites, folders, and recognizable item names.
- [Bitwarden vault timeout](https://bitwarden.com/help/vault-timeout/) — explicit distinction
  between lock, which retains offline vault data, and logout, which removes local vault data.
- [1Password search](https://support.1password.com/search-1password/) — search scoped to the
  selected account or collection with an explicit all-accounts option.
- [KeePassXC interface overview](https://keepassxc.org/docs/KeePassXC_UserGuide) — persistent
  group/search navigation, entry list, settings separation, compact mode, and keyboard shortcuts.
- [Microsoft NavigationView guidance](https://learn.microsoft.com/en-us/windows/apps/design/controls/navigationview)
  — adaptive top/left navigation, shallow hierarchy, visible settings placement, and responsive
  breakpoints.
- [Microsoft Windows accessibility overview](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview)
  — UI Automation, accessible names, keyboard operation, screen readers, contrast, and text
  requirements.
- [Microsoft settings guidance](https://learn.microsoft.com/en-us/windows/apps/design/app-settings/guidelines-for-app-settings)
  — settings as a stable navigation destination with grouped full-page content.
- [WCAG 2.2](https://www.w3.org/TR/WCAG22/) — reflow, focus order/appearance, and target-size
  criteria used by the release gates.
- [Microsoft Key Vault security guidance](https://learn.microsoft.com/en-us/azure/key-vault/general/secure-key-vault)
  — explicit control-plane/data-plane separation and least-privilege access.

## Observations and interpretations

| Observation from source/task analysis | Interpretation for Vault Prospector |
| --- | --- |
| Search is the fastest path in established password managers. | Search must be focused on launch and remain one action away from any top-level destination. |
| Search scope is tied to an account, vault, or collection. | Identity/workspace scope must be visible beside the query, not hidden in Settings. |
| Lock and logout have materially different local-data effects. | Unlock, reauthenticate, disable identity, remove identity, and archive/reset need distinct language and UI. |
| KeePassXC uses navigation + entry list + detail and offers compact density. | A three-region layout is a credible expert pattern, but density must adapt for smaller windows and text scaling. |
| Windows guidance favors shallow adaptive navigation and standard controls. | Replace the growing top-tab form with an adaptive navigation shell and separate page view models. |
| Key Vault has separate management and data planes. | Permission summaries must not collapse “vault visible,” “metadata list,” “value read,” and “write policy” into one status. |
| Secret reveal and copy are high-risk, short-lived actions. | The detail view should show source, version, expiry, verification boundary, and timer before/after retrieval. |

## Key themes

### 1. Search is primary, source context is non-negotiable

The fastest flow is query → inspect source → verify → reveal/copy. Unlike a consumer vault, the
same object name can appear through multiple identities and tenants. Search results and object
details therefore need a stable source label and cannot rely on color alone.

### 2. Progressive disclosure reduces accidental high-risk actions

Metadata is safe to browse offline; values are not. The UI should show object metadata and access
status first, then place reveal/copy behind a clearly described Windows-verification boundary.
Write operations, if independently approved later, belong in a separate elevated task surface.

### 3. Infrequent setup and recovery need guided tasks

Identity connection, subscription/vault scope, local-data recovery, and workload credentials are
infrequent and security-sensitive. A stepwise flow is more appropriate than exposing every field
in the same card.

### 4. Expert density and accessibility must coexist

Administrators may need many vaults and identities visible at once, but small windows, 200% text,
High Contrast, keyboard traversal, Narrator, and NVDA cannot be afterthoughts. Density should be an
optional presentation, not a different information model.

## Concepts delivered

The interactive prototype at
[`vault-prospector-ui-concepts`](vault-prospector-ui-concepts/README.md) contains synthetic data and
four switchable structures. Each includes Setup, Search, Secret reveal, and Settings.

| Concept | Primary hypothesis | Main risk |
| --- | --- | --- |
| A · Source-first | Persistent identity/tenant and read-only state prevent source confusion. | Navigation uses more horizontal space. |
| B · Search-first | A command-like search surface minimizes retrieval time for frequent users. | Source context can become secondary. |
| C · Guided tasks | Numbered task navigation reduces setup/recovery errors for infrequent users. | Frequent search may feel slower. |
| D · Operations console | Dense status supports multi-identity administrators. | Cognitive load and accessibility regressions. |

## Insights to opportunities

| Insight | Opportunity | Impact | Effort |
| --- | --- | --- | --- |
| Source ambiguity is a product-specific critical risk. | Persistent source badge and source column/detail. | High | Medium |
| Current identity page combines too many unrelated tasks. | Separate Connections, Discovery scope, and Workspaces pages. | High | High |
| Search remains the dominant repeated task. | Global search focus/shortcut and saved scoped filters. | High | Medium |
| Permission language can overclaim. | Structured management/list/read/write rows with “not evaluated” states. | High | Medium |
| Recovery is rare but consequential. | Dedicated recovery flow isolated from ordinary Settings. | High | Medium |
| Expert and accessibility needs differ in density, not semantics. | Comfortable/compact display preference using the same UI Automation structure. | Medium | Medium |

## Initial recommendation

Use **Source-first** as the baseline for representative-user testing, borrowing the global search
speed from Search-first and the separate setup/recovery flows from Guided tasks. Do not implement
the Operations console density as the default. Keep it as a future optional compact mode only if
tests show administrators benefit without losing keyboard and screen-reader clarity.

## Questions for further research

- Can users correctly predict which identity will retrieve a selected value?
- Do users understand “visible,” “metadata list allowed,” “value read not tested,” and “write
  disabled” without explanation?
- Is workspace scope understood as organization rather than access control?
- Which close/background wording avoids confusing lock with logout or exit?
- Do keyboard and screen-reader users prefer persistent navigation or a search-first command
  surface?
- Does the selected design remain usable with 20 identities, 200 subscriptions, and thousands of
  indexed objects?

## Methodology limitations

This document synthesizes official product and platform documentation plus internal task analysis.
It contains no participant observations, quotes, telemetry, support-ticket sample, or usability
measurements. It therefore cannot satisfy the representative-user exit criterion by itself.
Results from the protocol below must be recorded separately and may overturn the initial
recommendation.
