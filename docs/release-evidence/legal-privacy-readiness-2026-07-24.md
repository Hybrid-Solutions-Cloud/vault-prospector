# Legal and privacy source-readiness evidence

**Date:** 2026-07-24  
**Gate:** G-09 — Legal and privacy approval  
**Status:** In progress; source controls pass, human approval remains absent  
**Implementation commit:** `96300e9`

## Implemented controls

- Generated `docs/legal/third-party-components.json` and `THIRD-PARTY-NOTICES.md` from every
  committed NuGet lock file plus the desktop-design npm lock file.
- Added an explicit override boundary for the sole package whose metadata has no license:
  `AvaloniaUI.DiagnosticsSupport 2.2.3`, Release-excluded and still approval-required.
- Added a CI contract that checks inventory drift, required disclosures, production telemetry
  package absence, iOS/Android privacy defaults, Windows package metadata, and truthful approval
  status.
- Added draft Apple, Google, WinGet, and Chocolatey metadata plus an open human-review record.
- Changed Windows packaging to fail on notice drift and include `LICENSE.txt`,
  `THIRD-PARTY-NOTICES.md`, and `PRIVACY.md`.

## Local verification

The exact implementation commit passed:

| Verification | Result |
| --- | --- |
| Generated inventory determinism | Same SHA-256 before/after regeneration; 236 exact package/version records |
| Source legal/privacy contract | 25 passed, 0 failed |
| Locked Release restore/build | Passed; 0 warnings, 0 errors |
| Desktop/shared automated tests | 343 passed, 0 failed |
| Disposable Windows package | MSI and ZIP `0.1.0-ci.920` built; 0 warnings, 0 errors |
| Packaged legal/privacy contract | 29 passed, 0 failed |
| ZIP disclosures | All three files present |
| MSI File table | All three files present with expected sizes |

Disposable local artifact hashes (not release artifacts):

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `VaultProspector-0.1.0-ci.920-win-x64.zip` | 101,780,785 | `E1FCAD0D18644A50964BF7110A3ACEE2D7F68740C7C6F044ABD107667E76662A` |
| `VaultProspector-0.1.0-ci.920-win-x64.msi` | 60,487,753 | `B01700FDB75068D4FE18D1C35D8FF4125A158430D1E1A6861F8D064CFAF18787` |
| `THIRD-PARTY-NOTICES.md` | 22,042 | `1FED4FA9AE77117309E88E6CCEAA68A5EF71DDBC9DF5E7D2EAF1D27F1A4C77F0` |
| `third-party-components.json` | 206,639 | `3C4B46BE3CBF33CDBB76B3286B664E5D873CC663494353F9EF2394B31704052B` |

## Truth boundary and remaining gates

This evidence proves deterministic source/package controls, not legal approval. G-09 cannot pass
until a named reviewer:

1. resolves the unknown diagnostics-package terms;
2. reconciles the exact signed candidates, SBOMs, packaged files, upstream licenses, copyright,
   NOTICE, and redistribution obligations;
3. approves and publishes a stable public privacy URL;
4. reconciles Apple privacy/export and Google Play Data safety declarations against signed builds
   and observed traffic; and
5. records the approver, decision date, artifact hashes, accepted exceptions, and corrective dates.

Hosted exact-head CI also remains pending while the GitHub organization rejects jobs before
execution because of its account spending limit.
