# Laptop installation walkthrough

**Date:** 2026-07-25
**Release:** `0.2.0-preview.5`
**Participant:** Product owner using a real laptop installation

## Documentation requirements captured

- Publish a clear end-user installation and first-run walkthrough online.
- Provide the same guidance contextually inside the application.
- Explain how interactive identities work when a user has multiple Microsoft Entra tenants.
- Explain whether to add another interactive identity, reuse a guest account, or select a tenant
  exposed by an existing account.
- Explain what the user must do after identities are connected.
- Use the product owner's real questions and points of confusion to drive the documentation.
- Reconcile the implemented interface with the approved UI mockups; do not describe the current
  interface as matching the mockups without a comparison.

## Walkthrough observations

1. The product owner reached the **Identities** page.
2. The product owner asked whether `InteractiveUser` can be added more than once for
   multi-tenant use.
3. Two interactive identities were connected successfully.
4. The next unclear decision is whether any action remains on **Identities** before moving to
   another page.
5. The status bar says to select **Sync** to discover resources, but **Sync selected** is disabled.
   The interface does not explain that synchronization requires a highlighted identity whose
   displayed state is `Ready`, or identify which prerequisite is missing.
6. The connected identity displays `State: Ready`; readiness alone did not make it obvious whether
   the list row was actually selected.
7. The identity was confirmed highlighted and selected with `State: Ready`, but every identity
   action except **Cancel** remained disabled. The application therefore still reported itself as
   busy even though the status bar said `Connected ... Select Sync to discover resources.` This is
   a functional state-management defect or an uncommunicated in-progress operation, not merely a
   documentation problem.
8. Selecting **Cancel** cleared the stuck busy state. All applicable identity actions became
   enabled and the status bar changed to `Operation cancelled.` The recovery works, but a user
   should not need to cancel an apparently completed connection before the documented next step.
9. The busy/disabled state affected both connected identities, not only the highlighted identity.
   The application uses one global operation state, but the interface presents the restriction
   beside each identity without explaining that it is application-wide.
10. After the busy state was cleared, **Sync selected** started successfully. The status bar
    displayed the synchronization message and the progress indicator moved. Completion wording,
    duration, discovered counts, and error reporting still need to be captured.
11. Synchronization populated data and ended with
    `CompletedWithErrors: 2 vaults and 124 objects; 3 isolated errors.` The application exposed
    only the count during the walkthrough, leaving the user without an obvious way to inspect,
    export, or share the isolated failures.
12. An installation opened through an AVD or Remote Desktop session cannot be unlocked. The
    application explicitly reports that Windows verification is unavailable in Remote Desktop and
    instructs the user to reconnect at a console. That leaves legitimate AVD and remote
    administration use cases with no usable, policy-controlled unlock path.
13. The product owner asked what happens to local data after reinstalling the application. The
    current per-machine MSI removes program files but does not remove the per-user data under
    `%LOCALAPPDATA%\VaultProspector`. A same-account reinstall of the same or a newer compatible
    version is therefore intended to reopen the encrypted data with the same DPAPI-protected key.
    A different Windows account or device cannot decrypt that state, and downgrade is blocked.
    This behavior needs explicit product documentation and exact-package lifecycle validation.
14. The **Administration** page uses the term **workload identities** without explaining it.
    In this product, those are non-human Azure identities—managed identities and
    certificate- or federated-credential service principals—used by an application or Azure
    workload rather than an interactive person. The administration workflow is for discovering,
    assessing, and planning their access; it is not required for normal interactive-user use.
15. The term **workspace** is also not explained in the interface. A workspace is a local,
    user-defined grouping of connected identities, tenants, subscriptions, and vaults, such as a
    customer, employer, lab, or personal environment. It does not create or modify an Azure
    resource and does not grant access.
16. Revealing several secrets requires repeating Windows verification for every selected value.
    The current Reveal command does not cache a value: it retrieves the selected secret after
    verification, displays it for ten seconds, and disposes it. The separate **Cache offline**
    command stores an explicitly selected value in encrypted local storage and is not the desired
    solution for efficient consecutive reveals. The product owner requested a short,
    policy-controlled verification grace period that permits consecutive explicit reveals without
    silently retaining plaintext values.
