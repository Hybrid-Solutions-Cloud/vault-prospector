# Windows external focus-return validation — 2026-07-17

**Scope:** Unreleased source candidate after `0.1.0-preview.2`

**Candidate label:** `0.1.0-focus.1`

**Status:** Internal remediation and runtime evidence; not complete Entra, Windows Hello, keyboard,
or independent accessibility sign-off

## Finding and remediation

Vault Prospector launches operating-system and browser-owned surfaces for Microsoft Entra sign-in
and Windows Hello verification. The main window previously depended on implicit platform behavior
to return keyboard focus after those surfaces closed. There was no application-level contract that
remembered the initiating control, waited for both operation completion and window reactivation,
and rejected a target that had become disabled, hidden, or detached.

The candidate now:

- remembers the most recently focused control through a window-level routed focus handler;
- captures the initiating control when an asynchronous application operation becomes busy;
- waits until the operation is complete and the main window is active;
- restores focus with keyboard navigation semantics only when the control remains focusable,
  effectively enabled, effectively visible, and attached to the visual tree;
- discards an invalid target instead of repeatedly forcing focus to stale context;
- unsubscribes from view-model and platform events when the window closes.

Four coordinator tests cover eligible restoration, deferral while the external surface retains the
foreground, invalid-target rejection, and recovery from the last remembered control when Windows
temporarily reports no focused element.

## Live Windows browser-cancellation path

The self-contained source candidate was copied to the Windows 11 Enterprise Evaluation 25H2 x64
test VM after backing up `%LOCALAPPDATA%\VaultProspector`. It was launched through the interactive
Explorer session. A same-session Windows UI Automation helper then:

1. selected **Identities**;
2. focused **Continue to Microsoft sign-in** and proved it had keyboard focus;
3. invoked the button, causing the system browser to take the foreground and the app to enter its
   busy state;
4. returned to Vault Prospector, where **Cancel** was the enabled cancellation action;
5. invoked **Cancel**, allowing the MSAL operation to observe its cancellation token;
6. waited for the main window to reactivate and queried the actual Windows accessibility tree.

The final UI Automation capture recorded:

| Field | Value |
| --- | --- |
| Process path | `C:\VP-FOCUS\VaultProspector.App.exe` |
| Window name | `Vault Prospector` |
| Sign-in button enabled | `true` |
| Sign-in button keyboard-focusable | `true` |
| Sign-in button has keyboard focus | `true` |
| Focused name | `Continue to Microsoft sign-in` |
| Focused control type | `Button` |

The JSON SHA-256 was
`5D9E6667F55EAC171A1735FE885BF54974D728B9CCDE7F5452DEF49658D59526`.
The framebuffer showed the visible keyboard focus indicator on the returned sign-in button and the
safe status `Operation cancelled.`; its SHA-256 was
`8042185FD52AEDA05857EFCDD0FFECFA2CE718D724ABA6853EED542E324D4E90`.

The system browser displayed its local first-run surface before an Entra page. No account,
credential, token, or personal browser data was entered. This run therefore proves external-browser
deactivation, application cancellation, reactivation, and initiating-control focus return; it does
not claim a completed or failed Entra authentication exchange.

## Automated verification and cleanup

Formatting verification passed. The Release solution build completed with 0 warnings and 0 errors.
All seven test projects passed 80/80 tests, including 37 application tests and the four new
focus-return coordinator cases.

The candidate and browser processes were stopped. Candidate-created local application state was
removed, the pre-test state matched and was restored by recursive relative-path, length, and SHA-256
inventory, and the exact guest and host test roots were removed. `TextScaleFactor` remained absent
and High Contrast remained off (`Flags=126`).

## Remaining evidence

This run closes the implementation gap for deterministic operation focus return and proves the
browser-cancellation path on real Windows. P-15 remains in progress. Still required are a completed
Entra handoff/return, live Windows Hello approval and cancellation, complete keyboard-only core-task
transcripts, NVDA and Narrator output, populated list/dialog/authentication target sampling,
additional custom contrast palettes, representative-user usability, final signed-candidate
repetition, and independent sign-off.
