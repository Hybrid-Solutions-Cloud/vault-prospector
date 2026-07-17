# Onboarding and Product Registration Evidence — 2026-07-16

This record supports Preview gates P-06 and P-14. It proves the implemented configuration and automated coverage; it does not replace the remaining live tenant-policy, accessibility, or usability tests.

## Microsoft Entra configuration

Azure CLI inspection in the HCS tenant confirmed:

| Property | Verified value |
| --- | --- |
| Display name | Vault Prospector |
| Application (client) ID | `221af888-1c16-4637-9d45-b6dd2e1e7634` |
| Sign-in audience | `AzureADMultipleOrgs` |
| Public-client flow | Enabled |
| Redirect URI | `http://localhost` |
| Delegated APIs | Azure Resource Manager `user_impersonation`; Azure Key Vault `user_impersonation` |
| Application passwords/client secrets | 0 |
| Application certificates | 0 |
| Home service principal | Present, enabled, type `Application` |
| Publisher verification | Not configured |

The product registration is therefore suitable for an installed public client and holds no reusable application credential. It does not grant Azure RBAC or Key Vault data-plane access. Because publisher verification is not configured, tenant consent policy may require administrator approval; the UI retains an organization-controlled registration option.

## Implemented behavior

- A missing or legacy-empty settings file selects the product registration.
- A previously saved custom client ID is preserved and migrated to explicit custom mode.
- Interactive sign-in obtains an ARM token while requesting Key Vault delegated consent; tokens remain audience-specific.
- First run explains the account connection sequence and delegates passwords, MFA, Conditional Access, and FIDO prompts to Microsoft Entra.
- Authentication, authorization, Windows verification, protected-data, policy, and corrupted-settings failures map to redacted recovery guidance without echoing exception text.
- The app does not accept or store a client secret.

## Automated evidence

`VaultProspector.App.Tests` covers the product-registration default, legacy settings migration, custom-registration preservation, malformed custom settings, redacted error handling, Windows Hello recovery wording, and narrow corrupted-settings recovery. `VaultProspector.Providers.Azure.Tests` asserts separate ARM token acquisition and Key Vault extra consent.

The full local Release gate passed on 2026-07-16: locked restore, structured direct/transitive vulnerability inspection, formatting verification, a build with zero warnings and zero errors, and 41 tests. The final CI run must be linked after this evidence is committed.

## Desktop smoke evidence

The Release executable launched on Windows and rendered the first-identity guide, recommended product registration, custom-registration option, friendly label, and Microsoft sign-in action. UI Automation exposed the onboarding text and meaningful names for the custom client-ID control and sign-in button. Toggling custom mode exposed its requirements and client-ID field.

The initial window took longer than the ten-second automation polling window to become targetable. A second keyboard-only run confirmed that Tab reaches the custom-registration checkbox, Space exposes the custom client-ID field, and the next Tab focuses that named field. This is still partial smoke evidence: startup performance, complete task navigation, and real screen-reader behavior remain open under P-14 and P-15.

## Evidence still required

- Product-registration consent in the home tenant and at least one external/guest tenant.
- Tenant policies that require administrator consent.
- MFA, passwordless/FIDO, Conditional Access, cancellation, token expiry, reauthentication, and identity removal.
- Keyboard-only and screen-reader first-run completion.
- Windows Hello unavailable, cancelled, failed, and successful recovery on a supported Windows clean machine.
