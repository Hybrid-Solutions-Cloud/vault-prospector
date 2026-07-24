# Durable decisions

- Metadata is stored in SQLCipher; optional offline values are separate authenticated AES-GCM
  envelopes; DPAPI protects local keys. Existing encrypted state never falls back to plaintext or
  silently mints a replacement key.
- Interactive and workload identities use app-owned isolated contexts. Terminal Azure CLI,
  PowerShell, and IDE authentication state is not an application credential source.
- Azure provider behavior is read-only by default. Governed mutation remains disabled until the
  Phase 8 review and operation-specific controls/evidence exist.
- Browser fill is toolbar-initiated and one-shot. It requires exact machine policy, encrypted local
  mapping, authenticated native messaging, visible desktop confirmation, unchanged page context,
  and fresh Windows verification. Browser credential databases are never read.
- Password-manager UI concept selection requires representative usability and assistive-technology
  evidence; internal preference is not a substitute.
- Cross-device local encrypted-state migration is unsupported; resynchronization is the current
  recovery decision.

Detailed architectural decisions live under `docs/adr/`.

