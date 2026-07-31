import { h } from 'vue'
import DefaultTheme from 'vitepress/theme'
import './style.css'

// Site-wide banner. Vault Prospector is Preview software under active development and every page
// must say so, not just the home page.
export default {
  extends: DefaultTheme,
  Layout() {
    return h(DefaultTheme.Layout, null, {
      'layout-top': () =>
        h('div', { class: 'vp-wip-banner' }, [
          h('strong', null, 'Work in progress.'),
          ' Vault Prospector is Preview software under active development — unsigned packages, breaking changes between releases, not for production use. ',
          h('a', { href: '/vault-prospector/downloads' }, 'What that means'),
        ]),
    })
  },
}
