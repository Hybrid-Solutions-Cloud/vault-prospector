# Repository Structure

```text
vault-prospector/
├── .ai/state/                    # durable agent handoff state
├── .github/workflows/            # CI, security analysis, and release automation
├── docs/
│   ├── adr/                      # accepted architecture decisions
│   ├── architecture/             # system and domain design
│   ├── product/                  # charter, requirements, backlog, and roadmap
│   ├── security/                 # requirements and threat model
│   └── spikes/                   # feasibility research
├── scripts/                      # PowerShell 7 build and packaging entry points
├── src/
│   ├── VaultProspector.App/
│   ├── VaultProspector.Application/
│   ├── VaultProspector.Domain/
│   ├── VaultProspector.Infrastructure/
│   ├── VaultProspector.Platform/
│   └── VaultProspector.Providers.Azure/
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
- Desktop and platform projects explicitly target Windows and support cross-compilation from HCS Tier 1 WSL. Provider-neutral projects and tests run as native `net9.0`; final Windows Hello runtime validation runs on Windows.
- A dynamic plugin system remains intentionally absent until a signing and trust model is accepted.
