# Security Requirements

## Authentication

- Use Microsoft-supported OAuth and OpenID Connect libraries.
- Do not implement password collection.
- Support Conditional Access and MFA through interactive browser-based authentication.
- Avoid Resource Owner Password Credential flow.
- Do not assume refresh tokens are always available.
- Surface interaction-required states clearly.
- Remove token-cache entries when identities are removed.

## Authorization

- Never elevate Azure permissions.
- Do not create role assignments in the initial product.
- Evaluate access per identity, tenant, subscription, vault, object type, and operation.
- Treat metadata enumeration and value retrieval as distinct capabilities.
- Show the selected access path before or during retrieval.

## Local encryption

- Use authenticated encryption.
- Generate encryption keys with a cryptographically secure random generator.
- Store master or wrapping keys through platform-backed secure storage.
- Maintain key-version metadata for rotation.
- Never silently fall back to plaintext.
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

## Clipboard

- Copy only after explicit action.
- Clear after a configurable short interval.
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

- Signed artifacts.
- Protected release workflow.
- Dependency scanning.
- Secret scanning.
- Static analysis.
- SBOM.
- Provenance or attestation when practical.
- Documented vulnerability response.
