# Current task

Finish the `0.2.0-preview.1` release follow-through in accordance with `pmo/backlog.md`,
`pmo/plan.md`, the project charter, and `.ai/state/GOAL.md`.

The readiness integration is merged. Exact `main` ADO CI builds `284` and `290` passed all four
jobs, and the
Key Vault-backed ADO release build `287` produced the 13 checksum-, SBOM-, and Cosign-verified
assets now published in the public binary repository. The exact public MSI passed all 27 isolated
Windows 11 installer lifecycle gates on 2026-07-24. WinGet PR `microsoft/winget-pkgs#407541` is
open and mergeable; Chocolatey has not ingested the package after two HTTP 504 upload responses.

Next:

1. merge this release-documentation update after ADO validation;
2. monitor WinGet moderation and Chocolatey service recovery without claiming catalog acceptance;
   and
3. close or update ADO work item `AB#5095` according to the external catalog state.

GA remains open for the named live Azure/CyberArk, physical-device, independent security/legal,
representative usability/accessibility, trusted Windows signing, operational exercise, and
stability-window gates.
