# Current task

Correct release scope and delivery governance, validate the exact result, and reconcile Azure
DevOps Boards with implementation evidence.

Confirmed on 2026-07-25:

- CyberArk and native mobile applications are future-roadmap work, not Windows GA blockers.
- The repo-specific Azure Container Apps GitHub runner is healthy with labels
  `self-hosted,linux,ubuntu-22.04,hcs`.
- Windows-only packaging uses the HCS Tier-4 ephemeral Azure VM with labels
  `self-hosted,windows,hcs,vault-prospector`.
- GitHub Actions owns CI/release execution; Azure DevOps remains the governed hierarchy for epics,
  features, user stories, and tasks.
- The free trusted Windows path is Microsoft Store–signed MSIX. Direct MSI/ZIP downloads remain
  explicitly unsigned with checksums, SBOM, and Sigstore evidence.
- ADO builds 284, 287, 290, and 295 and the 27/27 clean-Windows run remain historical evidence; they
  do not justify continuing ADO pipelines.

In flight:

1. restore GitHub workflows on HCS runners and retire ADO pipeline definitions;
2. add and validate reproducible MSIX packaging;
3. hide the unsupported CyberArk UI from the Windows release;
4. remove invented 30-day, evaluator-quota, and 14-day release blockers;
5. run local and HCS validation, merge the corrective PR, and validate `main`;
6. update every affected ADO work item and close only items whose tasks and acceptance criteria are
   fully evidenced.
