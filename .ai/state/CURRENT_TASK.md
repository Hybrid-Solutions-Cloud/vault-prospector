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

Validated:

- PR #26 HCS run `30146204363` passed portable validation and full-history secret scanning.
- Local Windows build passed 371 tests plus MSI, MSIX, performance, legal/privacy, enterprise,
  browser, and operational gates.

Next:

1. merge PR #26;
2. provision the Tier-4 Windows runner for the queued exact-`main` Windows candidate;
3. validate `main` and remove the ephemeral Azure resources;
4. retire the live ADO pipeline definitions; and
5. reconcile every affected ADO work item, closing only items whose tasks and acceptance criteria
   are fully evidenced.
