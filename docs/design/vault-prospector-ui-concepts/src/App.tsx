import { useState } from 'react'
import './App.css'

type Concept = 'source' | 'search' | 'guided' | 'console'
type Screen = 'setup' | 'vault' | 'reveal' | 'settings'

const concepts: { id: Concept; name: string; intent: string }[] = [
  { id: 'source', name: 'A · Source-first', intent: 'Identity and tenant are always visible' },
  { id: 'search', name: 'B · Search-first', intent: 'Keyboard-led retrieval with progressive detail' },
  { id: 'guided', name: 'C · Guided tasks', intent: 'Stepwise safety for infrequent operators' },
  { id: 'console', name: 'D · Operations console', intent: 'Dense multi-vault status for administrators' },
]

const screens: { id: Screen; name: string }[] = [
  { id: 'setup', name: 'Setup' },
  { id: 'vault', name: 'Search' },
  { id: 'reveal', name: 'Secret reveal' },
  { id: 'settings', name: 'Settings' },
]

const items = [
  { name: 'sql-admin-password', vault: 'prod-eus-api', source: 'Contoso / Production', state: 'Current' },
  { name: 'stripe-webhook-key', vault: 'prod-eus-payments', source: 'Contoso / Production', state: 'Expires 8d' },
  { name: 'sap-client-certificate', vault: 'corp-shared', source: 'Fabrikam / Corporate', state: 'Current' },
]

function SourceBadge() {
  return (
    <div className="source-badge" aria-label="Active access path">
      <span className="source-dot" aria-hidden="true" />
      <span><b>Contoso operator</b><small>Tenant: contoso.com · Production</small></span>
    </div>
  )
}

function Setup({ concept }: { concept: Concept }) {
  const steps = ['Local verification', 'Connect identity', 'Choose scope', 'First metadata sync']
  return (
    <section className="screen setup" aria-labelledby="setup-title">
      <div className="eyebrow">SECURE START</div>
      <h2 id="setup-title">Connect without borrowing terminal credentials</h2>
      <p className="lede">Vault Prospector keeps a separate identity path and indexes metadata only. Passwords and client secrets are never requested.</p>
      <ol className={concept === 'guided' ? 'stepper vertical' : 'stepper'}>
        {steps.map((step, index) => <li className={index === 1 ? 'active' : index < 1 ? 'done' : ''} key={step}><span>{index + 1}</span>{step}</li>)}
      </ol>
      <div className="form-card">
        <label>Identity type<select defaultValue="Interactive user"><option>Interactive user</option><option>Certificate service principal</option></select></label>
        <label>Friendly label<input defaultValue="Contoso operator" /></label>
        <div className="notice"><b>Microsoft controls authentication.</b> Complete MFA and Conditional Access in the system browser.</div>
        <button className="primary">Continue to Microsoft sign-in</button>
      </div>
    </section>
  )
}

function VaultSearch({ concept, onReveal }: { concept: Concept; onReveal: () => void }) {
  const [query, setQuery] = useState('')
  const visible = items.filter(item => `${item.name} ${item.vault}`.includes(query.toLowerCase()))
  return (
    <section className="screen search-screen" aria-labelledby="search-title">
      <div className="search-heading"><div><div className="eyebrow">ENCRYPTED LOCAL INDEX</div><h2 id="search-title">Find a vault object</h2></div><span className="offline">Available offline</span></div>
      <label className="searchbox"><span aria-hidden="true">⌕</span><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Search names, tags, vaults, or tenants" /><kbd>Ctrl K</kbd></label>
      <div className="chips" aria-label="Search filters"><button>Secrets</button><button>Production</button><button>Favorites</button><button>Stale</button></div>
      <div className={concept === 'console' ? 'results dense' : 'results'}>
        <div className="result-header"><span>Name</span><span>Vault</span><span>Access path</span><span>State</span></div>
        {visible.map((item, index) => (
          <button className={`result ${index === 0 ? 'selected' : ''}`} key={item.name} onClick={onReveal}>
            <span><b>{item.name}</b><small>Secret · metadata only</small></span>
            <span>{item.vault}</span><span>{item.source}</span><span>{item.state}</span>
          </button>
        ))}
      </div>
    </section>
  )
}

