# Session Handoff

## Current state

- Branch: `agent/build-vault-prospector`
- Target release: `v0.1.0-preview.1`
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

- The release branch is ready for governed publication.
- Repository writes must use an HCS governance-minted GitHub App installation token.
- Publish by merging a reviewed pull request into `main`, tagging the merge as `v0.1.0-preview.1`, and verifying the release workflow, assets, checksums, SBOM, Sigstore bundle, and provenance attestation.
