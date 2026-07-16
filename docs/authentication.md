# Authentication Setup

Vault Prospector uses Microsoft Authentication Library (MSAL) interactive browser authentication. It supports MFA, Conditional Access, guest accounts, and multi-tenant authorization without collecting a password or storing a client secret.

## Create the app registration

1. In the Microsoft Entra admin center, open **App registrations** and create a registration.
2. Select **Accounts in any organizational directory** if the application must reach customer or guest tenants. Use a single-tenant registration only when cross-tenant use is prohibited by policy.
3. Add a **Mobile and desktop applications** platform.
4. Add the default native-client redirect URI shown by the portal. MSAL uses its loopback/default desktop redirect and does not require a web client secret.
5. Enable public-client flows.
6. Add these delegated permissions:
   - Azure Service Management: `user_impersonation`
   - Azure Key Vault: `user_impersonation`
7. Grant consent according to the organization's normal approval process.
8. Copy the **Application (client) ID** into Vault Prospector's **Identities** tab.

Do not create or enter a client secret. Vault Prospector is a public desktop client and cannot safely keep one.

The interactive sign-in requests an Azure Resource Manager token first. Azure Resource Manager and Azure Key Vault are separate token audiences; the Azure SDK later requests the Key Vault audience through the same MSAL account cache. Vault Prospector never combines audiences into one token request.

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
- **No subscriptions:** confirm the user can read subscriptions and that the app registration is allowed in the tenant.
- **Vault metadata returns 403:** the identity can see the resource but lacks the relevant Key Vault data-plane metadata permission.
- **Secret retrieval returns 403:** metadata listing and secret-value retrieval are intentionally separate permissions.
- **Guest tenant fails:** verify that the guest account is active, consent is allowed, and the resource tenant permits the public-client application.

Tokens and Azure identifiers are deliberately excluded from diagnostic logs. Use Azure sign-in logs and the app's status category together when investigating authorization failures.
