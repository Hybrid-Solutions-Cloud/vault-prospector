import { defineConfig } from 'vitepress'

// Public documentation site for Vault Prospector.
//
// Scope: user-facing material only — what the tool is, how to install and use it, the roadmap,
// and release history. Internal engineering records (ADRs, spikes, release evidence, threat
// models, PMO backlog, legal reviews) and the Node design prototypes under docs/design are
// excluded from the build.
//
// `ignoreDeadLinks` is on because the published pages cross-link into those excluded internal
// documents, which remain readable in the repository on GitHub.
export default defineConfig({
  title: 'Vault Prospector',
  description:
    'Local-first Windows desktop app for discovering and searching Azure Key Vault metadata across multiple Entra identities, tenants, and subscriptions.',
  lang: 'en-US',
  base: '/vault-prospector/',
  cleanUrls: true,
  lastUpdated: true,
  ignoreDeadLinks: true,

  srcExclude: [
    'design/**',
    'adr/**',
    'spikes/**',
    'release-evidence/**',
    'legal/**',
    'architecture/**',
    'artifact-signing.md',
    'ci-build-environments.md',
    'cyberark-integration.md',
    'performance-and-scale.md',
    'release-checklist.md',
    'release-operations-runbook.md',
    'product/backlog.md',
    'product/preview-feedback.md',
    'product/product-requirements.md',
    'product/project-charter.md',
    'product/release-readiness.md',
    'product/release-scope.md',
    'security/browser-integration-threat-model.md',
    'security/cyberark-provider-threat-model.md',
    'security/governed-write-threat-model.md',
    'security/in-app-update-threat-model.md',
    'security/independent-review-plan.md',
    'security/mobile-threat-model.md',
    'security/remote-session-verification-threat-model.md',
    'security/reveal-verification-grace-threat-model.md',
    'security/security-requirements.md',
  ],

  head: [
    ['meta', { name: 'theme-color', content: '#0b5cab' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'Vault Prospector' }],
    [
      'meta',
      {
        property: 'og:description',
        content: 'Search Azure Key Vault metadata across every tenant you can sign in to.',
      },
    ],
  ],

  themeConfig: {
    nav: [
      { text: 'Guide', link: '/user-guide' },
      { text: 'Roadmap', link: '/product/roadmap' },
      {
        text: 'Releases',
        items: [
          { text: 'Changelog', link: '/changelog' },
          { text: 'Latest release notes', link: '/release-notes/0.3.0-preview.3' },
          {
            text: 'All releases on GitHub',
            link: 'https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases',
          },
        ],
      },
      { text: 'Download', link: '/downloads' },
    ],

    sidebar: [
      {
        text: 'Getting started',
        items: [
          { text: 'Downloads', link: '/downloads' },
          { text: 'Install and verify a release', link: '/release' },
          { text: 'Authentication setup', link: '/authentication' },
          { text: 'User guide', link: '/user-guide' },
          { text: 'Glossary', link: '/glossary' },
        ],
      },
      {
        text: 'Features',
        items: [
          { text: 'Browser integration', link: '/browser-integration' },
          { text: 'Mobile applications', link: '/mobile-applications' },
          { text: 'Windows package distribution', link: '/package-distribution' },
        ],
      },
      {
        text: 'For administrators',
        items: [
          { text: 'Enterprise policy', link: '/enterprise-policy' },
          { text: 'Support lifecycle', link: '/support-lifecycle' },
        ],
      },
      {
        text: 'Security and privacy',
        items: [
          { text: 'Privacy', link: '/privacy' },
          { text: 'Security model', link: '/security/threat-model' },
        ],
      },
      {
        text: 'Project',
        items: [
          { text: 'Roadmap', link: '/product/roadmap' },
          { text: 'Changelog', link: '/changelog' },
          {
            text: 'Release notes',
            collapsed: true,
            items: [
              { text: '0.3.0-preview.3', link: '/release-notes/0.3.0-preview.3' },
              { text: '0.2.0-preview.5', link: '/release-notes/0.2.0-preview.5' },
              { text: '0.2.0-preview.4', link: '/release-notes/0.2.0-preview.4' },
              { text: '0.2.0-preview.1', link: '/release-notes/0.2.0-preview.1' },
              { text: '0.1.1-preview.1', link: '/release-notes/0.1.1-preview.1' },
            ],
          },
        ],
      },
    ],

    search: { provider: 'local' },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Hybrid-Solutions-Cloud/vault-prospector' },
    ],

    editLink: {
      pattern:
        'https://github.com/Hybrid-Solutions-Cloud/vault-prospector/edit/main/docs/:path',
      text: 'Edit this page on GitHub',
    },

    footer: {
      message:
        'Preview software. Direct packages are unsigned and display Unknown Publisher — verify the published SHA-256 before installing.',
      copyright: 'Hybrid Solutions Cloud',
    },
  },
})
