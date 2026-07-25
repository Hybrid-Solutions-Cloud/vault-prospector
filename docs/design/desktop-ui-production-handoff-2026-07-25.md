# Vault Prospector desktop UI production handoff

## Direction

Use **Compass** as the production baseline: calm, guided, and progressively disclosed. Borrow the
persistent workspace/source context from **Atlas**. Command Center remains a future optional compact
density, not a separate product shell.

The implementation must reuse existing Avalonia commands, verification boundaries, cancellation,
redaction, encrypted persistence, and enterprise-policy enforcement. The React study is a visual
and interaction reference only.

## Shell

| Token | Value | Usage |
|---|---:|---|
| `VaultColorCanvas` | `#EEF1EF` | Window and page canvas |
| `VaultColorSurface` | `#FFFFFF` | Cards and navigation surface |
| `VaultColorInk` | `#17201D` | Primary text |
| `VaultColorMuted` | `#63706A` | Secondary text |
| `VaultColorLine` | `#CBD5D0` | Dividers and control borders |
| `VaultColorAccent` | `#0B6957` | Primary actions and selected navigation |
| `VaultColorAccentStrong` | `#084E42` | Hover/pressed primary actions |
| `VaultColorAccentSoft` | `#DCEEE8` | Selected rows and contextual emphasis |
| `VaultColorWarning` | `#A25C10` | Isolated errors and action-required state |
| `VaultSpaceSm` | `8` | Inline control spacing |
| `VaultSpaceMd` | `16` | Card padding and section spacing |
| `VaultSpaceLg` | `24` | Page gutters and section separation |

- Desktop navigation is left aligned and grouped by daily use, setup, and management.
- Below the product header, a persistent context strip shows the active workspace, connected
  identity count, indexed-object count, and safe lock/readiness context.
- Below an effective width of 720 device-independent pixels after text scaling, navigation moves
  to the top and existing two-column content stacks vertically.
- The status region remains persistent, polite, keyboard reachable, and includes the only global
  operation progress/cancellation state.

## Core states

- Empty, loading, ready, partial success, actionable error, canceled, offline, locked, and
  policy-disabled states must use explicit text; color is supplemental.
- Long identity, tenant, subscription, vault, workspace, and object names wrap in details and
  truncate only in dense lists where the complete value remains available to accessibility APIs.
- Every disabled primary action must have adjacent text explaining the unmet prerequisite.
- Partial synchronization preserves usable results and exposes every isolated error with category,
  source-safe context, recovery guidance, and retry scope.

## Search and reveal

- Tenant, subscription, and vault filters are populated from encrypted discovered metadata.
- The all-sources option is explicit and filter options display friendly names without hiding the
  stable source identifier.
- Search results retain identity, tenant, subscription, and vault source context.
- Reveal remains an explicit action. Any verification grace is a short policy-controlled
  verification session, never plaintext caching or automatic retrieval.

## Accessibility

- Preserve logical focus after asynchronous completion, cancellation, and errors.
- Navigation, content, status, and dialogs must be reachable by keyboard in that order.
- Selected navigation and status must be announced without relying on color.
- Keep all authored controls at least 24 by 24 device-independent pixels.
- System High Contrast replaces product brushes; 200% display and text scaling must retain every
  task and avoid horizontal page scrolling.

## Delivery slices

1. Shared shell, tokens, responsive navigation, context strip, and populated search selectors.
2. Unlock and first-run identity setup, including remote-session policy-controlled alternatives.
3. Synchronization health, isolated-error details, and diagnostics/support bundle.
4. Search/reveal verification session and workspace/source context.
5. Guided browser fill, administration filtering, updates, notification-area behavior, and local
   data/recovery surfaces.
6. Visual baselines, keyboard/assistive-technology evidence, exact-package walkthrough, and
   product-owner approval.
