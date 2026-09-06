# Repo intent — vault-prospector

**Local-first Windows desktop app for discovering and searching Azure Key Vault metadata.**

## What this repo is

Discovers and searches Azure Key Vault metadata across multiple Microsoft Entra
identities, tenants, and subscriptions. Secret values are retrieved only after an
explicit action and Windows Hello verification.

**Release status:** `0.3.0-preview.19` is the current unsigned Windows Preview for
non-production evaluation — Windows shows "Unknown Publisher"; verify the
published SHA-256 before installing.

## Shape

- `src/` — the C# application (`VaultProspector.sln`)
- `browser-extension/`, `mobile/`, `installer/` — companion surfaces
- `infrastructure/`, `ops/`, `policy/` — deployment and governance
- `.gitleaks.toml` — secret-scanning config, appropriate given what this tool
  handles

## What works today

Interactive Entra sign-in with MFA/Conditional Access, multiple connected
identities with isolated token caches, subscription and Key Vault discovery,
secret/key/certificate metadata indexing without retrieving values, SQLCipher-
encrypted local metadata storage.

## How it relates to other repos

- **`vault-prospector-releases`** — the public repo hosting this app's signed
  installer artifacts; this repo is source, that one is distribution

## Status

Active, preview release (`0.3.0-preview.19`), pre-1.0.
