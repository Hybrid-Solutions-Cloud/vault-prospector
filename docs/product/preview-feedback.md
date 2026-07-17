# Preview Feedback and GA Promotion

The first formal feedback window began at `2026-07-17T20:43:21Z`, when `0.1.1-preview.1` became
public; its full credential-free asset verification subsequently passed. Unpublished candidates
and the superseded CI.68 test release do not count toward G-01.

This process defines the consent, privacy, triage, and measurable evidence required for the
Preview reliability and feedback gate (G-01). It does not replace the other GA gates in
[release readiness](release-readiness.md).

## Collection boundary

Vault Prospector sends no project-controlled telemetry. Feedback is voluntary and user initiated
through the public release repository:

- [Standard Bug intake](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/issues/new?template=bug.yml)
- [Standard Feature intake for experience improvements](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/issues/new?template=feature.yml)
- [Private security reporting](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/blob/main/SECURITY.md)

The public feedback notice states that submitting is an explicit, voluntary publication action and
requires synthetic or non-production data with prohibited sensitive content removed. It provides a
structured block for user-entered product version, installation path, Windows version, task,
outcome, ease, and sanitized friction. The repositories use the HCS-governed standard Bug, Feature,
and Task forms, native issue types/fields, and only reserved workflow labels. GitHub processes the
public issue under its own terms. The application never submits it.

Do not request or accept a credential, token, secret value, private key, tenant/subscription/account
identifier, Azure resource/object name, unreviewed diagnostic, database, crash dump, clipboard
content, or sensitive screenshot in a public report. Move a suspected vulnerability to the private
security process immediately and redact any accidental disclosure using repository-administration
controls.

## Triage and ownership

The release owner reviews new public feedback each business day and targets initial classification
within three business days. Every report receives:

1. affected version and installation path;
2. task, reproducibility, and supported-Windows scope;
3. severity and security/privacy screening;
4. an engineering work-item link or a documented non-actionable/duplicate decision; and
5. a release-blocking or non-blocking disposition.

Severity and response expectations are:

| Class | Definition | Required action |
| --- | --- | --- |
| Security-sensitive | Suspected disclosure, protection bypass, authorization failure, or sensitive evidence | Stop public triage, preserve only sanitized metadata, and use the private security process |
| Release blocker | Install/launch failure, data loss, core task unavailable without workaround, or supported upgrade failure | Assign immediately; no Preview refresh or GA promotion while unresolved |
| Major | Core task materially impaired but a safe workaround exists | Assign an owner and target version before GA decision |
| Minor | Non-blocking usability, documentation, or cosmetic issue | Triage and prioritize explicitly; may be deferred with rationale |

The owner records a weekly sanitized rollup during an active Preview: new, closed, outstanding, age,
severity, task outcomes, installation paths, and supported Windows builds. Counts come from explicit
issues only and must never be described as population telemetry.

## Measurable G-01 exit criteria

G-01 can move to **Passed** only when all of the following evidence exists for the latest GA
candidate and is linked from the release-readiness matrix:

- The public feedback intake and private security channel have remained operational for at least 30
  consecutive days after the latest Preview refresh.
- At least five distinct consenting evaluators have submitted experience feedback spanning at least
  three supported Windows 11 build/edition combinations and all three intended install paths: direct
  MSI, WinGet, and Chocolatey.
- At least 20 recorded core-task attempts cover install/update, first-run sign-in, discovery/sync,
  search, reveal/copy with synthetic values, offline cache/purge, and identity or local-data removal.
- At least 90% of those attempts are completed without maintainer intervention; 100% of failures
  have a triaged work item and disposition.
- 100% of public Preview reports are triaged within three business days, or an exception with owner,
  reason, and corrective date is recorded in release evidence.
- There are zero unresolved security-sensitive reports, release blockers, critical/high security
  findings, data-loss defects, authentication/authorization boundary failures, or encryption and
  user-verification bypasses.
- Every supported published Preview version upgrades to the latest candidate through direct MSI,
  WinGet, and Chocolatey on clean supported Windows, and uninstall/reinstall preserves or removes
  local state exactly as documented.
- The latest candidate completes a final 14 consecutive days without a newly confirmed release
  blocker; a replacement candidate restarts this stability window.

Evaluator count and task count are minimum evidence thresholds, not claims of statistical product
quality. GA still requires every other gate, independent review, and formal approval.

## Evidence record

For each weekly rollup and final G-01 decision, record the date range, candidate version and hash,
public issue query or IDs, unique consenting-evaluator count, task-attempt denominator and outcomes,
Windows/install-path coverage, triage latency, defect disposition, upgrade matrix, stability-window
dates, owner, and reviewer. Use sanitized identifiers in the private release-evidence record and do
not copy issue content that violates this policy.
