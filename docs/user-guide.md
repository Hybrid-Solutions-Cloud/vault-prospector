# User Guide

## Install on Windows

Download the Windows x64 MSI for the [current Preview](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.3.0-preview.5), verify its published SHA-256 checksum, and run it. The installer requires administrator approval, installs to `C:\Program Files\Vault Prospector`, and adds **Vault Prospector** to the Start menu. This Preview is intentionally unsigned, so Windows displays **Unknown Publisher**; confirm that the downloaded filename and checksum match the release before approving installation. Trusted Windows signing remains required for GA.

After installation, **Settings > Product updates** can check the authenticated public binary-release
repository. Checking, downloading and verifying, and launching Windows Installer are separate
actions; Vault Prospector never updates silently. The application rechecks the exact MSI immediately
before launch, then locks and exits only after Windows Installer starts.

An upgrade or reinstall under the same Windows account retains encrypted settings and discovered
metadata in `%LOCALAPPDATA%\VaultProspector`. Moving that data to another account or device is not
supported because its encryption is bound to the original Windows account. See
[In-app update threat model](security/in-app-update-threat-model.md) for the complete trust and
failure behavior.

After this exact Preview is approved by the community repositories, Windows users can also install with:

```powershell
winget install --id HybridSolutionsCloud.VaultProspector --exact
choco install vault-prospector
```

Preview Chocolatey packages require `--pre`. The portable ZIP remains available for environments where an MSI cannot be used.

## First launch and local unlock

Vault Prospector first asks Windows to verify you before opening its encrypted local data. This
local unlock is separate from Microsoft Entra authentication: it does not sign you in to Azure,
and completing Microsoft sign-in later does not replace the local protection boundary.

After a successful local unlock, a new profile opens directly on **Identities**:

1. **Local unlock** is already complete for this foreground session.
2. **Connect Microsoft Entra** opens Microsoft-controlled authentication. Microsoft handles
   passwords, passkeys/FIDO, MFA, and Conditional Access; Vault Prospector does not receive them.
3. Review the discovered scope and choose **Sync**. Synchronization indexes metadata only. Values
   remain in Key Vault until you explicitly request one and complete verification again.

If Windows verification is canceled, unavailable, not configured, disabled by policy, or fails,
Vault Prospector remains locked and does not initialize the metadata repository.

Windows can report the Windows Hello verification device as unavailable inside AVD or Remote
Desktop even when it is configured on the computer. In that case Vault Prospector uses the
Windows Security credential dialog for the current Windows account, if enterprise policy permits
it. Enter the credential into the Windows-controlled dialog; Vault Prospector does not receive or
store the password. Cancellation, failure, another account, or a policy denial leaves the
application locked with an actionable status. A local console continues to use Windows Hello.

## Connect an identity

Open **Identities**, optionally enter a friendly label, keep the recommended Vault Prospector product registration, and choose **Continue to Microsoft sign-in**. Complete the browser-based Microsoft sign-in, including any MFA, passwordless, FIDO, or Conditional Access prompts required by the tenant. Repeat for employer, customer, personal, or lab identities.

If the tenant blocks the product registration or requires an administrator-controlled application, enable **Use my organization's own public-client registration** and enter its Application (client) ID. See [Authentication setup](authentication.md) for consent and registration requirements. Vault Prospector never asks for an Entra password or client secret.

The current Preview also exposes advanced workload profiles:

- **Managed identity** is always listed so its host support status is clear. It can be connected
  only when Vault Prospector detects an Azure host managed-identity endpoint or Azure Instance
  Metadata Service. A system-assigned identity needs no client ID; a user-assigned identity uses
  its client ID. Creating the profile verifies that Azure can issue an ARM token before saving it.
- **Service principal** requires tenant and client GUIDs plus the thumbprint of a currently valid
  certificate with an accessible private key in the Windows Personal certificate store. Vault
  Prospector validates Azure token acquisition before saving the profile. Client secrets are not
  accepted or stored.
- **Federated service principal** requires tenant and client GUIDs plus an absolute path to a
  readable issuer-projected OIDC token file. The encrypted profile stores only the path. Token
  content remains in the issuer-managed file.

Workload profiles use separate Azure credential objects and never inherit Azure CLI, Azure
PowerShell, IDE, terminal, or human MSAL token-cache context. These paths are included for
non-production Preview evaluation and still require the documented live Azure validation before
production support.

For a selected certificate or federated service principal, enter a replacement credential under
**Rotate workload credential** and choose **Validate and rotate**. Azure token validation must
succeed before the encrypted profile changes. **Revoke local access** disables the selected
profile, removes the app-owned human token or stored workload credential reference, and purges
offline copies for its discovered vaults. Also revoke compromised certificates, federation trust,
or managed-identity assignments at their external issuer.

