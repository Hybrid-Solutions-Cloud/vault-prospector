# Proposed Repository Structure

```text
vault-prospector/
├── .editorconfig
├── .github/
│   ├── ISSUE_TEMPLATE/
│   ├── workflows/
│   └── dependabot.yml
├── build/
├── docs/
│   ├── adr/
│   ├── architecture/
│   ├── product/
│   ├── security/
│   └── spikes/
├── src/
│   ├── VaultProspector.App/
│   ├── VaultProspector.Application/
│   ├── VaultProspector.Domain/
│   ├── VaultProspector.Infrastructure/
│   ├── VaultProspector.Platform/
│   ├── VaultProspector.Plugin.Abstractions/
│   └── VaultProspector.Providers.Azure/
├── tests/
│   ├── VaultProspector.Application.Tests/
│   ├── VaultProspector.Domain.Tests/
│   ├── VaultProspector.Infrastructure.Tests/
│   ├── VaultProspector.Security.Tests/
│   └── VaultProspector.Providers.Azure.Tests/
├── tools/
├── CONTRIBUTING.md
├── LICENSE
├── README.md
└── SECURITY.md
```

## Dependency direction

```text
App
 ├── Application
 ├── Platform
 └── Infrastructure composition

Application
 ├── Domain
 └── abstractions defined by Application or Domain

Infrastructure
 ├── Application
 └── Domain

Azure Provider
 ├── Application provider contracts
 └── Domain

Platform
 ├── Application platform contracts
 └── Domain

Domain
 └── no project dependencies
```

## Notes

- Provider SDK models must not leak into the domain.
- Database entities may differ from domain entities.
- Platform projects may be split per operating system if required.
- A plugin system should not be implemented before a signing and trust model is accepted.
