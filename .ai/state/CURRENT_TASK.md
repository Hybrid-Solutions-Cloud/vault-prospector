# Current task

Continue the canonical backlog after publishing and verifying the corrected C · Atlas Windows
Preview.

Confirmed on 2026-07-25:

- PRs #52 and #53 completed the persistent Atlas secure-unlock shell and corrected its protected
  main validation.
- Exact-main source is `866f434e6d39c647c34c86456fc7dac4827412f0`.
- HCS run `30184356857` passed portable, Windows candidate, protected-main, and full-history
  secret-scan gates.
- Exact candidate `0.3.0-ci.207` installed successfully and passed real machine-qualified
  current-account verification in Windows 11 RDP.
- Immutable `v0.3.0-preview.3` release run `30185620476` passed and published 16 assets to the
  public binary-only repository.
- Independent verification passed all 16 GitHub asset digests, five adjacent checksums, and five
  Sigstore bundles.
- The exact public MSI SHA-256 is
  `778456E2B8BEBE595092961BCA19221F1E034AD31911E57D332EAA01FAD72C78`.
- The exact public MSI installed as version `0.3.3`, preserved five local-state files, rendered the
  approved Atlas secure-unlock startup, did not prompt automatically, and opened the explicit RDP
  current-account prompt.
- Source remains private; only release binaries are public.

Next:

1. Merge the release-record documentation PR after governed validation.
2. Update ADO items with exact candidate/public evidence; close only items whose complete task and
   Acceptance Criteria sets are satisfied.
3. Continue remaining exact-public every-state accessibility/usability, live provider,
   independent-review, enterprise, Store, and GA backlog work in dependency order.