17. Search presents **Tenant ID contains**, **Subscription ID contains**, and **Vault name
    contains** as free-text fields. The product owner expects populated selectors derived from the
    discovered identities, tenants, subscriptions, and vaults, with recognizable display names and
    identifiers rather than requiring exact text entry.
18. Minimizing the application leaves it represented in the Windows taskbar. The product owner
    previously requested that minimizing hide the window and taskbar entry while the process
    continues securely in the notification area. Close-to-notification-area exists, but no
    minimized-window transition is implemented.
19. **List service principals** retrieves the tenant-wide Microsoft Graph service-principal
    collection and includes Microsoft-owned first-party and infrastructure principals. The
    Administration workflow needs to default to relevant, customer-manageable workload candidates,
    clearly distinguish visibility from manageability, and provide safe search and filtering.
20. Browser integration requires the user to manually enter top-frame and target-frame origins,
    choose a field purpose, save a mapping, then invoke the extension and approve the desktop
    confirmation. The product owner considers that workflow too complicated. The browser should
    supply the current destination context and the application should guide a simple selection and
    approval flow while retaining origin binding, policy enforcement, explicit user action, and
    minimal value exposure.

## Product backlog requests from the walkthrough

1. Provide an in-application path to discover and safely install a newer supported release.
2. Provide privacy-safe diagnostics that users can inspect and export from inside the application,
   plus a documented external location and support-collection procedure when the interface cannot
   open.
3. Reconcile the shipped interface with the approved installation, setup/onboarding, and complete
   solution mockups. The current release still presents the older interface.
4. Correct the global busy-state defect observed after connecting identities.
5. Make synchronization errors inspectable and actionable instead of reporting only an isolated
   error count.
6. Support a secure, policy-controlled unlock method for AVD and Remote Desktop sessions without
   weakening local-data protection.
7. Document reinstall, upgrade, downgrade, Windows-account, and device-migration behavior, and
   validate it against exact packaged installers.
8. Explain advanced Administration concepts, especially workload identities, and distinguish them
   from normal interactive-user connections.
9. Explain workspaces in the interface at their first point of use, including what they organize
   and what they do not change in Azure.
10. Add a threat-modeled, policy-controlled verification grace period for consecutive explicit
    secret reveals without turning it into automatic plaintext or offline-value caching.
11. Replace free-text tenant, subscription, and vault filters with populated, accessible selectors
    backed by the current discovered metadata.
12. Hide a minimized application in the notification area while it remains securely running, and
    restore it predictably from the notification icon.
13. Filter service-principal discovery to relevant customer-manageable candidates by default,
    excluding Microsoft first-party infrastructure principals and explaining any optional broader
    view.
14. Redesign browser integration so the browser provides destination context and the user completes
    a guided select-and-fill workflow instead of manually constructing origin mappings.

## Work items created

The HCS split-source-of-truth model was followed on 2026-07-25: GitHub is the master for native
Bug issues, while ADO is the master for User Stories and Tasks.