## Discover and preview workload identities

In Vault Prospector, a **workload identity** is a non-human Azure identity used by an application,
automation job, or Azure resource. It is different from the interactive Microsoft Entra user who
operates the desktop application. Administration lists only eligible customer-managed candidates
and remains read-only; it does not create an identity or grant Azure access.

`0.2.0-preview.1` includes a read-only **Administration** tab:

1. Select an enabled, ready interactive identity on **Identities**.
2. Enter an exact subscription GUID and choose **List managed identities** to list user-assigned
   managed identities visible through that account.
3. Choose **Authorize Microsoft Graph directory read** only when service-principal discovery is
   needed. Microsoft Entra requests delegated `Application.Read.All` and may require administrator
   consent. Then choose **List service principals**. The default candidate list contains only
   enabled application service principals whose application-registration owner is the selected
   identity's home tenant. Microsoft-owned first-party/infrastructure applications, external
   enterprise applications, disabled principals, and managed-identity service principals are
   excluded. Managed identities are discovered separately through the exact Azure subscription.
   Graph traversal is limited to ten 100-item pages and 1,000 eligible candidates. Use the local
   accessible filter to narrow by name, type, client ID, or principal ID.
4. Enter one exact Key Vault resource ID, select a discovered candidate, and choose **Assess
   selected identity permissions**. The app reads the administrator's effective permissions at the
   exact managed-identity and vault resources, then inspects applicable candidate role
   assignments, role definitions, deny assignments, and conditions.
5. Review each evidence row's subject, exact scope, basis, state, and UTC observation time.
   **Conditional**, **Incomplete**, and **Not granted** never mean allowed. Access-policy vaults,
   unreadable deny assignments, potentially applicable group denies, and conditional expressions
   remain unproven. Even **Confirmed** role evidence is not a runtime test as the workload
   identity.
6. Enter a proposed identity name and, for managed identity, resource group. Optionally enter a
   matching role-definition resource ID with the exact Key Vault resource ID. Generate a preview.

The plan is deterministic and non-mutating. It names all intended scopes and effects, but this
build has no execution command and requests no identity or role-assignment write permission.
Directory visibility and customer ownership do not prove that the current operator owns a
credential, can attach the identity, or has Key Vault access; those states remain explicitly
unproven until their separate read-only assessment succeeds.
Provisioning remains gated on independent security review, fresh authorization design, confirmation,
encrypted audit, rollback, and live Azure tests.

## Synchronize metadata

Select an identity and choose **Sync selected**. Vault Prospector enumerates subscriptions, discovers Azure Key Vault resources, and indexes secret, key, and certificate versions. It does not retrieve secret values during synchronization. Choose **Cancel** to stop the current run; starting sync again safely upserts the discovered metadata.

After a completed synchronization, choose **Continue to Find secrets**. This remains available
when the run completed with isolated errors because successful metadata is preserved; review and
retry only the affected scopes from **Identities**.

One inaccessible subscription, vault, or object category does not stop unrelated work. The status
bar reports successful counts and isolated error counts without exposing resource names in logs.
Select an isolated error to see its safe timestamp and correlation ID, then choose **Retry selected
scope**. The retry targets only that exact subscription or vault and upserts its results without
marking unrelated metadata as removed.

After the first synchronization, the **Identities** tab shows the subscriptions and vault access
paths discovered for the selected identity. Select a subscription or vault and choose **Include**
or **Exclude** to control later synchronization. These choices are per identity and are applied
before Azure metadata enumeration. Exclusion keeps the scope record so it can be included again;
a successful synchronization tombstones excluded indexed objects without deleting history.

Each vault access path shows the responsible identity, tenant, and subscription. Its permission
summary separates management-plane visibility from the observed ability to list secret, key, and
certificate metadata. Synchronization never reads a secret to test value-read permission, so that
state is explicitly shown as not tested. Data-plane writes remain disabled by application policy,
even when the Azure identity has broader permissions.

## Search offline

The **Search** tab queries the encrypted local index and works without Azure connectivity. Search
by object name or tags. Tenant, subscription, and vault filters are populated selectors based on
the synchronized inventory, so an exact available value can be chosen without copying an ID.
Filters also cover object type, enabled/expired state, favorite status, and staleness. Select an
identity or workspace on its tab and enable the corresponding search checkbox to scope results.
Enable **Recent first** to prioritize objects opened previously. Every result shows its vault and
identity context so the access path is explicit.

Stale means the item has not been refreshed within the application's current staleness window. Azure remains authoritative.

## Reveal or copy a secret

Select a secret result, then choose:

- **Reveal** to show the value for ten seconds;
- **Copy securely** to place it on the clipboard for the configured interval.

