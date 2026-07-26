# Atlas secure-unlock Windows evidence — 2026-07-25

## Result

The exact `main` candidate passed the governed build, package, in-place installation, and real
Windows 11 Remote Desktop verification checks for the corrected C · Atlas secure-unlock shell.
Startup remained fail-closed without opening a Windows credential prompt automatically. Selecting
**Verify and continue** opened the current-account Windows Security prompt, and the supplied
machine-qualified account verified successfully in the active RDP session.

This record supplements the earlier eight-screen Atlas evidence. It covers the follow-up startup
and secure-unlock correction introduced by PRs #52 and #53.

## Exact candidate identity

- Source commit:
  `866f434e6d39c647c34c86456fc7dac4827412f0`.
- Pull requests: #52 and #53.
- HCS GitHub Actions run:
  [30184356857](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30184356857).
- Run number: `207`.
- Candidate version:
  `0.3.0-ci.207+866f434e6d39c647c34c86456fc7dac4827412f0`.
- Windows-candidate artifact ID: `8626599073`.
- Artifact archive digest:
  `BDE211AF53AA5863C1C3FDAE907297D1D1BC3148F073494244E8B1FAA8B9E88E`.
- MSI:
  `VaultProspector-0.3.0-ci.207-win-x64.msi`.
- MSI SHA-256:
  `6C7A448816776C31D3716202654D77AE2F298A2E015788D12DC0EA427D140C38`.
- Installed Apps version: `0.3.207`.
- Windows Installer product code:
  `{B6CD8B2C-E610-4857-A194-59A4EF7C444A}`.

## Governed build result

All four required execution boundaries passed against the exact source:

- portable source validation;
- Windows candidate build, tests, performance, packaging, installer/browser-host, lifecycle, and
  legal/privacy/enterprise/operational gates;
- full-history secret scan; and
- the protected-main workflow gate.

The authoritative build and package operations ran on HCS-managed runners. The operator
workstation only orchestrated the workflow, verified the artifact, and hosted the isolated
Hyper-V acceptance guest.

## Installed Windows 11 acceptance

The exact CI-produced MSI was copied into the retained Windows 11 Enterprise 25H2 acceptance
guest. Its guest-side SHA-256 matched the downloaded artifact before installation.

| Check | Result |
| --- | --- |
| In-place MSI upgrade | Passed; `msiexec` returned `0` |
| Installed product identity | Passed; `0.3.207` and exact informational version matched |
| Existing local-state files during upgrade | Passed; four files remained present |
| Startup in an active RDP session | Passed |
| Atlas grouped navigation and secure-unlock hierarchy | Passed |
| Automatic credential prompt at startup | Passed; no prompt opened |
| Explicit **Verify and continue** action | Passed; opened Windows Security |
| Machine-qualified current-account verification | Passed |
| Fail-closed local-data handling | Passed; unreadable DPAPI state was preserved and an explicit archive-not-delete recovery decision was required |

The unreadable local-data state was expected in this disposable guest because its Windows account
password had been rotated during acceptance-test credential hygiene. The application did not
silently replace, delete, or downgrade that state. The optional archive flow was not used as
release evidence.

## Rendered evidence

- [Atlas secure-unlock startup](images/atlas-ci207/vp-atlas-ci207-secure-unlock.png) —
  SHA-256 `40F77DACE1AA9E90654AAD609D2D7D957F9BAC99799B2DCB0D0296A41C812500`.
- [Successful RDP verification followed by preserved-data recovery](images/atlas-ci207/vp-atlas-ci207-rdp-verified-recovery.png) —
  SHA-256 `323E459A8C994EC55A5E999F454ECD1651D475E5602B020577736326A537578B`.

No tenant credential, access token, secret value, customer identifier, test-account password, or
verification material is present in either image or in the repository.

## Acceptance boundary

This evidence completes the exact-candidate startup and policy-controlled Remote Desktop
verification checks. It does not, by itself, close tasks that require a walkthrough of every
approved state from the exact public release, complete assistive-technology evidence, or
independent approval.
