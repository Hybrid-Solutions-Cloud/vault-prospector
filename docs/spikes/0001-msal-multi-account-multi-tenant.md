# Research Spike: MSAL multi-account and multi-tenant behavior

- **Status:** Planned
- **Owner:** Unassigned
- **Created:** 2026-07-16

## Objective

Determine the correct supported pattern for connecting multiple Microsoft Entra user identities, discovering resource tenants, handling guest access, selecting authorities, and retaining separate token-cache contexts across Windows, macOS, and iOS.

## Questions

- Can one public-client application retain multiple accounts reliably on every target platform?
- How should home-account identifiers and tenant profiles be persisted?
- How does interactive authentication behave for guest users and tenant-specific authorities?
- Which scopes are required for Azure Resource Manager and Key Vault data-plane calls?
- How are Conditional Access and interaction-required errors surfaced?
- Can accounts be removed individually from the token cache?

## Constraints

- Do not use production secrets.
- Do not commit tenant-specific identifiers or tokens.
- Record exact SDK and operating-system versions.
- Include security and user-experience implications.
- Prefer primary vendor documentation and reproducible tests.

## Deliverable

A console or minimal UI prototype that connects at least two identities, accesses at least two tenants, lists accounts, restarts successfully, and removes one account without affecting the other.

## Findings

_To be completed._

## Recommendation

_To be completed._

## Decision impact

List ADRs that should be accepted, changed, or superseded.
