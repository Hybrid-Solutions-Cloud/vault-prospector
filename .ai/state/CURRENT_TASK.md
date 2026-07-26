# Current task

Publish and verify the corrected C · Atlas Windows Preview from the exact governed source.

Confirmed on 2026-07-25:

- Atlas is the product-owner-approved production UI direction.
- PRs #47 through #50 replaced the legacy-derived layouts and corrected the installed-screen
  defects found during direct review.
- Exact-main source is `ae976be1d7a486aa26ba8ec70d52a48ad4bfa6ef`.
- HCS GitHub Actions run `30181586109` passed portable validation, full-history secret scanning,
  and the Windows candidate.
- Exact candidate `0.3.0-ci.201` MSI SHA-256 is
  `FED4F4E877498A56EB6AEE0D9C3A86E4761BDEABD5DFF788057C8BF063EF30C1`.
- The installed MSI passed machine-qualified current-account verification in a real Windows 11
  RDP session and rendered all eight production screens with the approved Atlas hierarchy.
- Administration contains the intended workload-discovery and provisioning-preview columns;
  Browser fill owns the Setup check; warning/policy cards and long navigation labels are readable.
- The failed `v0.3.0-preview.1` tag is immutable and must never be moved or reused.
- Source remains private. Only release binaries may be published to
  `Hybrid-Solutions-Cloud/vault-prospector-releases`.

Next:

1. Commit the corrected candidate evidence and PMO/readiness updates through a governed PR.
2. Close only Atlas child tasks whose implementation and candidate Acceptance Criteria are met.
3. Create new immutable tag `v0.3.0-preview.2` from the exact passing documentation merge.
4. Run the governed release workflow and independently verify every public binary, checksum,
   SBOM, and Sigstore provenance bundle.
5. Install the exact public MSI in the Windows 11 acceptance guest and repeat the Atlas/RDP
   walkthrough before closing exact-public-package tasks or parent items.
6. Continue the canonical backlog in dependency order; leave external live, accessibility,
   independent-review, Store, legal/privacy, and approval gates open where evidence is absent.
