# Privacy and Local Data Handling

**Effective date:** 2026-07-24

Vault Prospector is a local-first Windows application. The Preview does not operate a Vault
Prospector cloud service and does not send project-controlled analytics or telemetry.

This document describes technical data handling for the current Windows Preview. It is not a
promise that Windows, Microsoft Entra, Azure, GitHub, Chocolatey, or an organization's network and
endpoint-security products collect no data; those services operate under their own policies.

## Data processed

Vault Prospector processes the minimum data needed for the selected user workflow:

| Data | Purpose | Location and protection | Default retention |
| --- | --- | --- | --- |
| Public-client application ID and identity account metadata | Reconnect the selected Microsoft Entra identity and show its access context | SQLCipher-encrypted local metadata database | Until the identity or all local data is removed |
| Microsoft Entra access and refresh tokens | Authenticate to Azure Resource Manager and Azure Key Vault | App-specific MSAL token cache protected by supported Windows mechanisms | Controlled by MSAL, Entra policy, sign-out, and identity removal |
| Tenant, subscription, vault, key, certificate, and secret metadata | Offline discovery and search | SQLCipher-encrypted local metadata database | Until refreshed, removed, or all local data is deleted |
| CyberArk profile, safe, account, version, direct permission, and value-free audit metadata | Operate the separately configured Privilege Cloud source | SQLCipher-encrypted local metadata database | Synchronized metadata is removed with its profile; local audit is retained until all local data is deleted |
| CyberArk Identity service-user client credential | Authenticate an explicitly configured CyberArk profile | Separate per-profile DPAPI `CurrentUser` file with profile-specific entropy | Until rotated, locally revoked, the profile is removed, or all local data is deleted |
| CyberArk Identity and platform tokens | Authenticate one validate, sync, or retrieve operation | Process memory only; not cached by Vault Prospector | Scoped to the operation |
| Secret value selected for reveal or copy | Perform the explicit user action | Process memory; optionally the Windows clipboard | UI reveal is masked after ten seconds; clipboard clearing uses the configured interval if unchanged |
| Optional offline secret value | Allow an explicit offline workflow | Separate AES-GCM envelope with authenticated descriptor metadata and a DPAPI-protected key for the current Windows user | Disabled by default; expires by policy, is invalidated after an incompatible security upgrade, or is purged by the user |
| Allow-listed diagnostic events | Troubleshoot counts, status categories, and exception types | Local newline-delimited JSON log with identifiers pseudonymized | Until the user deletes local data; automatic log retention is not yet implemented |
| Local settings | Remember whether the product or an optional custom client ID is used, clipboard timeout, offline-cache preference, close behavior, and opt-in background metadata synchronization | Local JSON settings file; it contains no client secret, token, or secret value | Until the settings file or all local data is deleted |
| Recovery archives | Preserve a matched local-state set before reset/rotation and retain interrupted state for support | Timestamped directories under `%LOCALAPPDATA%\VaultProspector-Recovery`; protected values and keys retain their existing encryption/DPAPI boundaries | Retained indefinitely by default; one selected app-generated archive can be permanently deleted only after exact typed confirmation and fresh Windows verification |

## Network activity

When the user signs in or synchronizes, Vault Prospector contacts Microsoft identity endpoints,
Azure Resource Manager, and Azure Key Vault using the selected identity. Azure authorization is
never expanded by the application. Metadata synchronization does not retrieve secret values.

A secret value is requested from Azure Key Vault only after the user explicitly chooses a reveal,
copy, or cache operation and completes required local verification.

For an explicitly configured unreleased CyberArk profile, validate, sync, or retrieval contacts the
configured CyberArk Identity and Privilege Cloud production endpoints. CyberArk metadata sync lists
safes, direct member evidence, accounts, and versions but does not retrieve values. A CyberArk
value is requested only after exact account/version selection, a non-sensitive reason, and fresh
Windows verification. The reason is sent to CyberArk for its authoritative audit but is not stored
in the local audit.

If the user explicitly enables notification-area background synchronization, the application may
contact Microsoft identity endpoints, Azure Resource Manager, and Azure Key Vault metadata
endpoints every 15 minutes while the main window is hidden and Windows reports network
availability and external power. This path does not retrieve secret values, copy data, or create
offline cached values.

Installing or updating through GitHub Releases, WinGet, or Chocolatey contacts those distribution
services. Vault Prospector itself does not send those services vault content or application usage
telemetry.

## Clipboard and screen disclosure

