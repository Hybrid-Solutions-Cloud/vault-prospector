# ADR-0013: Report effective Azure authorization evidence without simulating access

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Vault Prospector product owner and maintainers

## Context

Workload-identity discovery proves that the selected interactive administrator can list an
identity, but listing alone does not prove permission to attach or manage that identity, manage
role assignments, or use it against an exact Key Vault. Azure role assignments can be inherited,
assigned through transitive groups, constrained by conditions, and overridden by deny
assignments. Key Vault can also use either Azure RBAC or the legacy access-policy model.

Azure exposes the effective permissions of the current caller at a resource, but it does not expose
a general-purpose API that safely impersonates an arbitrary managed identity or service principal.
Static role evidence therefore cannot be described as a successful runtime access test.

## Decision

- Permission assessment is an explicit, read-only action for one discovered candidate and one
  exact Key Vault resource ID.
- The selected administrator's attach/use, identity-management, and role-assignment-management
  capabilities use Azure's caller-permissions endpoint at the exact applicable resource.
- Candidate Key Vault evidence uses applicable role assignments with `assignedTo`, which includes
  transitive group assignments, plus each referenced role definition and applicable deny
  assignments.
- The evaluator applies assignment scope, `Actions`, `NotActions`, `DataActions`,
  `NotDataActions`, direct principal exclusions, and child-scope behavior.
- A matching unconditional deny takes precedence over an allow. A condition, unreadable deny set,
  potentially applicable group deny, unsupported permission model, or incomplete response is
  reported as conditional or incomplete and cannot produce an allowed result.
- Role-assignment evidence distinguishes Key Vault metadata listing from secret-value retrieval.
  It does not retrieve a secret, acquire a token as the candidate, or enable any write operation.
- Key Vault access-policy mode is detected and reported as incomplete rather than interpreted as
  Azure RBAC.
- Every result records the exact scope, subject, evidence state, non-sensitive basis, and
  observation time. Assessments are transient and are not treated as durable authorization.

## Options considered

### Infer access from role names

Rejected. Custom roles, `NotActions`, inheritance, conditions, and deny assignments make role
names insufficient.

### Acquire a credential and probe as every candidate

Rejected. A discovered candidate does not imply credential possession or local attachability, and
probing could cross a security boundary that the administrator did not authorize.

### Report only runtime observations after connecting the identity

Retained as the strongest data-plane evidence, but insufficient for the administration workflow.
It cannot explain attach, manage, or role-assignment permissions before connection.

### Combine caller permissions with fail-closed static candidate evidence

Accepted. This uses the strongest supported evidence for each subject while preserving the
difference between an observed authorization graph and a runtime operation.

## Consequences

- Administrators can inspect materially stronger permission evidence without changing Azure.
- Conditional and ambiguous authorization remains visible instead of being flattened into an
  unsafe yes/no answer.
- An exact runtime result can still differ because of token state, network policy, Key Vault
  configuration, or authorization changes after observation.
- Microsoft Graph directory-role and application-ownership analysis remains separate from Azure
  Resource Manager authorization.

## References

- [Azure RBAC overview](https://learn.microsoft.com/azure/role-based-access-control/overview)
- [List Azure role assignments using REST](https://learn.microsoft.com/azure/role-based-access-control/role-assignments-list-rest)
- [Permissions - List for Resource](https://learn.microsoft.com/rest/api/authorization/permissions/list-for-resource)
- [Deny Assignments - List for Resource](https://learn.microsoft.com/rest/api/authorization/deny-assignments/list-for-resource)
- [Azure role assignment conditions](https://learn.microsoft.com/azure/role-based-access-control/conditions-overview)
- [Azure Key Vault RBAC guide](https://learn.microsoft.com/azure/key-vault/general/rbac-guide)
