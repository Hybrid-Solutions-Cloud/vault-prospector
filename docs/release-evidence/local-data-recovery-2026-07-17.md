# Encrypted local-data recovery validation — 2026-07-17

## Scope and decision

This internal record advances GA gate G-03 and strengthens Preview gate P-04. It validates the
application's automated fail-closed behavior for encrypted metadata and protected offline-value
state. It does **not** pass G-03: supported key rotation, application-managed backup/restore,
replacement-device migration, final signed-candidate reinstall, every published-schema path, and
independent review remain open.

## Security invariants implemented

- New encrypted state may create a purpose-bound key; existing encrypted state must obtain its
  existing key and may never silently mint a replacement.
- Missing protected keys preserve the SQLCipher database and AES-GCM envelope unchanged so a
  matched key can be restored explicitly.
- A schema newer than the running binary is rejected before configuration or migration changes.
- SQLCipher open failures, wrong keys, corrupt storage, failed `quick_check`, missing required
  tables/columns, and invalid foreign-key relationships stop initialization.
- The version-1-to-2 schema update and validation execute transactionally.
- There is no plaintext database or cache fallback.
- User-facing errors disclose no exception details and distinguish missing-key, newer-version, and
  integrity recovery actions.

## Automated attack and recovery cases

| Case | Proven outcome |
| --- | --- |
| Existing metadata key removed | Initialization throws `ProtectedKeyUnavailableException`; no replacement appears; database SHA-256 is unchanged; restoring the original key reopens the original identity record. |
| Existing offline-cache key removed | Retrieval throws `ProtectedKeyUnavailableException`; no replacement appears; envelope bytes are unchanged; restoring the original key recovers the value. |
| Metadata key replaced with a wrong 256-bit key | Initialization throws `LocalDataIntegrityException`; database SHA-256 is unchanged; restoring the original key permits initialization. |
| Database replaced with corrupt bytes | Initialization throws `LocalDataIntegrityException`; bytes remain unchanged and no plaintext replacement is created. |
| Database marked with schema version 99 | Running schema version 2 throws `IncompatibleLocalDataVersionException`; SHA-256 and version 99 remain unchanged. |
| Required table removed from a version-2 database | Initialization throws `LocalDataIntegrityException`; the table remains absent rather than being silently recreated. |
| Required column removed from a version-2 table | Initialization throws `LocalDataIntegrityException`; the latent damage remains preserved for explicit recovery instead of reaching normal use. |
| Internal version-1 identity schema | Transactional migration adds `client_id`, sets version 2, and subsequent encrypted reads/writes succeed. |
| Missing DPAPI key file through the production provider | `GetExistingKeyAsync` throws without creating the key directory or a key file. |
| Recovery error mapping | Missing-key, newer-schema, and integrity failures produce separate redacted guidance without echoing internal exception messages. |

## Local verification

The following Release-equivalent gate passed from the working tree on 2026-07-17:

- `dotnet restore VaultProspector.sln --locked-mode`
- `dotnet format VaultProspector.sln --verify-no-changes --no-restore`
- `dotnet build VaultProspector.sln --configuration Release --no-restore`
- `dotnet test VaultProspector.sln --configuration Release --no-build`
- `scripts/Test-VulnerablePackages.ps1`
- PowerShell parser validation over `scripts` and `tests`

Result: build completed with **0 warnings and 0 errors**; all seven projects passed **111/111**
tests (Domain 4, Application 18, Platform 10, Security 1, Azure 3, App 46, Infrastructure 29); no
known vulnerable direct or transitive NuGet package was detected; PowerShell parsing passed.
GitHub Actions remains authoritative for workflow YAML and full-history secret scanning.

Commit [`6cec5a4`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/commit/6cec5a46bee6d9711cb90f4590bf99a54a6aac7a)
was pushed directly to the governed default branch with an HCS-minted GitHub App installation
token. Exact-commit CI run
[`29592049330`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/29592049330)
passed both `build-test`—including tests, package construction, installer-sequence validation,
WinGet manifest validation, and dependency inspection—and the full-history `secret-scan` job.

The exact MSI was then published as public test prerelease
[`v0.1.0-ci.68`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.1.0-ci.68)
with its checksum and `ci-candidate.json` provenance. An unauthenticated download of the public MSI
returned 45,727,744 bytes and independently matched SHA-256
`C605DF2135F81EBF5B19B14F846A50A41801C8D945A1CCF419F08831B9A3546E`. This establishes a usable
cross-machine test distribution path without claiming trusted signing or formal Preview approval.

## Residual work before G-03 can pass

1. Define and test key rotation without losing access to data encrypted under prior key versions.
2. Decide whether to ship an application-managed backup/restore mechanism or formally constrain
   recovery to Azure resynchronization; threat-model either decision.
3. Validate uninstall/reinstall and retained-state recovery on a clean supported Windows machine
   using the final signed candidate.
4. Exercise replacement-device and replacement-profile behavior, including explicit unsupported
   paths and safe reset guidance.
5. Run forward-only migration tests for every schema that is ever publicly shipped.
6. Obtain independent security review of implementation, tests, and live Windows recovery paths.
