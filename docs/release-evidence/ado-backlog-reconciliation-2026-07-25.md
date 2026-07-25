# Azure DevOps backlog reconciliation — 2026-07-25

## Scope

- Organization: `https://dev.azure.com/hybridcloudsolutions`
- Project: `Hybrid Solutions Cloud - Vault Prospector`
- Project ID: `51cf361f-78a7-4a0d-8804-25cb4887361b`
- Work items audited: 137
- Governing standard: HCS `work-items`

The audit compared every Epic, Feature, User Story, and Task with the canonical backlog, plan,
release-readiness matrix, repository source, test results, public release evidence, and current HCS
runner evidence.

## Corrections made

- Copied the existing three-or-more-item acceptance checklists from the 35 Epic and Feature
  descriptions into the formal Azure DevOps Acceptance Criteria field.
- Verified that every work item has a priority and only standard tags.
- Reclassified CyberArk and native mobile delivery as Priority 4 `future-roadmap`.
- Replaced paid Authenticode scope with the free Microsoft Store MSIX trust path. The item remains
  open for Partner Center identity, certification, Microsoft signing, and clean-device Store
  lifecycle evidence.
- Removed arbitrary GA calendar and participant quotas. The gate now requires exact-candidate
  workflow coverage, complete report disposition, zero unresolved blockers/high findings, and a
  named go or no-go decision.
- Closed AB#5095 after all five child Tasks became terminal: four Closed and the bld-01 repair Task
  honestly Removed from product scope. The approved HCS Tier-4 fallback was used instead.
- Closed AB#5332 and AB#5333 after current pull-request and exact-main dependency and full-history
  secret scans passed.
- Closed acceptance-evidence Task AB#5309 after every AB#5308 criterion received an observable
  result. Parent AB#5308 remains New because representative-device and live-provider evidence
  still fails its own Acceptance Criteria.
- Added a dated implementation/evidence audit to every remaining open User Story. No item was
  closed solely because source or a prototype exists.

## Verified hierarchy result

| Check | Result |
| --- | --- |
| Non-Task items missing formal Acceptance Criteria | 0 |
| Items missing tags | 0 |
| Items missing priority | 0 |
| Closed parents with open children | 0 |
| New parents whose children are all Closed or Removed and whose own Acceptance Criteria pass | 0 |
| Live ADO pipeline definitions | 0 |

Final state counts are 15 New Epics, 20 New Features, 46 New User Stories, 3 Closed User Stories,
45 New Tasks, 7 Closed Tasks, and 1 Removed Task. The open counts are intentional: their own
acceptance criteria or a child acceptance-evidence Task remains open.

## Correctly open work

The remaining New items fall into explicit categories:

- current Windows capabilities whose source exists but whose formal acceptance criteria still
  require live Entra/Azure/Windows/browser, representative-user/accessibility, independent-review,
  enterprise-deployment, Store, operational, or legal evidence;
- AB#5364/5365, governed Azure writes, which are genuinely not implemented and remain behind their
  design and security gate;
- the free Microsoft Store MSIX channel, which is packaged but not yet reserved, certified,
  Microsoft-signed, or installed from the Store; and
- CyberArk and native mobile work, which are separate Priority 4 future-roadmap releases and do
  not block the Windows Preview or Windows GA decision.

Parents remain open whenever any child remains open. A terminal evidence Task does not by itself
close its parent: AB#5308 demonstrates the required exception because its automated evidence is
recorded but its representative-device and live-provider Acceptance Criteria remain unmet.
