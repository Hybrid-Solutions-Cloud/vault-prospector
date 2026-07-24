# CyberArk Privilege Cloud integration

## Supported scope

Vault Prospector's initial CyberArk provider targets Privilege Cloud Shared Services on the
production `*.id.cyberark.cloud` and `*.privilegecloud.cyberark.cloud` domains. It uses a dedicated
CyberArk Identity service user and authorization application. Conjur, on-premises PVWA, custom
domains, interactive/MFA users, and certificate authentication are not supported by this provider.

The implementation is internally complete but not released as supported integration. A governed
non-production tenant, independent security review, and exact signed-artifact validation are still
required.

## Configure a profile

1. Have the CyberArk tenant administrator create a least-privilege Identity service user and
   authorization application for Privilege Cloud REST access.
2. Grant only the required visible safes and account retrieval permissions. Apply confirmation,
   ticketing, and audit policy in CyberArk.
3. Open **CyberArk** in Vault Prospector.
4. Enter a local profile label, the CyberArk Identity root URL, the Privilege Cloud root URL, the
   service-user name, application name, and client credential.
5. Select **Validate and protect credential**.

Vault Prospector validates the credential first, clears the form, and stores the credential in a
profile-specific Windows DPAPI file. The credential is not stored in SQLCipher metadata. Repeating
the action on an existing profile validates the replacement before rotating it.

## Synchronize and search

Select a profile and choose **Sync metadata**. Vault Prospector lists visible safes, direct
service-user safe-member evidence, accounts, and secret versions. It stores that metadata in the
encrypted local database. Sync never retrieves an account value.

Use **Search local CyberArk metadata** to search account name, safe, username, or address. Search
works against encrypted local metadata and remains separate from Azure results.

Direct member evidence does not prove complete effective access. CyberArk group/role membership,
dual control, confirmation, ticketing, platform policy, and current server authorization remain
authoritative.

## Retrieve a value

1. Select the exact CyberArk profile and account.
2. Confirm the safe, username/address, secret type, permission evidence, and optional version.
3. Enter a non-sensitive business reason. CyberArk receives this reason; the local audit does not
   store it.
4. Choose **Verify and reveal** or **Verify and copy**.
5. Complete fresh Windows verification.

Reveal lasts ten seconds. Copy follows the configured owner-aware clipboard clear interval.
Backgrounding, locking, Windows session/power boundaries, or cancellation hides the presentation.
CyberArk values are not available to the initial offline-value cache.

## Disable, rotate, revoke, and remove

- **Enable or disable** blocks retrieval locally without deleting encrypted metadata. A re-enabled
  profile remains unvalidated until credential validation or a successful metadata sync completes.
- To rotate, select the profile, enter the complete configuration and replacement credential, and
  validate again.
- To revoke local access, type `REVOKE CYBERARK` and select **Revoke local CyberArk access**. The
  profile is marked revoked and disabled before the protected credential is deleted, so a deletion
  failure remains fail-closed. Then revoke the service user or credential in CyberArk Identity;
  local revocation cannot invalidate an externally issued credential.
- To remove, type `REMOVE CYBERARK` and select the removal action. The protected credential and
  synchronized metadata are deleted. Value-free local audit is retained.

## Safe diagnostics

Do not attach real credentials, tokens, retrieval reasons, tenant identifiers, safe/account names,
values, or unreviewed screenshots to issues. Vault Prospector maps server failures to status and
category only; it does not include CyberArk response bodies in user errors or logs.
