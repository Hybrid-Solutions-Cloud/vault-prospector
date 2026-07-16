# Research Spike: Key Vault metadata enumeration

- **Status:** Planned
- **Owner:** Unassigned
- **Created:** 2026-07-16

## Objective

Validate the Azure SDK operations and permissions required to enumerate secret, key, and certificate metadata without retrieving sensitive values.

## Questions

- Which list operations expose names, tags, versions, enabled state, and expiry?
- Which operations require data-plane permissions?
- Can keys and certificates reveal sensitive material through metadata APIs?
- How are deleted, disabled, inaccessible, and throttled objects represented?
- What are the practical costs of indexing versions?

## Constraints

- Do not use production secrets.
- Do not commit tenant-specific identifiers or tokens.
- Record exact SDK and operating-system versions.
- Include security and user-experience implications.
- Prefer primary vendor documentation and reproducible tests.

## Deliverable

A proof of concept and a least-privilege permissions matrix.

## Findings

_To be completed._

## Recommendation

_To be completed._

## Decision impact

List ADRs that should be accepted, changed, or superseded.
