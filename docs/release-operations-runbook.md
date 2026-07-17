# Release Operations and Incident Runbook

**Owner:** Kristopher Turner / Hybrid Solutions Cloud

**Frequency:** Every release, package update, credential rotation, or incident

**Last updated:** 2026-07-16

**Last exercised:** Preview.2 build/install/upgrade/uninstall paths on 2026-07-16; package-service failure handling exercised during Chocolatey HTTP 504 responses

## Purpose

Use this runbook to publish or withdraw Vault Prospector, recover from a failed update, rotate
release credentials, respond to a security incident, and preserve auditable evidence. It applies to
the private source repository, public binary repository, MSI, WinGet, and Chocolatey distribution.

Never replace an artifact under an existing version. A changed binary, manifest, checksum, or
signature always receives a new immutable version.

## Contacts and channels

| Situation | Channel | Owner |
| --- | --- | --- |
| Normal product support or reproducible non-sensitive defect | Private repository issue | Repository maintainers |
| Suspected vulnerability or sensitive incident | <kris@hybridsolutions.cloud> with subject `Vault Prospector security report` | Kristopher Turner |
| WinGet validation/moderation | The package PR in `microsoft/winget-pkgs` | `kristopherjturner` contributor account |
| Chocolatey validation/moderation | Chocolatey package page and publisher dashboard | Vault Prospector Chocolatey publisher account |
| HCS GitHub App or Key Vault failure | HCS platform repository/process | HCS platform owner |

Do not place credentials, tokens, secret values, private keys, sensitive screenshots, or unredacted
diagnostics in an issue or package-review comment.

## Release prerequisites

- [ ] Every required gate in the [release-readiness matrix](product/release-readiness.md) is Passed for the target release stage.
- [ ] `main` is clean, pushed, and protected CI is green for the exact candidate commit.
- [ ] Version, release notes, evidence template, installer metadata, and package manifests agree.
- [ ] The candidate has no unresolved critical/high security defect.
- [ ] A clean supported Windows system is available for independent verification.
- [ ] HCS MCP can mint the Hybrid Solutions Cloud GitHub App token.
- [ ] WinGetCreate is authenticated as `kristopherjturner` for WinGet submission.
- [ ] `CHOCOLATEY_API_KEY` is available from `kv-hcs-vault-01` for Chocolatey submission.
- [ ] Rollback triggers, last verified version, approver, and support owner are recorded before tagging.

## Publish a release

### 1. Verify source

```powershell
$env:Path = 'C:\Program Files\dotnet;' + $env:Path
pwsh ./scripts/Build.ps1 -Configuration Release
dotnet list VaultProspector.sln package --vulnerable --include-transitive
```

Expected result: locked restore and formatting pass, build has zero warnings/errors, all tests pass,
and no vulnerable packages are reported.

If it fails: stop. Do not tag or publish. Fix the failure on `main` and rerun the complete gate.

### 2. Build and validate packages

```powershell
$version = '<version>'
pwsh ./scripts/PackageInstaller.ps1 -Version $version
pwsh ./scripts/PackageDistribution.ps1 -Version $version
winget validate --manifest "./artifacts/distribution/winget/HybridSolutionsCloud.VaultProspector/$version"
```

Expected result: MSI, portable ZIP, WinGet manifests, Chocolatey package, and checksums are created;
all WinGet YAML files use CRLF consistently; validation succeeds without warnings.

If it fails: stop. Never manually edit only the generated artifact; correct the generator and test
the same version in a non-published build.

### 3. Tag and run protected release automation

Create an annotated immutable tag only after the candidate commit and checklist are approved:

```powershell
git tag -a "v$version" -m "Vault Prospector $version"
git push origin "v$version"
```

Expected result: the protected GitHub release workflow builds/tests again and publishes source-repo
artifacts, hashes, SPDX SBOM, and Sigstore bundles.

If it fails: do not reuse the tag after any artifact was published. Diagnose the workflow, increment
the version, and create a new candidate.

### 4. Verify and mirror public artifacts

Obtain a fresh HCS GitHub App installation token through the HCS MCP and expose it only as
`GH_TOKEN` for the current process. Then run:

```powershell
pwsh ./scripts/PublishDistribution.ps1 -Version $version
```

Download the public assets anonymously and verify checksums and Sigstore bundles using
[Release and artifact verification](release.md). Record file sizes and hashes in a new
`docs/release-evidence/<version>.md` file.

If the public repository is private, mutable, or requires credentials to download, stop package
submission because WinGet and Chocolatey must reach an immutable public installer URL.

### 5. Submit WinGet

```powershell
wingetcreate token --store
pwsh ./scripts/SubmitPackageManagers.ps1 -Version $version -SkipChocolatey
```

Expected result: a PR owned by `kristopherjturner` is opened or updated in
`microsoft/winget-pkgs`. Record the PR URL, validation run, labels, review findings, merge time, and
the first successful public `winget search/install/upgrade` result.

If validation fails: compare the submitted bytes with the generated manifests, correct the
generator, update the same contributor branch, and wait for a fresh validation run. Preserve the
actual MSI ARP `DisplayVersion`; do not substitute the marketing version without installer evidence.

### 6. Submit Chocolatey

```powershell
$env:Path = 'C:\ProgramData\chocolatey\bin;' + $env:Path
. D:/git/platform/scripts/Load-HCSEnvironment.ps1
pwsh ./scripts/SubmitPackageManagers.ps1 -Version $version -SkipWinGet
```

