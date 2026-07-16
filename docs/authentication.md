# Authentication Setup

Vault Prospector uses Microsoft Authentication Library (MSAL) interactive browser authentication. It supports MFA, Conditional Access, guest accounts, and multi-tenant authorization without collecting a password or storing a client secret.

## Recommended product registration

First run uses the Vault Prospector multi-tenant public-client registration automatically. Its Application (client) ID is `221af888-1c16-4637-9d45-b6dd2e1e7634`. The HCS tenant registration has:

- accounts in any Microsoft Entra organization (`AzureADMultipleOrgs`);
- public-client flow enabled with the `http://localhost` loopback redirect;
- delegated `user_impersonation` permissions for Azure Resource Manager and Azure Key Vault;
- no password/client-secret credentials and no certificate credentials.

The registration authenticates the desktop client; it does not grant the application or user an Azure role. Microsoft Entra still applies the resource tenant's consent, MFA, Conditional Access, passwordless, FIDO, guest-access, and risk policies. The product registration is not yet publisher-verified, so a tenant that restricts user consent may require an administrator to approve it before sign-in succeeds.

## Use an organization-controlled registration

An administrator can require a tenant-owned public-client registration. In **Identities**, enable **Use my organization's own public-client registration** and enter its Application (client) ID.

To create one:

1. In the Microsoft Entra admin center, open **App registrations** and create a registration.
2. Select **Accounts in any organizational directory** if the application must reach customer or guest tenants. Use a single-tenant registration when cross-tenant use is prohibited by policy.
3. Add a **Mobile and desktop applications** platform with the `http://localhost` redirect URI.
4. Enable public-client flows.
5. Add these delegated permissions:
   - Azure Service Management: `user_impersonation`
   - Azure Key Vault: `user_impersonation`
6. Grant consent according to the organization's normal approval process.
7. Copy the **Application (client) ID** into Vault Prospector. Do not create or enter a client secret; a distributed public desktop client cannot safely keep one.

The interactive sign-in requests an Azure Resource Manager token and asks for Key Vault delegated consent in the same Microsoft-controlled flow. Azure Resource Manager and Azure Key Vault remain separate token audiences; the Azure SDK later requests the Key Vault token through the same app-owned MSAL account cache. Vault Prospector never combines audiences into one access token.

## Azure authorization

The app registration enables authentication; it does not grant access to Azure resources. Every connected user continues to have only their existing Azure permissions.

Typical read-only access requires:

- subscription/resource visibility sufficient to enumerate Key Vault resources;
- Key Vault data-plane metadata permissions for secrets, keys, and certificates;
- secret `get` permission only when the user explicitly retrieves a secret value.

Vault Prospector never creates role assignments. Administrators should grant the narrowest built-in or custom roles that support the intended workflow.

## Token storage and removal

MSAL stores tokens in its platform-protected user cache. Vault Prospector stores the non-secret public-client application ID with the account identifier, display label, username hint, and tenant relationship in its encrypted metadata database. Each identity therefore continues using the app registration under which it was connected. Removing an identity removes its MSAL account cache entry and local access mappings.

## Troubleshooting

- **Interaction required:** select the identity and sign in again. Conditional Access may require a fresh browser session.
- **Approval required:** ask a tenant administrator to consent to the Vault Prospector product registration, or use an approved organization-controlled registration.
- **No subscriptions:** confirm the user can read subscriptions and that the app registration is allowed in the tenant.
- **Vault metadata returns 403:** the identity can see the resource but lacks the relevant Key Vault data-plane metadata permission.
- **Secret retrieval returns 403:** metadata listing and secret-value retrieval are intentionally separate permissions.
- **Guest tenant fails:** verify that the guest account is active, consent is allowed, and the resource tenant permits the public-client application.

Tokens and Azure identifiers are deliberately excluded from diagnostic logs. Use Azure sign-in logs and the app's status category together when investigating authorization failures.