Revealed values are visible to the signed-in Windows session. Clipboard values may be retained by
Windows clipboard history, cross-device clipboard, remote-desktop software, endpoint tools, or
another process before Vault Prospector clears them. Disable clipboard history and synchronization
when organizational policy requires stronger isolation.

Vault Prospector cannot protect a deliberately revealed value from malware running as the same
user or from a local administrator.

## Telemetry and diagnostics

Project-controlled telemetry is disabled in the Preview. Diagnostic logs do not intentionally
contain tokens, secret values, usernames, vault names, object names, private keys, certificate
payloads, or decrypted cache content. Do not send diagnostic files publicly without reviewing
them under organizational policy.

If telemetry is proposed later, it requires an updated public schema, explicit release review, and
an updated notice before activation.

## Voluntary Preview feedback

Preview feedback is collected only when an evaluator explicitly submits a public GitHub issue
through the [Preview feedback process](product/preview-feedback.md). Vault Prospector does not
create an issue, upload diagnostics, or associate application activity with a GitHub account.

A submitted issue and its attachments are public and are processed under GitHub's privacy terms.
The feedback notice requires synthetic or non-production data and excludes credentials, tokens,
identity and Azure-resource identifiers, resource/object names, secret values, and unreviewed
diagnostics or screenshots. Choosing to submit after reading that public notice is the evaluator's
explicit publication action. Suspected vulnerabilities use the private channel in
[SECURITY.md](../SECURITY.md), not the feedback forms.

## Removal and device migration

Removing an identity removes its MSAL account entry and local access mapping. Use application
controls to purge item, vault, workspace, or all offline values.

Local revocation first persists a disabled/revoked state and then deletes the DPAPI-protected
credential while retaining encrypted metadata. Removing a CyberArk profile also deletes its
synchronized safe/account/version/permission metadata. Value-free local audit is retained for
investigation. Neither local action revokes the service user in CyberArk Identity; an administrator
must rotate or revoke it there when compromise is suspected.

Uninstall intentionally retains `%LOCALAPPDATA%\VaultProspector` to avoid silently deleting user
state. To remove all Vault Prospector data, close the application, uninstall it, and delete that
directory. DPAPI-protected keys are bound to the Windows user; copying the directory to another
device or profile is not a supported backup or migration.

An existing encrypted database or offline-value envelope is opened only with its existing matched
protected key. If that key is missing, Vault Prospector does not generate a replacement or alter
the encrypted file. A same-account recovery copy is useful only when its data and `keys` directory
are restored as one matched set. The Preview has no supported cross-device key migration or
application-managed backup/restore workflow.

When a protected key or encrypted database fails validation, Vault Prospector preserves the state.
Starting fresh requires the exact typed confirmation `RESET` and fresh Windows verification. The
application then moves the entire local data directory to a timestamped sibling under
`%LOCALAPPDATA%\VaultProspector-Recovery` and requires restart. The archive can contain encrypted
metadata, protected keys, opt-in offline values, app-owned identity caches, settings, and redacted
logs. It remains local and is not uploaded automatically. It is not a supported cross-device
backup. The Settings page lists app-generated archives without opening their protected values.
There is no automatic age or size deletion policy. Permanent deletion requires selecting one
archive, typing `DELETE ARCHIVE` exactly, and completing fresh Windows verification; delete it only
after deciding that recovery and support evidence are no longer needed.

## Browser integration

The unreleased browser integration does not inspect or import saved browser passwords. A toolbar
action sends only bounded page context needed for an exact fill decision: browser family, tab and
frame identifiers, opaque document/gesture/field tokens, canonical top and frame HTTPS origins,
field purpose, request identifier, and time. The extension does not persist a returned value.

Local browser mappings and value-free audit events are stored in the encrypted metadata database.
The audit records include time, result, canonical origins, field purpose, and local identifiers but
not the secret value. Vault Prospector does not send browser activity to project-controlled
telemetry.

## CyberArk integration

The unreleased CyberArk provider is separate from Azure identities, token caches, objects, browser
mappings, and offline-value caching. SQLCipher does not store the client credential, Identity
access token, platform token, retrieval reason, or account value. The protected credential file is
bound to the current Windows account and profile identifier.

Local CyberArk audit records operation, result, profile/account identifier, safe, version, time,
and a fixed safe message. They do not store the credential, tokens, business reason, or account
value. CyberArk remains authoritative for server-side access and audit records.

## Security and privacy contact

Report suspected vulnerabilities privately as described in [SECURITY.md](../SECURITY.md). For a
technical privacy question, contact <kris@hybridsolutions.cloud>. Do not email credentials, tokens,
private keys, or secret values.
