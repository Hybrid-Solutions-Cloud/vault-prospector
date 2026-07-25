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

- every current-release backlog story is implemented or has an explicitly approved removal from
  scope; future-roadmap stories remain separately prioritized and do not block the current release;
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

- The HCS standards define GitHub Actions on the HCS Azure runner for portable validation and an
  ephemeral HCS Windows VM for Windows-only packaging; Azure DevOps Boards remains the work-item
  system, not the delivery runner.
- The free trusted Windows path is Microsoft Store–signed MSIX. Partner Center certification,
  independent security/legal review, live Azure matrices, package-catalog acceptance, and named
  approval require external or human evidence.
- CyberArk and native mobile applications are future-roadmap products and do not block Windows GA.