Both actions require Windows verification. Settings can optionally reuse one successful
verification for consecutive explicit **Reveal** actions for Off, 30, 60, or 120 seconds.
Enterprise policy may shorten that period or force Off. Each value is still retrieved only after
selection, remains visible for no more than ten seconds, and is not prefetched or persisted by
Reveal. The grace period ends immediately on lock, minimize/background transition, Windows
session change, suspend/resume, identity or workspace change, policy change, timeout, or
verification failure. It never applies to Copy, offline access, recovery, administrative actions,
or browser fill.

Keys and certificate private keys are never exported. Clipboard clearing cannot revoke content
already captured by clipboard history, remote clipboard synchronization, or another process.

## Favorites and workspaces

Choose **Favorite** on a result to include it in the Favorites filter. Create workspaces to
represent customers, projects, or environments without duplicating indexed data. A selected
workspace can receive the selected identity, tenant, subscription, or vault from the corresponding
Identity/Search selection. A resource may belong to multiple workspaces. Enable **Selected
workspace** in Search to apply that scope.

The Workspaces tab also edits the selected workspace's offline-cache enablement, maximum lifetime,
and clipboard permission. Save the workspace policy before using it. Windows verification cannot
be disabled: it remains mandatory for reveal, copy, caching, and reopening an offline value.

## Diagnostics and support bundles

Open **Activity & support** and choose **Refresh diagnostics** to display the newest privacy-safe
events. Each row provides a timestamp, fixed category, pseudonymous scope when available, safe
status summary, and a recovery action. The viewer and external JSON-lines log exclude secret
values, tokens, credentials, usernames, vault names, and object names.

If the application cannot open or unlock, collect the external log from the exact path displayed
on that page—normally
`%LOCALAPPDATA%\VaultProspector\logs\vault-prospector.log`. Do not attach the encrypted database,
settings, token cache, offline-value files, crash dumps, or screenshots containing customer
information.

Choose **Create support bundle** to produce a local ZIP containing only its manifest and at most
the latest 4 MiB of diagnostic events. Export parses and re-sanitizes every event through the fixed
diagnostic allowlist instead of copying the source log blindly; malformed records and unknown
fields are omitted. Nothing is uploaded automatically. Open and inspect the ZIP before sending it
through an approved support channel. If export fails, the source log is left unchanged and its
external collection path remains available.

## Machine-managed policy (Preview)

Settings shows a read-only summary when an administrator manages Vault Prospector through HKLM
policy. The policy can restrict Azure tenants, providers, identity types, clipboard use, and
offline-value retention. User and workspace settings cannot weaken it; the most restrictive
applicable setting wins.

When a connected source becomes disallowed, governed search, synchronization, and value operations
stop, but local disable, revoke, purge, and remove actions remain available. Ask an administrator
to review the deployed policy rather than editing the registry as a standard user. See
[Machine-managed enterprise policy](enterprise-policy.md) for the ADMX/ADML deployment guide and
validation limits.

Governed Azure mutation controls are absent in normal Preview builds. Their production code is
default-denied and appears only when an independently accepted build enables its release switch
and an administrator deploys exact operation and exact vault-resource policy. Connecting an
identity, synchronizing, searching, revealing, or installing the app never enables mutation.

## Offline values

Offline values are disabled by default. To evaluate the feature:

1. Open **Settings** and enable the encrypted offline cache globally, or enable it for the selected
   workspace on **Workspaces**.
2. Set a maximum lifetime.
3. Select a secret and choose **Cache offline**.
4. Complete Windows Hello verification.
5. Choose **Open offline** to reopen an unexpired copy without contacting Azure. Windows Hello is required again.

Cached values are encrypted separately with AES-GCM. Their key is protected for the current Windows user with DPAPI. Expiration, source fingerprint, and scope metadata are authenticated with the value, and a metadata fingerprint invalidates the copy after the source version changes. Security upgrades may invalidate an older Preview cache format; cache the value again explicitly if an unexpired copy is no longer available after upgrading. Purge the selected item from Search, the selected identity from Identities, or the selected vault, selected workspace, or entire cache from Settings.

## Notification area and background synchronization

Settings controls what happens when the main window closes:

- **Ask** presents explicit Lock and continue, Exit, and Cancel choices.
- **Exit** clears an unchanged clipboard value owned by Vault Prospector and stops background work.
- **LockToNotificationArea** hides the window/taskbar entry, masks any presented value, cancels the
  foreground operation, and requires Windows verification when the window is restored.

The tray menu reports the current safe state and provides **Show Vault Prospector** and **Exit**.
Windows session changes—including lock, unlock, logon, logoff, console/remote connection changes,
and remote-control changes—immediately lock Vault Prospector, cancel active work, close any
in-app close prompt, and mask a presented value. Suspend and resume produce the same fail-safe
lock. Changing between battery and external power by itself does not lock the app.

