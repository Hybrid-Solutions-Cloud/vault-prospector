# User Guide

## Install on Windows

Download the Windows x64 MSI from the [public distribution releases](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases), verify its checksum, and run it. The installer requires administrator approval, installs to `C:\Program Files\Vault Prospector`, and adds **Vault Prospector** to the Start menu.

After the packages are approved by their community repositories, Windows users can also install with:

```powershell
winget install --id HybridSolutionsCloud.VaultProspector --exact
choco install vault-prospector
```

Preview Chocolatey packages require `--pre`. The portable ZIP remains available for environments where an MSI cannot be used.

## Connect an identity

Open **Identities**, optionally enter a friendly label, keep the recommended Vault Prospector product registration, and choose **Continue to Microsoft sign-in**. Complete the browser-based Microsoft sign-in, including any MFA, passwordless, FIDO, or Conditional Access prompts required by the tenant. Repeat for employer, customer, personal, or lab identities.

If the tenant blocks the product registration or requires an administrator-controlled application, enable **Use my organization's own public-client registration** and enter its Application (client) ID. See [Authentication setup](authentication.md) for consent and registration requirements. Vault Prospector never asks for an Entra password or client secret.

## Synchronize metadata

Select an identity and choose **Sync selected**. Vault Prospector enumerates subscriptions, discovers Azure Key Vault resources, and indexes secret, key, and certificate versions. It does not retrieve secret values during synchronization. Choose **Cancel** to stop the current run; starting sync again safely upserts the discovered metadata.

One inaccessible subscription, vault, or object category does not stop unrelated work. The status bar reports successful counts and isolated error counts without exposing resource names in logs.

## Search offline

The **Search** tab queries the encrypted local index and works without Azure connectivity. Search by object name or tags. Filters cover object type, enabled/expired state, favorite status, staleness, tenant ID, subscription ID, and vault name. Select an identity or workspace on its tab and enable the corresponding search checkbox to scope results. Enable **Recent first** to prioritize objects opened previously. Every result shows its vault and identity context so the access path is explicit.

Stale means the item has not been refreshed within the application's current staleness window. Azure remains authoritative.

## Reveal or copy a secret

Select a secret result, then choose:

- **Reveal** to show the value for ten seconds;
- **Copy securely** to place it on the clipboard for the configured interval.

Both actions require Windows Hello. Keys and certificate private keys are never exported. Clipboard clearing cannot revoke content already captured by clipboard history, remote clipboard synchronization, or another process.

## Favorites and workspaces

Choose **Favorite** on a result to include it in the Favorites filter. Create workspaces to represent customers, projects, or environments without duplicating indexed data. Select a search result and a workspace, then use **Add selected vault**; or select an identity and use **Add selected identity**. A resource may belong to multiple workspaces. Enable **Selected workspace** in Search to apply that scope.

## Offline values

Offline values are disabled by default. To evaluate the feature:

1. Open **Settings** and enable the encrypted offline cache.
2. Set a maximum lifetime.
3. Select a secret and choose **Cache offline**.
4. Complete Windows Hello verification.
5. Choose **Open offline** to reopen an unexpired copy without contacting Azure. Windows Hello is required again.

Cached values are encrypted separately with AES-GCM. Their key is protected for the current Windows user with DPAPI. Expiration, source fingerprint, and scope metadata are authenticated with the value, and a metadata fingerprint invalidates the copy after the source version changes. Security upgrades may invalidate an older Preview cache format; cache the value again explicitly if an unexpired copy is no longer available after upgrading. Purge the selected item from Search, or purge the selected vault, selected workspace, or entire cache from Settings.

## Remove local data

- Remove an identity from **Identities** to purge its MSAL token-cache account and local access mapping.
- Purge offline values from **Settings**.
- For an MSI or package-manager installation, uninstall from **Settings > Apps > Installed apps**, WinGet, or Chocolatey. For a portable ZIP, delete the extracted application folder.
- To remove all local application state, delete `%LOCALAPPDATA%\VaultProspector` after closing the app.

If startup reports that only the settings file is damaged, close the app and delete `%LOCALAPPDATA%\VaultProspector\settings.json`. This resets non-secret preferences and does not delete the encrypted metadata database, protected offline values, or app-owned token cache.

If startup reports that a protected key is unavailable, do not create a replacement key or delete
individual encrypted files. Vault Prospector leaves the encrypted state unchanged. A recoverable
copy must contain the matching data and `keys` directory from the same Windows account; restore
the matched set together. If no matched recovery copy exists and Azure remains authoritative,
close the app, preserve a copy for support if needed, delete the entire
`%LOCALAPPDATA%\VaultProspector` directory, reconnect identities, and synchronize again.

If startup reports that a newer Vault Prospector version is required, install the same or a newer
version than the build that last opened the local data. Do not use an older binary to reset the
database. If startup reports metadata integrity failure, preserve the local directory for support
or restore a matched data-and-key set; Vault Prospector will not silently rebuild that database.

For the complete inventory of locally processed data, network activity, retention, and deletion
behavior, see [Privacy and local data handling](privacy.md).

## Backup and device migration

Vault Prospector does not provide a backup/restore workflow in the preview. Metadata and offline-value keys are protected with Windows DPAPI for the current Windows user. Copying `%LOCALAPPDATA%\VaultProspector` to another device or user profile is not a supported migration and should not be treated as a recoverable backup. On a replacement device, install the app, connect identities again, and resynchronize metadata from Azure. Recreate any explicitly needed offline copies after reviewing policy; do not synchronize the protected-value directory through a consumer cloud-drive folder.
