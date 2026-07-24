# Current task

Complete delivery of the readiness integration candidate in accordance with `pmo/backlog.md`,
`pmo/plan.md`, the project charter, and `.ai/state/GOAL.md`.

PR `#22` now uses HCS Azure DevOps as the sole CI/CD system. Exact PR validation build `281` passed all
four jobs: Windows build/package and 370 tests, full-history secret scan, native iOS simulator
application plus credential-provider extension, and 44 managed mobile tests plus Android Release
App Bundle.

Next:

1. validate this evidence-only head in ADO and merge PR `#22`;
2. require ADO CI to pass on the exact `main` merge commit;
3. publish and verify the next immutable Preview through the Key Vault-backed ADO release pipeline;
4. synchronize public release documentation and package-manager status; and
5. remove the temporary HCS Windows fallback infrastructure.

Do not overstate the remaining clean-machine installed lifecycle, live Azure/CyberArk,
physical-device, independent security/legal, representative usability/accessibility, store
acceptance, operational exercise, or stability-window gates.
