# Research Spike: Local encrypted storage

- **Status:** Planned
- **Owner:** Unassigned
- **Created:** 2026-07-16

## Objective

Evaluate practical encrypted-storage options for metadata and cached values on Windows, macOS, and iOS.

## Questions

- Is SQLCipher licensing and packaging acceptable?
- Should metadata use database encryption, field encryption, or both?
- How should the data-encryption key be protected on each platform?
- Can local unlock be required before key release?
- How do backups and device migration affect protected data?
- How can cache expiration and purge be verified?

## Constraints

- Do not use production secrets.
- Do not commit tenant-specific identifiers or tokens.
- Record exact SDK and operating-system versions.
- Include security and user-experience implications.
- Prefer primary vendor documentation and reproducible tests.

## Deliverable

A threat-informed recommendation with working encryption and key-protection prototypes on Windows and one Apple platform.

## Findings

_To be completed._

## Recommendation

_To be completed._

## Decision impact

List ADRs that should be accepted, changed, or superseded.
