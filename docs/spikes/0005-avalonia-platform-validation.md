# Research Spike: Avalonia platform validation

- **Status:** Planned
- **Owner:** Unassigned
- **Created:** 2026-07-16

## Objective

Validate Avalonia UI as the application shell across Windows, macOS, and iOS.

## Questions

- Does MSAL interactive authentication integrate cleanly?
- Can native biometric and keychain APIs be invoked safely?
- Are accessibility and keyboard navigation sufficient?
- How mature are iOS navigation, lifecycle, and packaging?
- What is the startup time and binary size?
- Would a native SwiftUI iOS shell reduce risk?

## Constraints

- Do not use production secrets.
- Do not commit tenant-specific identifiers or tokens.
- Record exact SDK and operating-system versions.
- Include security and user-experience implications.
- Prefer primary vendor documentation and reproducible tests.

## Deliverable

A small application that authenticates, stores a protected test value, searches a local dataset, and packages for target platforms.

## Findings

_To be completed._

## Recommendation

_To be completed._

## Decision impact

List ADRs that should be accepted, changed, or superseded.
