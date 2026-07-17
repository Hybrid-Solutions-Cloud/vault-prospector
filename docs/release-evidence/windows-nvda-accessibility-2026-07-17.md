# Windows NVDA accessibility validation — 2026-07-17

**Scope:** Unreleased source candidate after `0.1.0-preview.2`

**Final local candidate label:** `0.1.69`

**Status:** Internal NVDA remediation and runtime evidence; not a release artifact, complete
accessibility conformance assessment, Narrator result, or independent sign-off

## Test boundary and provenance

The test ran in the isolated `vp-win11-preview-test` Hyper-V guest on Windows 11 Enterprise
Evaluation 25H2 x64, version `10.0.26200`, with Secure Boot enabled and a present and ready TPM.
The final self-contained candidate was produced from the working source with `scripts/Package.ps1`,
launched from `C:\VP-NVDA\candidate69`, and was not installed or published. Its executable SHA-256
was `1F5D1AAC496B839FF286E137EDA696B5EB1A319BF6686449D111666D39D592C4`.

The screen reader was the official NVDA `2026.1.1` portable copy created from
`nvda_2026.1.1.exe`. The downloaded installer was signed by **NV Access Limited** and matched the
official SHA-256
`6E0289EB5A3AA076EB97EA99C5D5465CB48B5ECC6A3257DC3D811F881A1747C9`.
It ran with add-ons disabled and debug logging enabled. NVDA usage telemetry was explicitly off.
See the [NVDA 2026.1.1 release notice](https://www.nvaccess.org/post/nvda-2026-1-1/) and
[official stable downloads](https://download.nvaccess.org/releases/stable/).

The guest has no audio render endpoint. NVDA therefore logged expected audio-device errors. This
record proves calls to NVDA's speech queue through `speech.speech.speak` and Speech Viewer; it does
not claim that audible sound was rendered.

After validation, the test processes and four `VP-*` scheduled tasks were removed, MSI `0.1.56`
was uninstalled, and the test root was deleted. The original database and DPAPI-protected metadata
key were restored with byte-identical SHA-256 hashes. Final inspection found no Vault Prospector,
Edge, or NVDA process; no Vault Prospector ARP entry, installation directory, shortcut, or test
root; no Windows text-scale override; and the original High Contrast `Flags=126` setting.

## Defects found and remediated

### Secondary-tab focus events

The Search tab exposed correct names, roles, states, and focus speech. On Identities and the other
selected tabs, Windows UI Automation exposed the focused controls and visual keyboard focus moved,
but NVDA received no focus event after Tab. Inspection of the pinned Avalonia `12.1.0` source found
that `WindowBaseAutomationPeer` could retain a null root focus when selected-tab content was not
accepted by its visual-ancestor check.

The remediation adds a window automation peer that, only for focused controls on secondary tabs,
synchronizes Avalonia's existing focused-control field and invokes its existing focus-change event
after routed focus settles. The bridge is deliberately narrow and pinned to Avalonia `12.1.0`;
regression coverage fails if the expected private field names or types change. It must be reviewed
or removed during any Avalonia upgrade.

### Actionable errors and live status

The original assertive error region was visible but did not produce a complete NVDA announcement.
The remediated banner focuses a real **Return to previous action** button whose automation name is
the complete redacted error, explanation, and recovery guidance. Enter restores the initiating
control. The button uses dark text on a light error-tint background so the focused control remains
readable on the dark alert panel.

Routine status remains a polite live region. Focus return is delayed by 900 milliseconds after a
successful or cancelled operation so NVDA can announce the status before the initiating control is
restored. Actionable errors do not overwrite the footer and therefore do not produce a duplicate,
title-only status announcement.

## Final candidate NVDA transcript

The final candidate produced these speech-queue entries during keyboard and UI Automation-driven
interaction:

```text
Settings tab selected
Save settings button
Settings saved locally. No client secret is stored.

Identities tab selected
Use my organization's own public-client registration check box checked
Continue to Microsoft sign-in button
Connection settings need attention. The custom Microsoft Entra application ID is missing or invalid.
Use the recommended Vault Prospector registration, or enter the Application (client) ID from your
organization's public-client registration. button

Cancel button
Cancelling the active operation…
Operation cancelled.
Continue to Microsoft sign-in button

Friendly identity label edit Customer, employer, lab…
Use my organization's own public-client registration check box not checked
```

The error-return control was then activated with Enter. NVDA announced
`Continue to Microsoft sign-in button`, proving return to the initiating action. A separate final
system-browser run opened the Microsoft sign-in surface, entered the app busy state, and exposed
**Cancel**. Cancelling produced the status and focus sequence shown above.

The final cancellation framebuffer showed `Operation cancelled.` and the visible keyboard focus
indicator on **Continue to Microsoft sign-in**; its SHA-256 was
`CE15EC8D4593F1252C960FA274CA48627B5FBB9CB1EECA906655DB46151E9B7C`.
The final Identities traversal framebuffer SHA-256 was
`F090BB07E5F1FBE357BA13701B71543901F2803591113CE2E97BF2F9ADF4F292`.

## Automated verification

The locked Release gate completed with no known vulnerable direct or transitive NuGet packages,
formatting unchanged, 0 build warnings, 0 build errors, and all seven test projects passing 84/84
tests. New coverage protects:

- the pinned Avalonia focus fields and named main tab control;
- accessible polite status and assertive error markup;
- complete redacted error-announcement composition;
- error focus return to the initiating action;
- focus-target capture, deferral, invalid-target rejection, failure handling, and requested return.

## Remaining P-15 work

This run closes the internally observed NVDA silence on secondary tabs, complete actionable-error
announcement, routine status announcement, browser cancellation status, and initiating-control
return. P-15 remains in progress. Still required are Narrator, a completed Entra handoff and return,
live Windows Hello approval and cancellation, populated result/list and dialog sampling, complete
keyboard-only core-task transcripts, additional custom contrast palettes, representative-user
usability, final signed-candidate repetition, and independent accessibility sign-off.
