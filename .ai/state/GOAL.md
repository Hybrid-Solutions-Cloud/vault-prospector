# Repository goal

## Objective

Fully implement every Vault Prospector backlog item in accordance with the canonical implementation
plan and project charter. Completion includes production code, user-accessible workflows, automated
and appropriate live testing, security and release evidence, and synchronized documentation.

Use the HCS governance MCP server for standards, governance, and external validation when required.
Do not report an HCS conformance result when the server cannot resolve this repository.

## Authoritative sources

1. `docs/product/project-charter.md` defines the mission, product goals, and non-goals.
2. `pmo/backlog.md` is the canonical story inventory.
3. `pmo/plan.md` defines dependency order, delivery rules, phase scope, and exit criteria.
4. `docs/product/release-readiness.md` records Preview and GA gates.
5. `.ai/state/HANDOFF.md` records the current execution state.

## Completion definition

The goal is complete only when:

- every backlog story is implemented or has an explicitly approved removal from scope;
- every capability has a reachable, understandable user workflow;
- required unit, integration, UI, accessibility, security, migration, live-service, install,
  upgrade, repair, rollback, and recovery checks pass;
- the exact released artifacts have the required signing, SBOM, provenance, package-catalog,
  independent-review, and supported-platform evidence;
- backlog, plan, readiness, architecture, security, operations, user, and release documentation
  agree with the exact released behavior; and
- the Phase 15 reliability thresholds and final GA approval are recorded.

Documentation, a prototype, an unverified local build, or a passing unit test alone does not satisfy
the goal.

## Execution order

Follow `pmo/plan.md` in dependency order. Stabilize the PMO baseline and Phase 2 first, then complete
Phases 3–7, 8–10, 11–13, 14, and 15. Security-sensitive or externally governed actions retain their
documented approval, threat-model, and audit requirements.

## Current constraints

- The HCS governance registry does not currently contain `vault-prospector`, and its MCP host cannot
  resolve `D:\git\hybrid-solutions-cloud\vault-prospector`; HCS drift must not be reported as passed.
- Trusted Windows signing, independent security review, package-catalog acceptance, live tenant and
  platform matrices, evaluator thresholds, and stability windows require external services or human
  evidence.
