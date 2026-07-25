# Repository Structure

```text
vault-prospector/
├── .ai/state/                    # durable agent handoff state
├── .github/workflows/            # GitHub Actions on HCS-owned build environments
├── .github/                      # issue templates and dependency-update configuration
├── docs/
│   ├── adr/                      # accepted architecture decisions
│   ├── architecture/             # system and domain design
│   ├── product/                  # charter, requirements, backlog, and roadmap
│   ├── security/                 # requirements and threat model
│   └── spikes/                   # feasibility research
├── mobile/
│   ├── VaultProspector.Mobile/   # shared Avalonia touch UI
│   ├── VaultProspector.Mobile.Core/
│   ├── VaultProspector.Mobile.Android/
│   ├── VaultProspector.Mobile.iOS/
│   └── VaultProspector.Mobile.Tests/
├── scripts/                      # PowerShell 7 build and packaging entry points
├── src/
│   ├── VaultProspector.App/
│   ├── VaultProspector.Application/
│   ├── VaultProspector.Domain/
│   ├── VaultProspector.Infrastructure/
│   ├── VaultProspector.Platform/
│   ├── VaultProspector.Providers.Azure/
│   └── VaultProspector.Providers.CyberArk/
├── tests/
│   ├── VaultProspector.Application.Tests/
│   ├── VaultProspector.Domain.Tests/
│   ├── VaultProspector.Infrastructure.Tests/
│   ├── VaultProspector.Providers.Azure.Tests/
│   └── VaultProspector.Security.Tests/
├── Directory.Build.props         # shared compiler, analyzer, lockfile, and version policy
├── global.json                   # HCS-selected .NET SDK baseline
├── NuGet.config                  # repository-scoped package source policy
└── VaultProspector.sln
```

## Dependency direction

```text
App ───────────────┬──> Application ──> Domain
                   ├──> Infrastructure ─┘
                   ├──> Platform ───────┘
                   └──> Azure Provider ─┘
```

- `Domain` has no project dependencies.
- `Application` owns the provider, persistence, clipboard, verification, key-material, clock, and diagnostic contracts.
- Infrastructure and provider SDK models never leak into the domain.
- `App` is the composition root and the only project that wires concrete implementations together.
- The Android and iOS projects are separate mobile composition roots. They reuse portable
  contracts and providers but own platform key storage, verification, clipboard, lifecycle,
  authentication callback, and privacy behavior.
- Desktop and platform projects explicitly target Windows and support cross-compilation from HCS
  Tier 1 WSL. Provider-neutral projects and tests run as native `net10.0`; final Windows Hello
  runtime validation runs on Windows.
- A dynamic plugin system remains intentionally absent until a signing and trust model is accepted.
