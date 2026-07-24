# Vault Prospector agent instructions

## MCP-first bootstrap

At session start, use the HCS Governance MCP server to determine the standards that apply to
`vault-prospector`, then read `.ai/state/CURRENT_TASK.md`, `.ai/state/HANDOFF.md`,
`.ai/state/OPEN_QUESTIONS.md`, and `.ai/memory/`. If the MCP server cannot resolve this repository,
follow its HCS hard rules and the fallback instructions here, and record that drift was not
verified.

## Repository authority

- `docs/product/project-charter.md` defines mission and non-goals.
- `pmo/backlog.md` is the canonical story inventory.
- `pmo/plan.md` defines implementation order and exit criteria.
- `docs/product/release-readiness.md` defines release gates.
- `.ai/state/GOAL.md` defines the continuing completion objective.
- `.ai/state/HANDOFF.md` is the cross-tool execution record.

## Hard rules

- Never commit secrets, tokens, credentials, subscription IDs, connection strings, or live
  protected values.
- PowerShell scripts require PowerShell 7, strict mode, and stop-on-error behavior.
- Documentation is Markdown. Diagrams use draw.io source plus any exported PNG.
- Commit messages use `type(scope): short description`; include `AB#<id>` when a mapped ADO work
  item exists.
- Use a feature/fix/docs/chore branch and passing CI before merging to protected `main`.
- Keep Azure mutation, browser distribution, trusted signing, and other security-sensitive
  behavior behind their documented review and release gates.
- Never turn incomplete live, independent, signed-artifact, store, or participant evidence into a
  passing claim.

## Local verification

Run `pwsh ./scripts/Build.ps1 -Configuration Release`. For browser work also run `npm test` and
`npm run build` under `browser-extension`, then execute the three installer validation scripts
against the exact locally packaged MSI.

Full HCS standards: <https://platform.hybridsolutions.cloud>.

