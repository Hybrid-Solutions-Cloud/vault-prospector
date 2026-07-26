# Atlas Windows candidate evidence — 2026-07-25

## Current result

The corrected exact-main candidate passed the installed Windows 11 Remote Desktop walkthrough.
The production application now renders the product-owner-approved C · Atlas experience rather
than the legacy-derived content layout found in the first candidate.

This is exact CI-candidate evidence, not yet exact public-release evidence. The candidate must
still be rebuilt from a new immutable tag, published as `v0.3.0-preview.2`, downloaded from the
public binary-only repository, and repeated before the exact-public-package tasks can close.
The withdrawn `v0.3.0-preview.1` tag will not be moved or reused.

## Corrected candidate identity

- Source commit: `ae976be1d7a486aa26ba8ec70d52a48ad4bfa6ef`.
- Candidate version: `0.3.0-ci.201`.
- HCS GitHub Actions run:
  [30181586109](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30181586109).
- MSI SHA-256:
  `FED4F4E877498A56EB6AEE0D9C3A86E4761BDEABD5DFF788057C8BF063EF30C1`.
- Installed Apps version: `0.3.201`.
- Public release status: not released; this record covers an exact CI candidate.

The downloaded MSI matched its adjacent SHA-256 file before installation. The existing
`0.3.199` acceptance installation was upgraded in place so the walkthrough also exercised retained
per-user state and the real Windows Installer upgrade path.

## Authoritative HCS validation

All three jobs passed against the exact source commit:

- **Portable source validation** passed locked restore, formatting, build, platform-neutral tests,
  browser-extension and design-prototype checks, dependency inspection, PowerShell syntax,
  operational-readiness validation, and artifact upload.
- **Full-history secret scan** passed using the pinned Gitleaks tool.
- **Windows candidate** passed the exact Windows build and tests, performance/scale checks,
  MSI/MSIX and package-manager builds, installer and browser-host contracts, the complete clean
  installer lifecycle, legal/privacy/enterprise/operational evidence checks, and artifact upload.

The authoritative build and package operations ran only on HCS-managed runners. The operator
workstation orchestrated the workflow, verified the downloaded artifact, and hosted the isolated
Hyper-V acceptance guest.

## Installed Windows 11 Remote Desktop walkthrough

The exact MSI was installed in the retained Windows 11 Enterprise 25H2 Generation 2 Hyper-V guest
with Secure Boot and vTPM enabled. The application ran in an actual RDP user session.

| Check | Result |
| --- | --- |
| Upgrade from installed `0.3.199` to `0.3.201` | Passed; `msiexec` returned `0` |
| Machine-qualified current-account verification | Passed with `VP-WIN11-PREVIE\vp-test-admin` |
| Application process after verification | Passed; responsive in the active RDP session |
| Atlas safety header and **Lock now** action | Passed; readable light-on-dark presentation |
| Persistent workspace, identity, subscription, readiness context | Passed |
| Connections / first-run setup | Passed; Atlas guided workflow rendered |
| Search | Passed; Atlas search hierarchy and tenant/subscription/vault selectors rendered |
| Administration | Passed; two non-overlapping columns rendered |
| Workload-identity explanation and discovery controls | Passed |
| Workload-provisioning Preview warning | Passed; readable warning palette |
| Workspaces | Passed |
| Browser fill | Passed; **Setup check** rendered here rather than over Administration |
| Activity and support | Passed |
| Settings and updates | Passed; installed `0.3.0-ci.201` and readable machine-policy card |
| About | Passed; installed candidate identity rendered |
| Long navigation labels | Passed; **Activity and support** and **Settings and updates** are visible |
| Visual parity with approved C · Atlas production direction | Passed |

The disposable Windows account, its password, and any verification material are not stored in the
repository. No tenant credential, secret value, access token, customer identifier, or live
protected value was used in this visual walkthrough.

## Rendered evidence

The eight exact installed-screen captures are retained under
[`images/atlas-ci201`](images/atlas-ci201/):

- [Connections](images/atlas-ci201/vp-atlas-ci201-connections.png)
- [Search](images/atlas-ci201/vp-atlas-ci201-search.png)
- [Administration](images/atlas-ci201/vp-atlas-ci201-administration.png)
- [Workspaces](images/atlas-ci201/vp-atlas-ci201-workspaces.png)
- [Browser fill](images/atlas-ci201/vp-atlas-ci201-browser-fill.png)
- [Activity and support](images/atlas-ci201/vp-atlas-ci201-activity-support.png)
- [Settings and updates](images/atlas-ci201/vp-atlas-ci201-settings-updates.png)
- [About](images/atlas-ci201/vp-atlas-ci201-about.png)

## Acceptance disposition

This candidate is sufficient implementation and installed-rendering evidence for the Atlas
production-layout portions of AB#5571, AB#5574, AB#5589, AB#5591, and AB#5600. Tasks AB#5592 and
AB#5601 explicitly require the exact public package and remain open until `v0.3.0-preview.2` is
published and downloaded independently for the repeated walkthrough. Parent items remain open
until every child task and every Acceptance Criterion is complete.

The following separate live or independent matrices are not claimed by this record:

- two real Microsoft Entra identities and multi-identity busy-state recovery;
- live partial Azure synchronization with isolated failures and retry/export;
- consecutive live secret reveals;
- installed Chrome, Edge, and Firefox fill;
- tenant-scale service-principal discovery;
- populated search selectors sourced from live discovery; and
- complete keyboard, screen-reader, representative-user, and independent-review approval.

## Superseded failed candidate

The first exact candidate remains historical defect evidence:

- source `01c2b820c01b64a4ddb2d83d917ea385c7d3a74a`;
- HCS run
  [30175377767](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30175377767);
- version `0.3.0-ci.190`;
- MSI SHA-256
  `506B43A01D91A0C6437D60B04852EDD00031723C73DD76675B410131AEC80A8B`; and
- release run `30178225455`, cancelled before publication.

That candidate passed installer, RDP verification, updater, diagnostics, and notification-area
checks but failed product-owner visual review because the production content remained
legacy-derived. It must not be cited as Atlas visual-parity evidence.