Expected result: Chocolatey accepts the immutable `.nupkg` for automated validation/moderation.
Record the response and package URL. Approval is not proven until the exact version appears through
the Community Repository API and installs successfully on a clean system.

If the endpoint times out: query the exact package/version before retrying. If it is absent, retry
once after service recovery. Repeated 5xx responses are an external outage; preserve timestamps and
stop repeated uploads.

### 7. Independent Windows verification

Complete [the release smoke-test checklist](release-checklist.md) on a clean supported Windows
system for direct MSI, WinGet, and Chocolatey. Include install, repair, upgrade from the last
supported version, launch, core workflows, uninstall, and retained-data behavior. Record the tester,
Windows build, commands, results, artifact hashes, and time.

For the direct MSI lifecycle, run `tests/scenario/windows-installer-lifecycle.scenario.ps1` with the
previous and current immutable MSI paths and their independently obtained published SHA-256 hashes.
Archive its structured result and verbose MSI logs with the release evidence. The harness must begin
without an installed copy; it is not authorized to replace or remove a user's existing installation.

## Failed update and rollback

1. Stop the rollout when a rollback trigger in the readiness matrix occurs.
2. Preserve the failed installer, logs, hashes, signatures, package metadata, version, and affected
   Windows build without collecting secret material.
3. Mark the GitHub release as withdrawn and add a prominent notice; do not delete or replace assets
   needed for investigation.
4. Request WinGet/Chocolatey unlisting or moderation action through their supported maintainer
   channels. Publish a fixed version rather than editing an approved immutable installer URL.
5. Direct users to uninstall the affected version. Windows Installer should retain
   `%LOCALAPPDATA%\VaultProspector`.
6. Do not claim that installing an older binary over newer local state is supported. If a schema or
   key change prevents safe downgrade, instruct users to close the app, preserve evidence if needed,
   remove `%LOCALAPPDATA%\VaultProspector`, install the last verified version, reconnect identities,
   and resynchronize metadata from Azure.
7. Rotate any affected credential and publish containment/recovery guidance.
8. Build, sign, verify, and publish a new immutable version through the complete release procedure.

## Security incident procedure

| Severity | Example | Immediate action |
| --- | --- | --- |
| Critical | Published artifact compromise, plaintext protected data, verification bypass with broad exposure | Stop distribution immediately, preserve evidence, rotate affected credentials, withdraw packages, notify users |
| High | Reachable secret/token disclosure or authorization-boundary bypass | Stop affected path, assess exposure, rotate/contain, prepare fixed release before disclosure |
| Medium | Security control weakness requiring user interaction or constrained preconditions | Track privately, define mitigation/owner/date, fix before GA unless formally accepted |
| Low | Hardening or defense-in-depth issue without a demonstrated sensitive impact | Track and schedule; do not misclassify higher-impact chains |

For every incident:

1. Record detection time, reporter, affected versions, artifact hashes, scope, and safe reproduction.
2. Do not copy live secret material into the incident record.
3. Determine whether release, GitHub, Chocolatey, WinGet, Azure, signing, or user credentials require
   rotation or revocation.
4. Apply the rollback procedure and the disclosure process in [SECURITY.md](../SECURITY.md).
5. Add regression tests and update the threat model, runbook, readiness matrix, and release evidence.
6. Close only after containment, remediation, independent verification, and required notification.

## Credential rotation

### Chocolatey publisher API key

1. Generate/rotate the API key in the Chocolatey publisher account.
2. Update the repository Actions secret `CHOCOLATEY_API_KEY`.
3. Update `keyvault://kv-hcs-vault-01/hcs-vault-prospector-chocolatey-publisher-api-key` with:

   ```powershell
   pwsh ./scripts/Set-ChocolateyApiKeyInKeyVault.ps1
   ```

4. Verify only Key Vault metadata/tags and a non-production authenticated package operation; never
   display the value. The standard rotation interval is 180 days or immediately on suspected compromise.

### WinGet contributor credential

```powershell
wingetcreate token --clear
wingetcreate token --store
```

Authenticate as `kristopherjturner`. The local OAuth credential is not a Vault Prospector runtime
secret and is not stored in HCS Key Vault for the manual submission path.

### HCS GitHub App

Vault Prospector never uses a personal PAT to push or publish into the Hybrid Solutions Cloud
organization. Rotate the HCS App private key through the central platform process, update
`hcs-platform-github-app-private-key` in `kv-hcs-vault-01`, invalidate the retired key, and verify a
fresh installation token without recording it.

### Sigstore and future Authenticode identity

Sigstore signing is keyless through GitHub Actions OIDC and has no repository signing secret to
rotate. An Authenticode certificate is not yet configured; its protected storage, access, rotation,
timestamping, revocation, and compromise procedure must be approved before P-13 passes.

## Verification and history

- [ ] Public assets match recorded hashes and signatures.
- [ ] Direct MSI, WinGet, and Chocolatey resolve the intended immutable version.
- [ ] Clean install, repair, upgrade, launch, and uninstall pass.
- [ ] Core identity/search/reveal/copy/cache/purge paths pass without sensitive diagnostics.
- [ ] Readiness matrix and Preview/GA issues reflect the evidence.
- [ ] Incident, rollback, or rotation actions are added to the relevant release-evidence record.

| Date | Operator | Result |
| --- | --- | --- |
| 2026-07-16 | Codex under HCS governance | Initial runbook created from Preview.2 evidence; Chocolatey 504 retry handling exercised; full end-to-end runbook exercise remains a Preview gate |
