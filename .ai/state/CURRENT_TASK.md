# Current task

Fully implement the canonical backlog in `pmo/backlog.md` in dependency order and within the
security/release gates defined by `pmo/plan.md`, the project charter, and `.ai/state/GOAL.md`.

Phases 11–13 browser integration, CyberArk Privilege Cloud, native mobile application/autofill
source, and the G-06 machine-managed enterprise-policy source are implemented. The enterprise
policy is on `feature/enterprise-policy` at implementation commit
`5d20399ce37370213fdf280a2b9ff97918fbf1ef`. PR #21 is open with local exact-source evidence;
hosted checks are blocked before step execution by the organization payment/spending limit and the
PR must remain unmerged until exact-head checks run and pass.

Do not merge any open readiness PR until exact-head checks execute and pass. GitHub-hosted jobs are
currently starting with zero steps because the organization has a payment/spending-limit block;
record that as an external infrastructure blocker, not a code failure. Continue the remaining
Phase 8–15 live-service/device, independent-review, signing, distribution, usability,
accessibility, reliability, and stability gates without substituting source or simulator evidence.
