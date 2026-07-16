# Research Spike: Background synchronization

- **Status:** Planned
- **Owner:** Unassigned
- **Created:** 2026-07-16

## Objective

Determine what synchronization can run without user interaction across Windows, macOS, and iOS while respecting token, battery, platform, and Conditional Access constraints.

## Questions

- Can metadata refresh run in the background on iOS?
- What happens when authentication requires interaction?
- How should sync schedules avoid API throttling?
- Can sync be scoped by workspace and network state?
- How should the app communicate stale data?

## Constraints

- Do not use production secrets.
- Do not commit tenant-specific identifiers or tokens.
- Record exact SDK and operating-system versions.
- Include security and user-experience implications.
- Prefer primary vendor documentation and reproducible tests.

## Deliverable

A platform-specific synchronization strategy and fallback behavior.

## Findings

_To be completed._

## Recommendation

_To be completed._

## Decision impact

List ADRs that should be accepted, changed, or superseded.
