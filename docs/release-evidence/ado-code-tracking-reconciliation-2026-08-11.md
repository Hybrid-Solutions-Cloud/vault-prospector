# ADO-to-code tracking reconciliation — 2026-08-11

## Scope

The audit compared all 124 nonterminal Epic, Feature, User Story, and Bug records in the private
`HCS -Vault Prospector` Azure DevOps project with the tagged Preview 8 source and repository-held
release evidence. ADO state was treated as planning metadata, not proof of implementation or
acceptance.

## Corrections applied

- Authored complete Acceptance Criteria for Feature AB#6165.
- Removed the duplicated criterion from User Story AB#5334 and retained four distinct schema and
  recovery requirements.
- Corrected AB#5296, AB#5298, and AB#5314 to identify their implementation as included in
  `v0.3.0-preview.8`, while keeping their live exact-package validation open.
- Assigned all Active work to dated Q3 iterations: 21 items in S4, 29 in S5, and 39 in S6.
- Returned two P4 items that were incorrectly Active to New/root backlog.
- Added future private-endpoint connectivity scope AB#6192–6225 to `pmo/backlog.md` and
  `pmo/plan.md` without asserting implementation.

## Final ADO verification

| Check | Result |
| --- | ---: |
| Total work items | 248 |
| Active | 89 |
| New | 92 |
| Resolved | 13 |
| Closed | 53 |
| Removed | 1 |
| Active at root iteration | 0 |
| Nonterminal product-backlog items | 124 |
| Nonterminal product-backlog items missing Acceptance Criteria | 0 |
| Target stale release descriptions | 0 |

The project and default team names are `HCS -Vault Prospector` and
`HCS -Vault Prospector Team`.

## Code and release conclusions

- The latest public tag is `v0.3.0-preview.8` at
  `2582a44e50155c80205370c6ec90b9d19eb7a006`.
- The repository has no dedicated Preview 8 release-evidence file. Preview 6 remains the latest
  release with retained exact artifact hashes, asset count, provenance, and independent download
  verification. No equivalent Preview 8 claim is inferred.
- AB#6192–6225 has no corresponding implementation, ADR, threat model, topology diagram, lab
  evidence, or packaged validation. It remains future planning scope.
- AB#6052 remains under-decomposed and should receive child Stories before scheduling.
- Cross-repository attribution remains unresolved until the other two product repositories and the
  approved ADO representation are identified.

## Local verification

- Browser extension: 6/6 tests passed; production build passed.
- Repository-wide `pwsh ./scripts/Build.ps1 -Configuration Release`: not run to completion because
  this workstation has .NET SDK `9.0.316` while `global.json` requires `10.0.302`.

This record does not close any item whose live, independent, signed-artifact, Store, participant,
or exact-package evidence remains open.
