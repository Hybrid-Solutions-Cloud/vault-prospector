# Authentication Setup

Vault Prospector uses Microsoft Authentication Library (MSAL) interactive browser authentication. It supports MFA, Conditional Access, guest accounts, and multi-tenant authorization without collecting a password or storing a client secret.

## Recommended product registration

First run uses the Vault Prospector multi-tenant public-client registration automatically. Its Application (client) ID is `221af888-1c16-4637-9d45-b6dd2e1e7634`. The HCS tenant registration has:

- accounts in any Microsoft Entra organization (`AzureADMultipleOrgs`);
- public-client flow enabled with the `http://localhost` loopback redirect;
- delegated `user_impersonation` permissions for Azure Resource Manager and Azure Key Vault;
- no password/client-secret credentials and no certificate credentials.

The unreleased mobile hosts require the additional public-client callback
`msal221af888-1c16-4637-9d45-b6dd2e1e7634://auth`. The native manifests already use that exact
value, and the production registration contains it as of 2026-07-24. Live system-browser and
account/tenant validation remains open.

The registration authenticates the desktop client; it does not grant the application or user an Azure role. Microsoft Entra still applies the resource tenant's consent, MFA, Conditional Access, passwordless, FIDO, guest-access, and risk policies. The product registration is not yet publisher-verified, so a tenant that restricts user consent may require an administrator to approve it before sign-in succeeds.

## Use an organization-controlled registration

An administrator can require a tenant-owned public-client registration. In **Identities**, enable **Use my organization's own public-client registration** and enter its Application (client) ID.

To create one:

1. In the Microsoft Entra admin center, open **App registrations** and create a registration.
2. Select **Accounts in any organizational directory** if the application must reach customer or guest tenants. Use a single-tenant registration when cross-tenant use is prohibited by policy.
3. Add a **Mobile and desktop applications** platform with the `http://localhost` redirect URI.
   For a mobile build, also register its exact `msal{client-id}://auth` callback and configure the
   corresponding Android package/signature or iOS URL scheme according to Microsoft identity
   platform guidance.
4. Enable public-client flows.
5. Add these delegated permissions:
   - Azure Service Management: `user_impersonation`
   - Azure Key Vault: `user_impersonation`
6. Grant consent according to the organization's normal approval process.
7. Copy the **Application (client) ID** into Vault Prospector. Do not create or enter a client secret; a distributed public desktop client cannot safely keep one.

The interactive sign-in requests an Azure Resource Manager token and asks for Key Vault delegated consent in the same Microsoft-controlled flow. Azure Resource Manager and Azure Key Vault remain separate token audiences; the Azure SDK later requests the Key Vault token through the same app-owned MSAL account cache. Vault Prospector never combines audiences into one access token.

The advanced **Administration** tab does not request Microsoft Graph access during normal sign-in.
Choosing **Authorize Microsoft Graph directory read** separately requests delegated
`Application.Read.All`, the least-privileged permission Microsoft documents for listing service
principals. Microsoft Entra may require administrator consent and an eligible directory role. The
selected app registration must permit that delegated permission. Vault Prospector does not request
a Graph write permission.

## Azure authorization

The app registration enables authentication; it does not grant access to Azure resources. Every connected user continues to have only their existing Azure permissions.

Typical read-only access requires:

- subscription/resource visibility sufficient to enumerate Key Vault resources;
- Key Vault data-plane metadata permissions for secrets, keys, and certificates;
- secret `get` permission only when the user explicitly retrieves a secret value.

Vault Prospector never creates role assignments. Administrators should grant the narrowest built-in or custom roles that support the intended workflow.

The advanced Administration assessment is also read-only. For the selected administrator, Azure's
caller-permissions endpoint evaluates attach/use, managed-identity management, and role-assignment
management at exact resource scopes. For a discovered workload identity and exact RBAC-enabled Key
Vault, Vault Prospector reads applicable role assignments (including assignments returned through
transitive groups), role definitions, deny assignments, and conditions. It separately reports
metadata-list and secret-value actions.

This evidence is not impersonation. Vault Prospector does not acquire a candidate credential,
retrieve a value, or simulate request/resource attributes. A conditional expression, unavailable
deny-assignment read, potentially applicable group deny, access-policy vault, or incomplete Azure
response remains unproven. Authorization can also change after the displayed UTC observation time.

## Advanced workload profiles

`0.2.0-preview.1` provides three isolated workload paths for non-production evaluation:

- **Managed identity** is available only when the running Azure host exposes a managed-identity
  endpoint. A system-assigned identity has no client ID; a user-assigned identity uses its client
  ID. Vault Prospector stores no managed-identity credential.
- **Certificate service principal** requires tenant and client GUIDs and the SHA-1 or SHA-256
  thumbprint of a currently valid certificate with an accessible private key in the Windows
  Personal certificate store. The private key never enters Vault Prospector storage.
- **Federated service principal** requires tenant and client GUIDs and an absolute, readable path to
  the issuer-projected OIDC token file. The encrypted profile stores the path, not token content.
  The issuer owns token-file replacement and federation trust.

Each workload profile proves ARM token acquisition before it is saved. Certificate and federated
profiles can be rotated from **Identities**: Vault Prospector validates the replacement first and
persists it only on success. **Revoke local access** marks the encrypted profile revoked, removes
its credential reference or app-owned human cache entry, and purges offline copies for its
discovered vaults. Revoke a compromised certificate, federated trust, or managed-identity
assignment at Microsoft Entra or the external issuer as well.

**Purge identity offline values** clears protected offline values for every vault associated with
the selected identity, including retained removed-access history, without disabling or removing the
identity.

Client secrets are not accepted. Workload credentials do not read Azure CLI, Azure PowerShell, IDE,
terminal, or human MSAL caches.

## Token storage and removal

MSAL stores tokens in its platform-protected user cache. Vault Prospector stores the non-secret public-client application ID with the account identifier, display label, username hint, and tenant relationship in its encrypted metadata database. Each identity therefore continues using the app registration under which it was connected. Removing an identity removes its MSAL account cache entry and local access mappings.

## Troubleshooting

- **Interaction required:** select the identity and sign in again. Conditional Access may require a fresh browser session.
- **Federated token unavailable:** restore the configured projected token file and its issuer trust,
  then rotate or reauthenticate the profile.
- **Revoked workload profile:** provide and validate a replacement certificate thumbprint or token
  file. Managed-identity access must first be restored on the Azure host.
- **Approval required:** ask a tenant administrator to consent to the Vault Prospector product registration, or use an approved organization-controlled registration.
- **No subscriptions:** confirm the user can read subscriptions and that the app registration is allowed in the tenant.
- **Service-principal listing is forbidden:** ask a Microsoft Entra administrator to approve
  delegated `Application.Read.All` for the selected app registration and confirm the user has an
  eligible directory role. Then use the explicit authorization action again.
- **Authorization assessment is forbidden:** grant the selected administrator the minimum Azure
  resource and role-assignment read permissions at the exact identity and vault scopes.
  `Microsoft.Authorization/denyAssignments/read` is required before the assessment can confirm
  that no visible deny blocks an observed grant.
- **Vault metadata returns 403:** the identity can see the resource but lacks the relevant Key Vault data-plane metadata permission.
- **Secret retrieval returns 403:** metadata listing and secret-value retrieval are intentionally separate permissions.
- **Guest tenant fails:** verify that the guest account is active, consent is allowed, and the resource tenant permits the public-client application.

Tokens and Azure identifiers are deliberately excluded from diagnostic logs. Use Azure sign-in logs and the app's status category together when investigating authorization failures.
