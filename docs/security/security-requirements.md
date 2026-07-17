# Security Requirements

## Authentication

- Use Microsoft-supported OAuth and OpenID Connect libraries.
- Do not implement password collection.
- Support Conditional Access and MFA through interactive browser-based authentication.
- Evaluate Windows Web Account Manager for secure single sign-on with accounts known to Windows, Windows Hello, Conditional Access, and FIDO support.
- Treat local application unlock and Azure authorization as separate boundaries; satisfying one must not silently satisfy the other for a high-risk action.
- Delegate MFA and passwordless authentication to Windows and the configured identity provider rather than implementing a project-specific second factor.
- Avoid Resource Owner Password Credential flow.
- Do not assume refresh tokens are always available.
- Surface interaction-required states clearly.
- Remove token-cache entries when identities are removed.
- Keep application token caches isolated from Azure CLI, Azure PowerShell, IDE, and other terminal-session context files.
- Distinguish interactive human identities from service principals and managed identities in configuration, storage, UI, policy, and audit records.
- Offer managed identity only on supported Azure compute that supplies the identity endpoint.
- Prefer certificate or workload identity federation over stored service-principal client secrets.

## Authorization

- Never elevate Azure permissions.
- Do not create role assignments in the initial product.
- Evaluate access per identity, tenant, subscription, vault, object type, and operation.
- Treat metadata enumeration and value retrieval as distinct capabilities.
- Show the selected access path before or during retrieval.
- Default every new connection to read-only behavior even when the selected identity has broader Azure permissions.
- Treat identity creation, role assignment, and Key Vault mutation as separate elevated capabilities requiring explicit scope, confirmation, least privilege, and audit.
- Never enable future write operations merely because the signed-in account already holds a broad role.

## Local encryption

- Use authenticated encryption.
- Generate encryption keys with a cryptographically secure random generator.
- Store master or wrapping keys through platform-backed secure storage.
- Maintain key-version metadata for rotation.
- Never silently fall back to plaintext.
- Keep metadata encryption mandatory with no user-facing disable switch.
- Make offline value retention optional, not its encryption: every retained value must be encrypted whenever the feature is enabled.
- Fail closed when protected storage is unavailable.
- Document backup and device-migration behavior.

## Secret handling

- Do not retrieve values during metadata indexing.
- Minimize decrypted lifetime.
- Avoid immutable strings for long-lived sensitive buffers where practical.
- Mask values by default.
- Clear UI and application references promptly.
- Do not include values in exception messages.
- Require explicit consent before offline caching.

## Offline cache

- Disabled by default.
- Configurable globally, by workspace, and by item.
- Requires local unlock.
- Supports absolute expiration.
- Supports purge at item, vault, workspace, and global scopes.
- Marks stale and unvalidated content.
- Invalidates entries after source-version changes.
- Must be testable independently from the metadata index.

## Background operation

- Default close behavior must be explicit and configurable; users must be able to fully exit.
- Enter a locked state when the main window closes or the configured idle timeout expires.
- Never reveal, copy, refresh, or newly cache a secret value from an unattended background process.
- Limit optional unattended synchronization to metadata and stop safely when MFA, Conditional Access, or other interaction is required.
- Expose background, locked, synchronizing, offline, and interaction-required states through an accessible notification-area experience.

## Browser integration

- Complete a separate browser-extension threat model before implementation or distribution.
- Use signed extensions and authenticated, minimal native-messaging contracts.
- Require explicit origin-to-item mapping; never offer all secrets to every page.
- Treat iframes, redirects, internationalized domains, lookalike hosts, and compromised extensions as hostile inputs.
- Require user action, policy approval, and local verification according to value sensitivity.
- Never read private browser password databases or bypass supported browser APIs.

## Write operations

- Keep write functionality disabled by default and separately governed from discovery and value retrieval.
- Define permissions and confirmations per supported operation rather than exposing a generic unrestricted mode.
- Revalidate identity, target, authorization, and current object version immediately before mutation.
- Record audit metadata without recording the written secret, key material, certificate private data, or token.
- Require independent security review and recovery guidance before public release.

## Clipboard

- Copy only after explicit action.
- Clear after a configurable short interval.
- Clear an unchanged application-owned value during orderly shutdown.
- Prevent a delayed clear from an older copy operation from clearing a newer clipboard value.
- Avoid restoring unrelated clipboard content unless platform behavior can be made reliable.
- Warn that clipboard history and cross-device clipboard services may retain data.
- Provide a policy to disable copying.

## Logging

- No tokens.
- No secret values.
- No private keys.
- No full certificate payloads.
- No decrypted cache content.
- Use identifiers only when needed and allow diagnostic pseudonymization.
- Redaction must be centralized and unit tested.

## Telemetry

- Disabled or minimal by default during early releases.
- Never include vault object names unless clearly opted in.
- Never include tenant IDs, subscription IDs, vault names, object names, usernames, or values in project-controlled telemetry by default.
- Publish a telemetry schema before enabling production telemetry.

## Release security

- Explicitly labeled unsigned Preview evaluation artifacts; trusted signing required for stable/GA.
- Protected release workflow.
- Dependency scanning.
- Secret scanning.
- Static analysis.
- SBOM.
- Provenance or attestation when practical.
- Documented vulnerability response.