function Reveal() {
  const [revealed, setRevealed] = useState(false)
  return (
    <section className="screen reveal-screen" aria-labelledby="reveal-title">
      <div className="eyebrow">SECRET · READ-ONLY</div>
      <h2 id="reveal-title">sql-admin-password</h2>
      <SourceBadge />
      <dl className="facts"><div><dt>Vault</dt><dd>prod-eus-api</dd></div><div><dt>Version</dt><dd>9f3b…c22a</dd></div><div><dt>Updated</dt><dd>2 hours ago</dd></div><div><dt>Expires</dt><dd>Not set</dd></div></dl>
      <div className="secret-field" aria-live="polite"><code>{revealed ? 'correct-horse-example' : '•••••••••••••••••••••'}</code><span>{revealed ? 'Hides in 10 seconds' : 'Value not retrieved'}</span></div>
      <div className="actions"><button className="primary" onClick={() => setRevealed(!revealed)}>{revealed ? 'Hide now' : 'Verify and reveal'}</button><button>Verify and copy</button><button>Favorite</button></div>
      <div className="warning"><b>Windows verification required.</b> Retrieval uses the access path above. Clipboard policy clears an unchanged app-owned value after 30 seconds.</div>
    </section>
  )
}

function Settings() {
  return (
    <section className="screen settings" aria-labelledby="settings-title">
      <div className="eyebrow">POLICY AND LIFECYCLE</div>
      <h2 id="settings-title">Settings</h2>
      <div className="setting-group"><h3>Security</h3><label className="toggle"><input type="checkbox" checked readOnly /><span>Require Windows verification</span><small>Mandatory for reveal, copy, and offline values</small></label><label className="toggle"><input type="checkbox" /><span>Encrypted offline cache</span><small>Disabled by default</small></label></div>
      <div className="setting-group"><h3>Background behavior</h3><label>When the window closes<select defaultValue="Ask every time"><option>Ask every time</option><option>Exit</option><option>Lock and continue in notification area</option></select></label><label className="toggle"><input type="checkbox" /><span>Metadata-only background sync</span><small>Never retrieves values</small></label></div>
      <div className="setting-group danger"><h3>Local recovery</h3><p>Archive encrypted local state before starting fresh. Recovery always requires typed confirmation and Windows verification.</p><button>Open recovery guidance</button></div>
    </section>
  )
}

function Shell({ concept, screen, setScreen }: { concept: Concept; screen: Screen; setScreen: (screen: Screen) => void }) {
  const content = screen === 'setup' ? <Setup concept={concept} /> : screen === 'vault' ? <VaultSearch concept={concept} onReveal={() => setScreen('reveal')} /> : screen === 'reveal' ? <Reveal /> : <Settings />
  if (concept === 'search') return <main className="shell command-shell"><header><div className="brand">VAULT PROSPECTOR</div><SourceBadge /></header><nav aria-label="Prototype screens">{screens.map(item => <button className={screen === item.id ? 'active' : ''} onClick={() => setScreen(item.id)} key={item.id}>{item.name}</button>)}</nav>{content}</main>
  if (concept === 'guided') return <main className="shell guided-shell"><aside><div className="brand">VAULT<br />PROSPECTOR</div><p>Secure Azure retrieval</p><nav aria-label="Prototype screens">{screens.map((item, index) => <button className={screen === item.id ? 'active' : ''} onClick={() => setScreen(item.id)} key={item.id}><span>{index + 1}</span>{item.name}</button>)}</nav><SourceBadge /></aside>{content}</main>
  if (concept === 'console') return <main className="shell console-shell"><header><div className="brand">VP / OPERATIONS</div><div className="health"><span>● 3 identities ready</span><span>● 18 vaults indexed</span><span>○ Read-only</span></div></header><nav aria-label="Prototype screens">{screens.map(item => <button className={screen === item.id ? 'active' : ''} onClick={() => setScreen(item.id)} key={item.id}>{item.name}</button>)}</nav>{content}</main>
  return <main className="shell source-shell"><aside><div className="brand">VAULT<br />PROSPECTOR</div><SourceBadge /><nav aria-label="Prototype screens">{screens.map(item => <button className={screen === item.id ? 'active' : ''} onClick={() => setScreen(item.id)} key={item.id}>{item.name}</button>)}</nav><div className="read-only">READ-ONLY POLICY<br /><small>Writes are unavailable</small></div></aside>{content}</main>
}

export default function App() {
  const [concept, setConcept] = useState<Concept>('source')
  const [screen, setScreen] = useState<Screen>('vault')
  return (
    <div className={`prototype concept-${concept}`}>
      <header className="prototype-bar">
        <div><b>Vault Prospector UI study</b><span>Interactive, synthetic-data concepts · not production</span></div>
        <div className="concept-picker" role="group" aria-label="Design concept">
          {concepts.map(item => <button title={item.intent} className={concept === item.id ? 'active' : ''} onClick={() => setConcept(item.id)} key={item.id}>{item.name}</button>)}
        </div>
      </header>
      <Shell concept={concept} screen={screen} setScreen={setScreen} />
      <footer><b>{concepts.find(item => item.id === concept)?.intent}</b><span>Use Setup, Search, Secret reveal, and Settings to compare each structure.</span></footer>
    </div>
  )
}
