# Session Handoff

## Current state

- Branch: `main`
- Published release: [`v0.1.0-preview.1`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/releases/tag/v0.1.0-preview.1)
- Vault Prospector is implemented as a Windows `win-x64` Avalonia desktop application with separated domain, application, infrastructure, platform, and Azure provider projects.
- Authentication uses MSAL public-client flows, per-identity client IDs, Windows Hello consent, DPAPI-protected key material, SQLCipher metadata, and explicit secret retrieval.
- The Apple and Google applications are intentionally deferred and recorded in the roadmap and backlog.

## Validation state

- Windows locked restore, formatting, zero-warning compilation, and all 22 tests pass on .NET SDK 9.0.316.
- HCS Tier 1 Ubuntu WSL locked restore, formatting, zero-warning cross-build, and all 22 tests pass on .NET SDK 9.0.315.
- Windows `win-x64` self-contained packaging passes without PDBs and without modifying the canonical dependency lock files.
- Packaged-application startup and responsive main-window verification pass on Windows.
- Live Azure tenant authentication and data-plane access are not automated because the repository contains no tenant credential; this is an explicit preview limitation.

## Publishing state

- Pull requests [#2](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/2) and [#3](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/3) are merged.
- Tag `v0.1.0-preview.1` points to commit `0a0ad791808f8e3f09c82375b1f574e40bc1ad6c`.
- Release workflow [29528363343](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/29528363343) passed and published the Windows archive, checksum, SPDX SBOM, and Sigstore bundle.
- Independent SHA-256, ZIP contents, SPDX document, and Cosign identity verification passed.
- Repository writes must use an HCS governance-minted GitHub App installation token.
- GitHub-native attestations remain unavailable while the repository is private under the current organization plan.
