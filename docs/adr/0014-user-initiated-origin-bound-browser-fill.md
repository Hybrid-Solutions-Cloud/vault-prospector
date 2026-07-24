# ADR-0014: Use a user-initiated, origin-bound browser fill boundary

**Status:** Proposed  
**Date:** 2026-07-23  
**Deciders:** Vault Prospector product owner, maintainers, browser-store reviewers, and independent
security reviewer

## Context

Vault Prospector may help a user place a specifically approved secret into a browser field. A
browser extension has access to hostile pages, frames, redirects, and browser profiles. A native
messaging host crosses from that environment into the local desktop process, which can retrieve
Azure values after Windows verification. Treating an extension request as trusted would create a
confused-deputy path from any compromised page or extension update to every accessible secret.

Chromium's `activeTab` permission grants temporary access only after a user gesture and revokes it
when the tab navigates to a different origin. Both Chromium and Firefox support native messaging
through a browser-launched standard-input/standard-output process. The browser host manifest can
allow only specific signed extension identities, but native messaging does not independently prove
that a claimed page origin is genuine. The extension must therefore derive page context from
browser-supplied sender and tab data, while the desktop must still require an exact stored mapping,
policy approval, visible confirmation, and fresh local verification.

The documented browser extension APIs expose password-manager settings, not a supported API for
reading or synchronizing the browser's private password database. Direct database access would
bypass browser protection, locking, profile, and update boundaries.

## Proposed decision

Implement browser fill as an explicit one-shot operation:

1. the user invokes the signed extension in the active tab;
2. the extension derives the current top-frame and target-frame context from browser APIs;
3. the extension asks the native host for one exact origin, field purpose, and stored mapping;
4. the native host validates a bounded, versioned message and forwards it to the already-running
   desktop process over a current-user-only authenticated channel;
5. the desktop revalidates the mapping, policy, identity, item, tab/frame/document nonce, and
   foreground state;
6. the desktop shows the normalized origin and exact mapped item, then requires fresh Windows
   verification;
7. the desktop returns one value for one request;
8. the extension verifies that the tab, frame, document, origin, and field still match before
   assigning the value and dispatching the minimum compatible input events; and
9. every component clears its transient value buffer and records only value-free audit context.

The extension uses `activeTab`, `scripting`, and `nativeMessaging`; it does not request
`<all_urls>`, background web-request interception, browsing history, cookies, clipboard, downloads,
or password-manager permissions. No content script can call native messaging directly. The
background component creates messages only from browser-supplied sender/tab/frame data and accepts
requests only during a short-lived user-gesture transaction.

An origin mapping contains an HTTPS origin, a top-frame requirement, an exact field purpose, a
Vault Prospector item and access path, and a policy state. Wildcards, HTTP origins, opaque origins,
IP literals, user-info, non-default ports unless explicitly approved, and public-suffix-wide
mappings are rejected. Internationalized domain names are stored and displayed in both canonical
ASCII and Unicode forms after confusable review.

Browser-vault import, export, and synchronization are not implemented. The application never opens
Chrome, Edge, Firefox, or other browser profile databases. A future one-way handoff may use a
documented public browser API only after a separate decision and consent design.

Distribution remains disabled until the extension identities, store signing, update ownership,
native-host installer registration, compromise response, live browser matrix, and independent
security review pass.

## Options considered

### Persistent access to every site

Rejected. It grants unnecessary continuous page access and magnifies extension-compromise impact.

### Page content script calls the native host

Rejected. Browser APIs allow native messaging only from extension contexts, and accepting
page-constructed security context would make hostile page data authoritative.

### Native host reads the local encrypted repository directly

Rejected. It would duplicate unlock and value-access logic, allow a browser-launched process to
retrieve values without the visible desktop boundary, and expand key exposure.

### Read or synchronize the browser password database

Rejected. No supported cross-browser extension API provides this boundary, and private-profile
database access violates the project security requirements.

### User-initiated one-shot fill

Proposed. It minimizes permissions and exposure while retaining explicit origin, mapping, policy,
identity, and user-presence checks.

## Consequences

- Fill is deliberately less automatic than a general-purpose password manager.
- The desktop application must be running, unlocked in the foreground, and able to present Windows
  verification.
- Frame and navigation changes invalidate an outstanding request; the user must invoke fill again.
- Browser families require separate signed manifests and native-host registrations while sharing
  a reviewed protocol and common extension source where supported.
- Compromise of a signed extension remains a material threat. Store-account protection, reproducible
  packaging, reviewed updates, revocation, and emergency native-host disablement are release
  controls, not optional operations work.