Metadata-only background synchronization is separately opt-in. It runs every 15 minutes only while
the window is hidden, Windows reports network availability, and Windows confirms external power.
It never calls secret-value
retrieval, clipboard, or offline-cache operations. Azure interaction-required or network failures
remain visible as status and do not unlock foreground access.

## Browser fill (Preview)

The current Preview includes a Browser tab for exact, one-time fills. A local mapping is
not enough: an administrator must also enable the same HTTPS destination, browser family, and field
purpose in protected machine policy. Each request displays its destination, purpose, secret, vault,
and identity in the desktop app and requires **Verify and fill once** plus fresh Windows
verification.

The Browser setup card can refresh its checklist. It reports whether a supported Chrome, Edge, or
Firefox extension is installed, whether the matching machine native-host registration and
manifest are valid, whether the host executable is beneath the protected install root, and whether
the local broker is ready. Correct each failed check before creating a mapping.

Choose **Set up in Edge**, **Set up in Chrome**, or **Set up in Firefox** to open the browser's
extension-management page and the exact reviewed extension folder included by the MSI. Follow the
instructions shown in the app, then choose **Refresh setup check**.

Vault Prospector never scans saved browser passwords and does not fill in the background. See
[Browser integration](browser-integration.md) for setup, policy format, limitations, and release
status.

## CyberArk Privilege Cloud (future roadmap)

CyberArk Privilege Cloud is not enabled or supported in the current Windows Preview. Its private
source and automated tests remain future-roadmap work until live-tenant validation, independent
security review, and a separate release decision are complete.

See [CyberArk integration](cyberark-integration.md) for the future design, security boundaries, and
remaining release evidence. Do not rely on that document as a supported configuration guide for
the current Windows release.

## Remove local data

- To remove an identity from **Identities**, select it, type `REMOVE`, and choose **Remove local
  connection**. This purges its local token-cache account and mappings; it does not delete the
  Microsoft Entra account.
- Choose **Purge identity offline values** to clear every protected offline value associated with
  that identity without removing it.
- Purge offline values from **Settings**.
- For an MSI or package-manager installation, uninstall from **Settings > Apps > Installed apps**, WinGet, or Chocolatey. For a portable ZIP, delete the extracted application folder.
- To remove all local application state, delete `%LOCALAPPDATA%\VaultProspector` after closing the app.

If startup reports that only the settings file is damaged, close the app and delete `%LOCALAPPDATA%\VaultProspector\settings.json`. This resets non-secret preferences and does not delete the encrypted metadata database, protected offline values, or app-owned token cache.

If startup reports that a protected key is unavailable, do not create a replacement key or delete
individual encrypted files. Vault Prospector leaves the encrypted state unchanged. A recoverable
copy must contain the matching data and `keys` directory from the same Windows account; restore
the matched set together. If no matched recovery copy exists and Azure remains authoritative, use
the in-app recovery panel: type `RESET`, complete fresh Windows verification, and choose
**Verify and archive local data**. Vault Prospector moves the complete local state to a timestamped
directory beneath `%LOCALAPPDATA%\VaultProspector-Recovery`, creates an empty data directory, and
requires restart. After restarting, reconnect identities and synchronize again.

To review retained recovery data, open **Settings > Recovery archive retention**. Vault Prospector
lists each app-generated reset, pre-rotation, or interrupted-rotation archive with its local
creation time and size. Archives are never removed automatically. To permanently remove one,
select it, type `DELETE ARCHIVE` exactly, choose
**Verify and permanently delete selected archive**, and complete fresh Windows verification.
Delete an archive only after deciding that its matched-key recovery data and support evidence are
no longer needed. Vault Prospector refuses unknown paths, reparse points, and deletion while
rotation recovery is pending.

If startup reports that a newer Vault Prospector version is required, install the same or a newer
version than the build that last opened the local data. Do not use an older binary to reset the
database. If startup reports metadata integrity failure, preserve the local directory for support
or restore a matched data-and-key set. The same verified archive-and-restart workflow is available
when starting fresh is deliberate; Vault Prospector will not silently rebuild that database.

For the complete inventory of locally processed data, network activity, retention, and deletion
behavior, see [Privacy and local data handling](privacy.md).

## Backup and device migration

Vault Prospector does not provide cross-device backup/restore. Metadata and offline-value keys are
protected with Windows DPAPI for the current Windows user. Copying `%LOCALAPPDATA%\VaultProspector`
or a recovery archive to another device or user profile is not a supported migration. A recovery
archive keeps a failed same-account state intact for deliberate recovery or support; it is not an
ordinary backup. On a replacement device, install the app, connect identities again, and
resynchronize metadata from Azure. Recreate any explicitly needed offline copies after reviewing
policy; do not synchronize the protected-value or recovery directories through a consumer
cloud-drive folder.
