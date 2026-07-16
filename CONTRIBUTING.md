# Contributing to Vault Prospector

Vault Prospector is being designed as a security-sensitive open-source project. Contributions are welcome, but changes affecting authentication, encryption, secret handling, clipboard behavior, local storage, logging, telemetry, or provider permissions require additional review.

## Before contributing

1. Read the project charter and architecture overview.
2. Review the active architecture decision records.
3. Search the backlog and existing issues before creating a new proposal.
4. For significant architectural changes, create or amend an ADR before implementation.

## Development expectations

- Use clear, testable interfaces.
- Keep provider-specific logic outside the core domain.
- Never commit credentials, tokens, tenant identifiers, production vault names, or secret values.
- Do not log access tokens, refresh tokens, secret values, certificate private keys, or decrypted database content.
- Include tests for security-sensitive behavior.
- Prefer explicit error handling over silent fallback.
- Treat offline cached values as a separate security tier from metadata.

## Pull requests

A pull request should include:

- A concise problem statement.
- The proposed change.
- Relevant issue or ADR references.
- Test evidence.
- Security implications.
- Documentation updates when behavior changes.

## Architecture decisions

ADRs are stored in `docs/adr`.

Use the next available number and the format:

```text
NNNN-short-decision-title.md
```

Each ADR should include context, decision, consequences, alternatives, and status.

## Reporting security issues

Do not open a public issue for a suspected vulnerability. Follow the process in `SECURITY.md`.
