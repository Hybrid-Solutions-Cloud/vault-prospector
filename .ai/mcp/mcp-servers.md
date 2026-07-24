# MCP server inventory

## HCS Governance

Used for scope-aware HCS standards, deterministic repository drift checks, and short-lived
provider authentication tokens. No credential material is stored in this repository.

Current limitation: the server does not resolve the local `vault-prospector` checkout or a
repository registry profile, so deterministic drift and automatic ADO work-item mapping are not
available. Standards lookup by repository type still resolves the HCS governance, documentation,
project-management, agents, and AI-workspace rules.

