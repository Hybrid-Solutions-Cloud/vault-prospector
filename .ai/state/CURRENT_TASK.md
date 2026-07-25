# Current task

Finish the product-owner walkthrough corrections on branch `feature/desktop-ui-redesign`, close
acceptance-complete Azure DevOps items bottom-up, merge PR #46, publish an immutable binary-only
Preview release, verify that exact public package, and clean temporary test/runner resources.

Confirmed on 2026-07-25:

- Atlas is the approved production UI direction.
- CyberArk and native mobile applications remain future-roadmap work, not Windows release blockers.
- GitHub Actions performs builds and releases on HCS-managed runners. Azure DevOps remains the
  authoritative hierarchy for epics, features, user stories, and tasks.
- Source remains private. Only release binaries are published to
  `Hybrid-Solutions-Cloud/vault-prospector-releases`.
- The operator workstation does not perform authoritative builds, tests, packaging, or
  publication. It may orchestrate workflows and host an isolated Hyper-V acceptance guest.

Validated:

- PR #46 exact head is `01c2b820c01b64a4ddb2d83d917ea385c7d3a74a`.
- HCS GitHub Actions run `30175377767` passed portable validation, full-history secret scanning,
  and the Windows candidate, including all 432 Windows tests and 27 installer lifecycle checks.
- Exact MSI `0.3.0-ci.190` SHA-256 is
  `506B43A01D91A0C6437D60B04852EDD00031723C73DD76675B410131AEC80A8B`.
- The exact MSI passed Atlas installer and first-run walkthrough, real RDP current-account Windows
  verification, in-app update check, diagnostics/support bundle export, minimize-to-notification
  area, and locked restore from the notification icon.
- Support bundle SHA-256 is
  `2B590E49BB18C4BBA74C936C69295D150C857D300E217BD7547495CD7433411D`.

Next:

1. Commit candidate evidence and user/PMO documentation, then repeat governed exact-head CI.
2. Close only child items whose implementation, tests, and exact-candidate evidence are complete.
3. Merge PR #46, run exact-main validation, create the immutable Preview release, and verify public
   hashes/provenance plus the public MSI walkthrough.
4. Close eligible parent stories/bugs only after every Acceptance Criterion is satisfied.
5. Leave live identity, browser, service-principal-scale, independent review, Store signing,
   enterprise deployment, legal/privacy, and operational approvals open where evidence is absent.
6. Remove temporary runner/test access and stop the disposable VM without deleting it.
