# Vault Prospector complete UI redesign study

This interactive prototype compares three complete desktop directions using the same synthetic
Vault Prospector workflow. It covers installation, first-run setup, and normal daily operation
rather than applying a new visual theme to the existing tabbed interface.

Open [`bundle.html`](bundle.html) directly in a browser. It is self-contained and does not require
Node.js or a local server.

## Design directions

| Direction | Intent | Best fit |
|---|---|---|
| **A · Compass** | Calm, guided, progressive disclosure with explicit next actions | Recommended default for most users |
| **B · Command Center** | Dark, compact, search-first operation with persistent health context | Power users and administrators |
| **C · Atlas** | Workspace and source context leads every screen | Consultants and multi-tenant customer environments |

The direction picker changes the complete product shell. The lifecycle picker keeps the selected
direction and opens the equivalent screen, allowing direct layout and interaction comparisons.
The product owner selected **C · Atlas** on 2026-07-25; the review bundle now opens on Atlas by
default while retaining Compass and Command Center for comparison.

## Lifecycle coverage

| Phase | Prototype screen | Walkthrough and backlog coverage |
|---|---|---|
| Start | Install | Package trust, installation options, update/reinstall data retention |
| Start | Secure unlock | Local Windows verification plus policy-controlled AVD/RDP alternative |
| Start | Connect identities | Multiple interactive accounts, guest tenants, readiness, clear next step |
| Start | Sync and health | Partial results, two vaults, 124 objects, three actionable isolated errors |
| Use | Find secrets | Populated tenant, subscription, and vault selectors from discovery |
| Use | Reveal safely | Ten-second reveal and policy-controlled verification grace without plaintext caching |
| Use | Workspaces | Plain-language local grouping, Azure/non-permission boundary, workspace safeguards |
| Use | Browser fill | Browser-supplied destination context and guided select-and-fill |
| Manage | Administration | Workload-identity explanation and customer-manageable principal filtering |
| Manage | Activity and support | Privacy-safe events, error details, support bundle, external log path |
| Manage | Settings and updates | In-app update, reveal grace, minimize to notification area, background policy |

## Traceability

The study incorporates the Preview.5 product-owner walkthrough and these ADO outcomes:

- AB#5569 — trusted in-application release updates
- AB#5570 — privacy-safe diagnostics and support bundles
- AB#5571 — approved installation, setup, and desktop experience
- AB#5572 — application-wide identity busy state
- AB#5573 — actionable isolated synchronization errors
- AB#5574 — shipped interface divergence from approved mockups
- AB#5575 — secure AVD and Remote Desktop unlock
- AB#5608 — policy-controlled reveal verification grace period
- AB#5609 — simplified browser setup and one-time fill
- AB#5610 — relevant customer-manageable service-principal discovery
- AB#5611 — minimize to the Windows notification area
- AB#5628 — populated search filter selectors

## Review method

1. Compare **Install**, **Connect**, and **Search** in all three directions.
2. Choose the direction that should establish the production shell.
3. Review **Reveal**, **Browser**, **Administration**, and **Activity** for security and support
   clarity.
4. Record elements to borrow from either non-selected direction.
5. Convert the selected direction into the production Avalonia handoff only after product-owner
   approval.

All organizations, identities, resource names, identifiers, values, and operational events in the
prototype are synthetic.

## Development

The prototype is isolated from the production desktop application:

```powershell
pnpm install
pnpm build
```

The committed `bundle.html` is the review artifact. The React source remains available for
iteration and later implementation handoff.