| Finding or planned outcome | GitHub master | ADO work item | Parent |
|---|---|---|---|
| Global busy state after connecting identities | [#39](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/issues/39) | [AB#5572](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5572) | Feature AB#5269 |
| Isolated synchronization errors are not actionable | [#40](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/issues/40) | [AB#5573](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5573) | Feature AB#5274 |
| Preview UI diverges from the intended mockups | [#41](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/issues/41) | [AB#5574](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5574) | Feature AB#5283 |
| AVD and Remote Desktop sessions cannot unlock | [#42](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/issues/42) | [AB#5575](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5575) | Feature AB#5278 |
| Trusted in-application release updates | ADO master | [AB#5569](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5569) | Feature AB#5287 |
| Privacy-safe diagnostics and support bundles | ADO master | [AB#5570](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5570) | Feature AB#5274 |
| Approved installation, setup, and desktop experience | ADO master | [AB#5571](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5571) | Feature AB#5283 |
| Policy-controlled grace period for consecutive reveals | ADO master | [AB#5608](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5608) | Feature AB#5272 |
| Simpler browser setup and one-time fill | ADO master | [AB#5609](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5609) | Feature AB#5284 |
| Service-principal results include Microsoft infrastructure | [#43](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/issues/43) | [AB#5610](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5610) | Feature AB#5279 |
| Minimize does not hide the application in the notification area | [#44](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/issues/44) | [AB#5611](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5611) | Feature AB#5282 |
| Populated tenant, subscription, and vault search selectors | ADO master | Refined [AB#5312](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5312), Task [AB#5628](https://dev.azure.com/hybridcloudsolutions/Hybrid%20Solutions%20Cloud%20-%20Vault%20Prospector/_workitems/edit/5628) | Feature AB#5271 |

Thirty child Tasks, AB#5576 through AB#5605, decompose implementation, threat modeling, design
approval, contextual help, redaction, automated regression coverage, and exact-package validation.
All 37 new ADO items were verified as `New` in the root iteration with one correct parent, approved
tags, priorities, and the required Acceptance Criteria. Every ADO Bug has repro steps, severity, a
GitHub hyperlink, and a Related link. GitHub issues #39 through #42 are native Bug issues labeled
`ado-tracked` and contain their ADO mirror link.

The second walkthrough batch created 21 items, AB#5608 through AB#5628: two ADO User Stories, two
GitHub-master Bugs with ADO mirrors, and 17 child Tasks. Existing search Story AB#5312 was refined rather than
duplicated; its Acceptance Criteria now require populated tenant, subscription, and vault selectors
and its implementation Task is AB#5628. All 22 inspected records—the 21 new items plus refined
AB#5312—passed parent, iteration, tag, field, Acceptance Criteria, and link verification with zero
errors. GitHub issues #43 and #44 are native Bugs labeled `ado-tracked` with their ADO mirror links.

## Answers provided during the walkthrough

- **Reinstall:** the per-machine MSI removes or replaces installed program files, but it does not
  delete `%LOCALAPPDATA%\VaultProspector`. Reinstalling the same or a newer compatible version for
  the same Windows account is intended to preserve and reopen the encrypted metadata, identity
  cache, settings, logs, and any explicitly cached values. The DPAPI keys are bound to the Windows
  user, so copying this directory to another account or device is not a supported restore. The MSI
  also blocks installing an older version over a newer one.
- **Workload identity:** a non-human Azure identity used by software or an Azure workload, such as
  a managed identity or a service principal using a certificate or federated OIDC credential.
  Normal interactive-user use does not require the advanced Administration workflow.
- **Workspace:** a local user-defined grouping for identities, tenants, subscriptions, and vaults,
  such as a customer, employer, lab, or personal environment. A workspace does not create or modify
  anything in Azure and does not grant permission.

## Guidance to validate during this walkthrough

- A separately authenticated Microsoft Entra account is added as a separate interactive identity.
- An account with guest access may expose more than one tenant, so documentation must distinguish
  accounts from tenant memberships.
- Each connected identity must be synchronized before its Azure Key Vault metadata can appear in
  search.

## Open findings

- Record exactly how the connected-identity state is presented after authentication.
- Confirm whether synchronization is clearly discoverable and whether each identity must be
  selected individually.
- Improve the disabled synchronization state so the interface says whether the user must select an
  identity, reauthenticate it, enable it, wait for an operation, or resolve an enterprise-policy
  restriction.
- Record the next screen, wording, progress feedback, errors, and recovery path.
- Compare the complete real workflow against the approved mockups and create implementation work
  items for material differences.

## Complete UI redesign review artifact — 2026-07-25

- Location: `docs/design/vault-prospector-ui-redesign-2026-07-25/`.
- The self-contained `bundle.html` compares three complete directions:
  Compass (guided/default), Command Center (dense/search-first), and Atlas
  (workspace/source-first).
- Each direction implements the same 11 lifecycle screens from installation through setup and
  everyday administration. The flows incorporate every finding recorded in this walkthrough,
  including multi-identity setup, partial synchronization errors, discovered filter selectors,
  policy-controlled remote-session unlock, reveal verification grace, browser fill, notification
  area behavior, service-principal filtering, updates, logs, and support bundles.
- Validation passed: the production-style Vite build succeeded, the single-file review bundle was
  regenerated, and automated browser traversal rendered all 33 direction/screen combinations with
  zero console or page errors.
- Screenshots for install, setup, and daily search in all three directions are under the
  prototype's `screenshots/` directory.
- Product-owner decision remains open under AB#5571/AB#5587. Do not treat a visual direction as
  approved or begin the production Avalonia shell conversion until that decision is recorded.
