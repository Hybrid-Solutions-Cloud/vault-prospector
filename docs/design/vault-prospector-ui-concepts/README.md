# Vault Prospector desktop UI concepts

Interactive React prototype containing four distinct information-architecture concepts:

- A · Source-first
- B · Search-first
- C · Guided tasks
- D · Operations console

Each concept includes Setup, Search, Secret reveal, and Settings screens. All names and values are
synthetic. This is a research artifact, not production code and not an Azure client.

## Run

```powershell
npm install
npm run dev
```

Open the local URL printed by Vite. Use the concept buttons at the top and the screen navigation
inside each concept.

## Validate

```powershell
npm run build
```

The 2026-07-23 internal browser check exercised all 16 concept/screen combinations with no browser
console errors. A 390-pixel viewport had no horizontal document overflow. This is implementation
validation only; it is not representative-user or assistive-technology evidence.

## Research

- [Comparative research and synthesis](../desktop-ui-research-2026-07-23.md)
- [Representative-user usability study plan](../desktop-usability-study-plan.md)
