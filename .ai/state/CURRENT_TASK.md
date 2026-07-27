# Current task

Complete product-owner installed workflow validation of the published
`0.3.0-preview.6` Windows manual-test Preview, then update only the work items whose complete
Acceptance Criteria receive observable evidence.

Confirmed on 2026-07-27:

- Current public source is `8751df7f2a6c1014f3e51c4b570625364f9fb5f9`.
- Exact-main run `30229406561` passed the full HCS Windows candidate.
- Immutable tag `v0.3.0-preview.6` release run `30233704752` passed and published 16 assets.
- Independent public verification passed all five adjacent package checksums.
- The exact MSI SHA-256 is
  `1FAABD20B917C26B2150DA49A6F1558CE1DAE0F2FA6E215038FB876EC5F0040C`.
- PRs #60 and #61 merged the release and final ADO reconciliation records.
- The private Agile ADO project is correctly named `Vault Prospector`.
- The ADO hierarchy contains 198 items: 53 Closed, 1 Removed, and 144 open with named work or
  evidence. No open parent has only terminal children.
- GitHub Bug #62 and ADO mirror AB#5799 track the missing installer logo/red-X placeholder. The
  records are tracking-only; no fix has been attempted.
- The Tier-4 Windows resource group is deleted, no repository runner remains registered, and
  temporary build credentials are soft-deleted.
- Source remains private; only release binaries are public.

Next:

1. Record product-owner observations from the exact installed Preview.
2. Close validation Tasks and roll up Stories only when every child and Acceptance Criterion pass.
3. Continue live Azure/browser, accessibility/usability, independent-review, enterprise,
   Microsoft Store signing, and GA gates in plan order.
4. Keep mobile on its separate roadmap and CyberArk on the post-GA roadmap.
