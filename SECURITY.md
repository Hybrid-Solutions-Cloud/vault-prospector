# Security Policy

Vault Prospector is a security-sensitive application that may access and locally process Azure Key Vault metadata and secret material.

## Supported versions

Vault Prospector is currently a Preview product for non-production evaluation. Security fixes are
provided only for the latest published Preview version. Older Preview builds may be withdrawn when
a replacement is available.

No version is supported for production use until the repository's GA readiness gates are complete.
See the [support lifecycle](docs/support-lifecycle.md) and
[release-readiness matrix](docs/product/release-readiness.md).

## Reporting a vulnerability

Do not disclose suspected vulnerabilities in public issues, discussions, pull requests, or social media.

Email suspected vulnerabilities to <kris@hybridsolutions.cloud> with the subject
`Vault Prospector security report`. Do not include live credentials, tokens, private keys, or secret
values. Use synthetic reproduction data and ask for a secure transfer method if sensitive evidence
is necessary.

GitHub private vulnerability reporting should also be enabled when repository security settings and
the installed GitHub App permissions permit it. Until then, email is the private reporting channel.

A future security contact should request:

- A description of the issue.
- Reproduction steps.
- Affected platform and version.
- Expected impact.
- Any proposed remediation.
- Whether secret material may have been exposed.

The maintainer will attempt to acknowledge a report within three business days and provide an
initial severity assessment or request for additional evidence within seven business days. These
targets are Preview operational goals, not a contractual service-level agreement.

Confirmed issues are handled privately until a fix or effective mitigation is available. The
response includes affected versions, rotation or containment guidance when applicable, a new
immutable release, and coordinated public disclosure appropriate to the risk.

## Security boundaries

The project distinguishes between:

- Public source code and documentation.
- Locally encrypted metadata.
- OAuth tokens managed through platform and MSAL facilities.
- Opt-in cached secret values.
- In-memory decrypted values.
- Clipboard contents.
- Exported or shared content.
- Machine-managed provider, tenant, identity-type, clipboard, and offline-cache policy.
- Browser extension, native-host, desktop-broker, machine-policy, and page-origin boundaries.

Each boundary must be documented and tested before a production release.

The unreleased [machine-managed enterprise policy](docs/enterprise-policy.md) is read from HKLM,
never written by the application, and fails closed when an enabled policy is invalid or unreadable.

## Prohibited behavior

The application must never:

- Transmit secret values to project-controlled telemetry.
- Store access or refresh tokens in plaintext.
- Cache secret values without explicit user action or policy.
- Include secret values in crash reports.
- Include secret values in logs.
- Silently weaken encryption when a platform capability is unavailable.
- Bypass or weaken an applicable machine-managed enterprise policy from user settings.
- Offer a vault value to a browser without exact machine policy, local mapping, page-context,
  extension/host identity, visible confirmation, and fresh local-verification checks.

The unreleased browser boundary is defined in the
[browser integration threat model](docs/security/browser-integration-threat-model.md). Browser
password databases are out of scope and must never be scraped or parsed.

The unreleased CyberArk Privilege Cloud boundary is defined in the
[CyberArk provider threat model](docs/security/cyberark-provider-threat-model.md). Its local
revocation action fails closed and removes the protected local credential; administrators must
still revoke the service user or credential in CyberArk Identity to invalidate external access.

## Release withdrawal

A release is withdrawn when a reachable critical/high vulnerability exposes protected data,
bypasses required local verification, disables required encryption, or invalidates artifact trust.
Published assets are never replaced under the same version. The maintainer preserves evidence,
publishes containment guidance, rotates affected credentials, and issues a new version.
