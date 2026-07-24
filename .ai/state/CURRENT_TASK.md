# Current task

Finish the `0.2.0-preview.1` release follow-through in accordance with `pmo/backlog.md`,
`pmo/plan.md`, the project charter, and `.ai/state/GOAL.md`.

The readiness integration is merged. Exact `main` ADO CI build `284` passed all four jobs, and the
Key Vault-backed ADO release build `287` produced the 13 checksum-, SBOM-, and Cosign-verified
assets now published in the public binary repository. The exact public MSI passed all 27 isolated
Windows 11 installer lifecycle gates on 2026-07-24. WinGet PR `microsoft/winget-pkgs#407541` is
open and mergeable; Chocolatey has not ingested the package after two HTTP 504 upload responses.

Next:

1. require exact-merge `main` ADO build `290` to pass for merged PR `#24`;
2. merge this release-documentation update after ADO validation;
3. monitor WinGet moderation and Chocolatey service recovery without claiming catalog acceptance;
4. update ADO work item `AB#5095`; and
5. confirm completion of the asynchronous deletion of the temporary HCS Windows fallback resource
   group; its two temporary Key Vault credentials are already soft-deleted.

GA remains open for the named live Azure/CyberArk, physical-device, independent security/legal,
representative usability/accessibility, trusted Windows signing, operational exercise, and
stability-window gates.
