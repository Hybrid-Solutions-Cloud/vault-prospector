# Repository gotchas

- HCS governance currently returns `Path not found` for this checkout and has no repo profile for
  `vault-prospector`. Do not report a drift pass; use the applicable HCS hard rules and record the
  unresolved registry/work-item mapping.
- Pushing to the `Hybrid-Solutions-Cloud` GitHub organization requires an HCS-governance-minted
  GitHub App installation token. Never use a personal PAT.
- The full infrastructure test project intentionally takes about 90 seconds; allow at least five
  minutes for the locked build with coverage.
- Browser fill remains disabled in packages until an administrator edits the protected
  `browser-fill-policy.json`; portable output is outside the trusted Program Files root and
  therefore fails closed.
- The browser extension public key determines the reviewed Chromium extension ID. Never replace the
  key or IDs casually, and never commit a private extension signing key.
- Existing security, signing, live-service, usability, accessibility, and store gates must remain
  visibly open until their exact evidence exists.

