# Vault Prospector desktop UI production handoff

## Direction

The product owner selected **Atlas** on 2026-07-25. Atlas is the production baseline: persistent
workspace and source context leads every screen for multi-tenant and customer-environment work.
Compass and Command Center remain comparison studies, not competing runtime shells.

The implementation must reuse existing Avalonia commands, verification boundaries, cancellation,
redaction, encrypted persistence, and enterprise-policy enforcement. The React study is a visual
and interaction reference only.

## Shell

| Token | Value | Usage |
|---|---:|---|
| `VaultColorCanvas` | `#F4F1EA` | Window and page canvas |
| `VaultColorSurface` | `#FFFDF8` | Cards and content surfaces |
| `VaultColorSurfaceAlt` | `#EEE9DF` | Secondary panels and grouped context |
| `VaultColorInk` | `#25231F` | Primary text |
| `VaultColorMuted` | `#6F6A61` | Secondary text |
| `VaultColorLine` | `#D4CDC0` | Dividers and control borders |
| `VaultColorLineStrong` | `#A99E8D` | Emphasized control borders |
| `VaultColorAccent` | `#9A412B` | Primary actions and selected navigation |
| `VaultColorAccentStrong` | `#7B3020` | Hover/pressed primary actions |
| `VaultColorAccentSoft` | `#F2DFD8` | Selected rows and contextual emphasis |
| `VaultColorGood` | `#27715B` | Ready and healthy states |
| `VaultColorGoodSoft` | `#DEEEE8` | Ready-state surfaces |
| `VaultColorWarning` | `#A26118` | Isolated errors and action-required state |
| `VaultColorNavigation` | `#EEE8DD` | Workspace tool navigation |
| `VaultColorContext` | `#E7DFD2` | Active workspace strip |
| `VaultColorHeader` | `#2C3737` | Product header |
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

## Installer implementation

The MSI uses the WiX `WixUI_InstallDir` interaction model so installation, upgrade, repair, and
removal retain native Windows Installer keyboard and accessibility behavior. Atlas dialog and
banner artwork carry the selected palette into setup. The completion screen explains same-account
local-state retention and offers an explicit, user-controlled launch action. Silent enterprise
installation remains supported and continues to create the Start menu shortcut.

## Automated visual baseline

`tests/VaultProspector.App.Tests/Baselines/atlas.visual-baseline.json` pins the approved Atlas
application resources, production window markup, reference screenshots, and installer artwork by
SHA-256. CI fails whenever one of those visual sources changes without an intentional baseline
review. Semantic accessibility tests separately verify automation names, live regions, keyboard
focus support, responsive layout, text scaling, and high-contrast resources.
