# ADR-0010: Govern Azure mutations per operation

**Status:** Proposed  
**Date:** 2026-07-23  
**Deciders:** Vault Prospector product owner, maintainers, and independent security reviewer

## Context

Vault Prospector is read-only by default. A connected Azure identity may already hold broad
permissions, but that must not make mutation controls appear or become executable. Secret values,
key material, certificate material, Azure role assignments, and identity provisioning have
different authorization, concurrency, rollback, and audit requirements.

A generic write-mode switch would combine these risks, make least privilege difficult to explain,
and allow one approval to authorize unrelated operations.

## Proposed decision

Model every supported Azure mutation as a separate capability and command. A command is executable
only after all of these checks succeed immediately before the provider call:

1. a machine-scoped administrator policy explicitly allows the exact operation and target scope;
2. the selected identity is enabled, uses the expected tenant, and completes fresh Azure
   reauthentication;
3. provider authorization is evaluated for the exact resource and operation;
4. Windows verification succeeds;
5. the user reviews a value-free preview naming operation, identity, tenant, subscription, resource
   group, vault, object, expected effect, concurrency token, and recovery guidance; and
6. the user types the preview's one-time confirmation phrase.

The first implementation set is intentionally narrow:

- create a new secret;
- create a new version of an existing secret with an expected-current-version precondition;
- create a new software-protected key version from an allowlisted key type and operations; and
- start a certificate-policy operation that contains no private key import.

Secret deletion/purge, key deletion/purge, certificate deletion/purge, private-key import, arbitrary
ARM operations, identity creation, and RBAC assignment are separate future capabilities and are
not implied by this decision.

Every attempt writes a value-free local audit event before and after the provider call. Failure
does not automatically retry a non-idempotent operation. Provider responses return the created
resource/version identifier and operation-specific recovery guidance.

No mutation capability may be enabled in a public build until the threat model and implementation
receive independent security approval and live Azure integration evidence.

## Options considered

### Generic write-mode toggle

Rejected. It authorizes unrelated operations, obscures the active target, and makes policy and
audit controls too broad.

### Infer write enablement from the identity's Azure roles

Rejected. Existing Azure permissions are necessary but not sufficient; application policy, local
verification, exact confirmation, and product release gates remain mandatory.

### Keep all mutation code permanently absent

Safest current behavior but does not satisfy the governed-operations requirement. Retained as the
public-build state until the complete gate passes.

### Separate capability pipelines

Proposed. This is more work but creates explicit authorization, preview, concurrency, audit,
recovery, and test boundaries for each operation.

## Consequences

- The UI cannot expose one unrestricted write switch.
- Policy parsing and audit storage become security boundaries and require fail-closed tests.
- Secret or private material must never enter previews, diagnostics, audit records, exception
  messages, or persistent command state.
- Live tests require disposable Azure resources and post-test inventory confirmation.
- Independent approval is a hard enablement gate, not documentation debt.
