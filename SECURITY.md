# Security Policy

Vault Prospector is a security-sensitive application that may access and locally process Azure Key Vault metadata and secret material.

## Supported versions

No production version is currently supported. This repository is in architecture and early development.

## Reporting a vulnerability

Do not disclose suspected vulnerabilities in public issues, discussions, pull requests, or social media.

Until a private security contact is published, repository maintainers should enable GitHub private vulnerability reporting before accepting external security reports.

A future security contact should request:

- A description of the issue.
- Reproduction steps.
- Affected platform and version.
- Expected impact.
- Any proposed remediation.
- Whether secret material may have been exposed.

## Security boundaries

The project distinguishes between:

- Public source code and documentation.
- Locally encrypted metadata.
- OAuth tokens managed through platform and MSAL facilities.
- Opt-in cached secret values.
- In-memory decrypted values.
- Clipboard contents.
- Exported or shared content.

Each boundary must be documented and tested before a production release.

## Prohibited behavior

The application must never:

- Transmit secret values to project-controlled telemetry.
- Store access or refresh tokens in plaintext.
- Cache secret values without explicit user action or policy.
- Include secret values in crash reports.
- Include secret values in logs.
- Silently weaken encryption when a platform capability is unavailable.
