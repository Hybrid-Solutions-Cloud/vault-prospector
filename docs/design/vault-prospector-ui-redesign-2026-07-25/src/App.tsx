import { useMemo, useState, type ReactNode } from 'react'
import {
  Activity,
  AppWindow,
  ArrowLeft,
  ArrowRight,
  Bell,
  BookOpen,
  Boxes,
  BriefcaseBusiness,
  Check,
  CheckCircle2,
  ChevronDown,
  CircleAlert,
  CircleHelp,
  Cloud,
  CloudDownload,
  Copy,
  Download,
  Eye,
  EyeOff,
  FileArchive,
  Filter,
  Fingerprint,
  FolderKanban,
  HardDrive,
  Info,
  KeyRound,
  Laptop,
  ListFilter,
  LockKeyhole,
  MonitorDown,
  MoreHorizontal,
  Network,
  PackageCheck,
  RefreshCw,
  RotateCcw,
  Search,
  ServerCog,
  Settings,
  Shield,
  ShieldCheck,
  Sparkles,
  TerminalSquare,
  UserRound,
  Users,
  Vault,
  Globe2,
  Wifi,
  X,
  type LucideIcon,
} from 'lucide-react'
import './App.css'

type Direction = 'compass' | 'command' | 'atlas'
type Screen =
  | 'install'
  | 'unlock'
  | 'connect'
  | 'sync'
  | 'search'
  | 'reveal'
  | 'workspaces'
  | 'browser'
  | 'admin'
  | 'activity'
  | 'settings'

type NavigationItem = {
  id: Screen
  name: string
  shortName: string
  phase: 'Start' | 'Use' | 'Manage'
  icon: LucideIcon
}

const directions: Record<Direction, { name: string; label: string; description: string; bestFor: string }> = {
  compass: {
    name: 'Compass',
    label: 'A',
    description: 'Guided, calm, and progressive. The product explains the next safe action.',
    bestFor: 'Recommended for most users',
  },
  command: {
    name: 'Command Center',
    label: 'B',
    description: 'Search-first and information-dense, with operational status always visible.',
    bestFor: 'Power users and administrators',
  },
  atlas: {
    name: 'Atlas',
    label: 'C',
    description: 'Workspace and source context lead every screen for multi-tenant work.',
    bestFor: 'Consultants and customer environments',
  },
}

const navigation: NavigationItem[] = [
  { id: 'install', name: 'Install', shortName: 'Install', phase: 'Start', icon: PackageCheck },
  { id: 'unlock', name: 'Secure unlock', shortName: 'Unlock', phase: 'Start', icon: Fingerprint },
  { id: 'connect', name: 'Connect identities', shortName: 'Connect', phase: 'Start', icon: Users },
  { id: 'sync', name: 'Sync and health', shortName: 'Sync', phase: 'Start', icon: RefreshCw },
  { id: 'search', name: 'Find secrets', shortName: 'Search', phase: 'Use', icon: Search },
  { id: 'reveal', name: 'Reveal safely', shortName: 'Reveal', phase: 'Use', icon: Eye },
  { id: 'workspaces', name: 'Workspaces', shortName: 'Workspaces', phase: 'Use', icon: FolderKanban },
  { id: 'browser', name: 'Browser fill', shortName: 'Browser', phase: 'Use', icon: Globe2 },
  { id: 'admin', name: 'Administration', shortName: 'Admin', phase: 'Manage', icon: ServerCog },
  { id: 'activity', name: 'Activity and support', shortName: 'Activity', phase: 'Manage', icon: Activity },
  { id: 'settings', name: 'Settings and updates', shortName: 'Settings', phase: 'Manage', icon: Settings },
]

const searchRows = [
  {
    name: 'sql-admin-password',
    kind: 'Secret',
    vault: 'prod-eus-api',
    subscription: 'Contoso Production',
    tenant: 'Contoso',
    workspace: 'Customer · Contoso',
    state: 'Current',
    updated: '2h ago',
  },
  {
    name: 'stripe-webhook-key',
    kind: 'Secret',
    vault: 'prod-eus-payments',
    subscription: 'Contoso Production',
    tenant: 'Contoso',
    workspace: 'Customer · Contoso',
    state: 'Expires in 8d',
    updated: '1d ago',
  },
  {
    name: 'sap-client-certificate',
    kind: 'Certificate',
    vault: 'corp-shared',
    subscription: 'Fabrikam Corporate',
    tenant: 'Fabrikam',
    workspace: 'Corporate',
    state: 'Current',
    updated: '4d ago',
  },
  {
    name: 'demo-api-key',
    kind: 'Secret',
    vault: 'lab-demo',
    subscription: 'HCS Lab',
    tenant: 'Hybrid Solutions Cloud',
    workspace: 'Lab and demos',
    state: 'Stale',
    updated: '19d ago',
  },
]

const setupSteps = [
  { name: 'Local protection', state: 'complete' },
  { name: 'Connect identities', state: 'current' },
  { name: 'Choose discovery scope', state: 'upcoming' },
  { name: 'Synchronize metadata', state: 'upcoming' },
]

function Pill({
  children,
  tone = 'neutral',
}: {
  children: ReactNode
  tone?: 'neutral' | 'good' | 'warn' | 'danger' | 'accent'
}) {
  return <span className={`pill pill-${tone}`}>{children}</span>
}

function SectionHeading({
  eyebrow,
  title,
  description,
  action,
}: {
  eyebrow: string
  title: string
  description: string
  action?: ReactNode
}) {
  return (
    <div className="section-heading">
      <div>
        <div className="eyebrow">{eyebrow}</div>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {action && <div className="heading-action">{action}</div>}
    </div>
  )
}

function Button({
  children,
  variant = 'default',
  icon: Icon,
  onClick,
  disabled,
}: {
  children: ReactNode
  variant?: 'default' | 'primary' | 'quiet' | 'danger'
  icon?: LucideIcon
  onClick?: () => void
  disabled?: boolean
}) {
  return (
    <button className={`button button-${variant}`} onClick={onClick} disabled={disabled}>
      {Icon && <Icon size={17} aria-hidden="true" />}
      <span>{children}</span>
    </button>
  )
}

function Callout({
  tone = 'info',
  title,
  children,
  action,
}: {
  tone?: 'info' | 'good' | 'warn' | 'danger'
  title: string
  children: ReactNode
  action?: ReactNode
}) {
  const Icon = tone === 'good' ? CheckCircle2 : tone === 'warn' ? CircleAlert : tone === 'danger' ? Shield : Info
  return (
    <div className={`callout callout-${tone}`}>
      <Icon size={19} aria-hidden="true" />
      <div>
        <strong>{title}</strong>
        <div>{children}</div>
      </div>
      {action && <div className="callout-action">{action}</div>}
    </div>
  )
}

function SelectField({
  label,
  value,
  onChange,
  options,
  hint,
}: {
  label: string
  value: string
  onChange?: (value: string) => void
  options: string[]
  hint?: string
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <div className="select-wrap">
        <select value={value} onChange={(event) => onChange?.(event.target.value)}>
          {options.map((option) => (
            <option key={option}>{option}</option>
          ))}
        </select>
        <ChevronDown size={16} aria-hidden="true" />
      </div>
      {hint && <small>{hint}</small>}
    </label>
  )
}

function InstallerScreen({ onNext }: { onNext: () => void }) {
  const [shortcut, setShortcut] = useState(true)
  const [launch, setLaunch] = useState(true)
  return (
    <div className="screen installer-screen">
      <SectionHeading
        eyebrow="WINDOWS INSTALLER"
        title="Install Vault Prospector"
        description="Search Azure Key Vault safely from one local, encrypted index."
      />
      <div className="installer-layout">
        <div className="install-hero">
          <div className="product-mark">
            <Vault size={38} />
          </div>
          <div>
            <Pill tone="accent">Version 0.3 preview concept</Pill>
            <h2>Private source. Public, verified release package.</h2>
            <p>
              The application indexes metadata only. Secret values remain in their source until you explicitly
              request one.
            </p>
          </div>
          <div className="install-proof">
            <div><ShieldCheck size={18} /><span><b>Package integrity checked</b><small>Release manifest and SHA-256 match</small></span></div>
            <div><HardDrive size={18} /><span><b>Local encrypted state</b><small>Stored for this Windows account</small></span></div>
            <div><Cloud size={18} /><span><b>No hosted secret database</b><small>Azure remains authoritative</small></span></div>
          </div>
        </div>
        <div className="install-options panel">
          <h3>Install options</h3>
          <div className="install-location">
            <span>Install for everyone on this computer</span>
            <code>C:\Program Files\Vault Prospector</code>
          </div>
          <label className="check-row">
            <input type="checkbox" checked={shortcut} onChange={(event) => setShortcut(event.target.checked)} />
            <span><b>Add a Start menu shortcut</b><small>Available to every Windows user</small></span>
          </label>
          <label className="check-row">
            <input type="checkbox" checked={launch} onChange={(event) => setLaunch(event.target.checked)} />
            <span><b>Launch after installation</b><small>First run starts with secure local setup</small></span>
          </label>
          <Callout title="Updates preserve your local state">
            Installing the same or a newer compatible version keeps data under your Windows profile.
          </Callout>
          <div className="button-row end">
            <Button variant="quiet">Cancel</Button>
            <Button variant="primary" icon={Download} onClick={onNext}>Install</Button>
          </div>
        </div>
      </div>
    </div>
  )
}

function UnlockScreen({ onNext }: { onNext: () => void }) {
  const [method, setMethod] = useState<'hello' | 'remote'>('hello')
  return (
    <div className="screen unlock-screen">
      <SectionHeading
        eyebrow="LOCAL PROTECTION"
        title="Unlock this installation"
        description="Windows verifies that you are present before Vault Prospector opens its encrypted local data."
      />
      <div className="unlock-layout">
        <div className="unlock-art">
          <div className="shield-orbit"><Fingerprint size={58} /></div>
          <h2>Your Azure identities are separate</h2>
          <p>Unlocking the app does not sign you into Azure. You can connect several Microsoft Entra identities afterward.</p>
        </div>
        <div className="panel unlock-card">
          <div className="session-line">
            <Laptop size={18} />
            <span><b>Windows session detected</b><small>KRIS-LAPTOP · Local interactive session</small></span>
            <Pill tone="good">Supported</Pill>
          </div>
          <div className="choice-list" role="radiogroup" aria-label="Verification method">
            <button className={method === 'hello' ? 'choice selected' : 'choice'} onClick={() => setMethod('hello')}>
              <Fingerprint size={22} />
              <span><b>Windows Hello</b><small>Face, fingerprint, or PIN configured for this account</small></span>
              {method === 'hello' && <Check size={18} />}
            </button>
            <button className={method === 'remote' ? 'choice selected' : 'choice'} onClick={() => setMethod('remote')}>
              <Network size={22} />
              <span><b>Approved remote-session verification</b><small>Available when enabled by enterprise policy for AVD or RDP</small></span>
              {method === 'remote' && <Check size={18} />}
            </button>
          </div>
          <Callout title="Remote sessions do not bypass verification">
            If this is AVD or Remote Desktop, the app uses only an administrator-approved remote verification method.
          </Callout>
          <Button variant="primary" icon={LockKeyhole} onClick={onNext}>Verify and continue</Button>
          <button className="text-link"><CircleHelp size={15} /> I cannot use either method</button>
        </div>
      </div>
    </div>
  )
}

function SetupStepper() {
  return (
    <ol className="setup-stepper" aria-label="Setup progress">
      {setupSteps.map((step, index) => (
        <li key={step.name} className={step.state}>
          <span className="step-number">{step.state === 'complete' ? <Check size={15} /> : index + 1}</span>
          <span><b>{step.name}</b><small>{step.state === 'complete' ? 'Complete' : step.state === 'current' ? 'Current step' : 'Not started'}</small></span>
        </li>
      ))}
    </ol>
  )
}

function ConnectScreen({ onNext }: { onNext: () => void }) {
  const [connected, setConnected] = useState(2)
  return (
    <div className="screen connect-screen">
      <SectionHeading
        eyebrow="FIRST-RUN SETUP"
        title="Connect the accounts you use"
        description="Add each Microsoft Entra account once. Guest memberships and accessible tenants are discovered after sign-in."
        action={<Pill tone="good">Local protection complete</Pill>}
      />
      <SetupStepper />
      <div className="setup-columns">
        <div className="panel identity-panel">
          <div className="panel-title">
            <div><h3>Connected identities</h3><p>{connected} interactive accounts ready</p></div>
            <Button icon={UserRound} onClick={() => setConnected((value) => value + 1)}>Add another account</Button>
          </div>
          <div className="identity-card selected">
            <div className="avatar">KT</div>
            <div><b>kris@hybridsolutions.cloud</b><small>Hybrid Solutions Cloud · Home account</small></div>
            <Pill tone="good">Ready</Pill>
            <MoreHorizontal size={18} />
          </div>
          <div className="tenant-memberships">
            <span>Accessible tenants</span>
            <label><input type="checkbox" defaultChecked /> Hybrid Solutions Cloud <small>Home</small></label>
            <label><input type="checkbox" defaultChecked /> Contoso <small>Guest</small></label>
            <label><input type="checkbox" /> Fabrikam <small>Guest</small></label>
          </div>
          <div className="identity-card">
            <div className="avatar alternate">KT</div>
            <div><b>kturner@tierpoint.com</b><small>TierPoint · Work account</small></div>
            <Pill tone="good">Ready</Pill>
            <MoreHorizontal size={18} />
          </div>
          {connected > 2 && (
            <div className="identity-card new">
              <div className="avatar new-avatar">+</div>
              <div><b>New Microsoft account</b><small>Authentication completed in the system browser</small></div>
              <Pill tone="good">Ready</Pill>
            </div>
          )}
        </div>
        <div className="panel next-step-card">
          <div className="next-icon"><CloudDownload size={25} /></div>
          <h3>Next: discover metadata</h3>
          <p>Synchronization reads subscriptions, vaults, and object metadata. It does not retrieve secret values.</p>
          <div className="safe-list">
            <span><Check size={15} /> Names and tags</span>
            <span><Check size={15} /> Vault and subscription context</span>
            <span><X size={15} /> No secret values</span>
          </div>
          <Button variant="primary" icon={ArrowRight} onClick={onNext}>Review discovery scope</Button>
          <button className="text-link">Finish setup later</button>
        </div>
      </div>
    </div>
  )
}

function SyncScreen({ onNext }: { onNext: () => void }) {
  const [expanded, setExpanded] = useState(true)
  return (
    <div className="screen sync-screen">
      <SectionHeading
        eyebrow="DISCOVERY AND HEALTH"
        title="Your metadata is ready"
        description="Two identities were synchronized. Successful results are available even when one scope needs attention."
        action={<Button icon={RefreshCw}>Sync all identities</Button>}
      />
      <div className="metrics">
        <div><Vault size={20} /><span><b>2</b><small>Vaults indexed</small></span></div>
        <div><KeyRound size={20} /><span><b>124</b><small>Objects searchable</small></span></div>
        <div><Users size={20} /><span><b>2</b><small>Identities ready</small></span></div>
        <div className="metric-warn"><CircleAlert size={20} /><span><b>3</b><small>Isolated errors</small></span></div>
      </div>
      <div className="sync-layout">
        <div className="panel sync-status">
          <div className="panel-title">
            <div><h3>Connected sources</h3><p>Last synchronized just now</p></div>
            <Pill tone="warn">Completed with errors</Pill>
          </div>
          <div className="source-row">
            <div className="avatar">KT</div>
            <div><b>kris@hybridsolutions.cloud</b><small>2 tenants · 1 subscription · 2 vaults</small></div>
            <Pill tone="good">124 objects</Pill>
            <Button variant="quiet" icon={RefreshCw}>Sync</Button>
          </div>
          <div className="source-row">
            <div className="avatar alternate">KT</div>
            <div><b>kturner@tierpoint.com</b><small>1 tenant · no selected subscriptions</small></div>
            <Pill>Ready</Pill>
            <Button variant="quiet" icon={RefreshCw}>Sync</Button>
          </div>
          <Callout tone="good" title="Partial results were preserved">
            The 124 successful objects are available in Search. Failed scopes were not marked successful.
          </Callout>
          <div className="button-row end">
            <Button variant="primary" icon={Search} onClick={onNext}>Search indexed objects</Button>
          </div>
        </div>
        <div className="panel error-panel">
          <button className="panel-title disclosure" onClick={() => setExpanded(!expanded)}>
            <div><h3>3 isolated errors</h3><p>Safe details with recovery actions</p></div>
            <ChevronDown className={expanded ? 'rotated' : ''} size={20} />
          </button>
          {expanded && (
            <div className="error-list">
              <div className="error-row">
                <span className="error-dot" />
                <div><b>Access denied to secret metadata</b><small>Contoso · prod-eus-legacy · 4:42 PM</small><p>Ask a vault owner for metadata-list access, then retry this vault.</p></div>
                <Button variant="quiet">Retry</Button>
              </div>
              <div className="error-row">
                <span className="error-dot" />
                <div><b>Vault endpoint unavailable</b><small>Contoso · archived-apps · 4:42 PM</small><p>Network or private-endpoint access may be required.</p></div>
                <Button variant="quiet">Details</Button>
              </div>
              <div className="error-row">
                <span className="error-dot" />
                <div><b>Tenant interaction required</b><small>Fabrikam · guest tenant · 4:41 PM</small><p>Reauthenticate this identity to satisfy tenant policy.</p></div>
                <Button variant="quiet">Sign in</Button>
              </div>
              <Button icon={FileArchive}>Export redacted support bundle</Button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function SearchScreen({ onReveal }: { onReveal: () => void }) {
  const [query, setQuery] = useState('')
  const [tenant, setTenant] = useState('All tenants')
  const [subscription, setSubscription] = useState('All subscriptions')
  const [vault, setVault] = useState('All vaults')
  const [selected, setSelected] = useState(searchRows[0])
  const visibleRows = useMemo(() => searchRows.filter((row) => {
    const matchesQuery = `${row.name} ${row.kind} ${row.vault} ${row.subscription} ${row.tenant}`.toLowerCase().includes(query.toLowerCase())
    const matchesTenant = tenant === 'All tenants' || row.tenant === tenant.replace(/ · .*/, '')
    const matchesSubscription = subscription === 'All subscriptions' || row.subscription === subscription.replace(/ · .*/, '')
    const matchesVault = vault === 'All vaults' || row.vault === vault
    return matchesQuery && matchesTenant && matchesSubscription && matchesVault
  }), [query, tenant, subscription, vault])
  return (
    <div className="screen search-screen">
      <SectionHeading
        eyebrow="ENCRYPTED LOCAL INDEX"
        title="Find a vault object"
        description="Search synchronized metadata across your identities and workspaces. Values are retrieved only when requested."
        action={<div className="sync-fresh"><Wifi size={16} /><span><b>Index current</b><small>124 objects · just now</small></span></div>}
      />
      <div className="search-bar">
        <Search size={21} />
        <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search names, tags, vaults, or subscriptions" />
        <kbd>Ctrl K</kbd>
      </div>
      <div className="filter-panel">
        <div className="filter-heading"><ListFilter size={17} /><b>Scope</b><span>Populated from your latest discovery</span></div>
        <SelectField label="Tenant" value={tenant} onChange={setTenant} options={['All tenants', 'Contoso · 71c…97a', 'Fabrikam · 362…a18', 'Hybrid Solutions Cloud · d6f…e83']} />
        <SelectField label="Subscription" value={subscription} onChange={setSubscription} options={['All subscriptions', 'Contoso Production · 7b2…f19', 'Fabrikam Corporate · 9e0…4aa', 'HCS Lab · 10c…771']} />
        <SelectField label="Vault" value={vault} onChange={setVault} options={['All vaults', 'prod-eus-api', 'prod-eus-payments', 'corp-shared', 'lab-demo']} />
        <Button variant="quiet" icon={RotateCcw} onClick={() => { setTenant('All tenants'); setSubscription('All subscriptions'); setVault('All vaults') }}>Reset</Button>
      </div>
      <div className="search-layout">
        <div className="results-panel panel">
          <div className="results-toolbar">
            <span><b>{visibleRows.length} results</b><small>Workspace: All accessible sources</small></span>
            <div><button className="chip active">Secrets</button><button className="chip">Favorites</button><button className="chip">Expiring</button></div>
          </div>
          <div className="result-table">
            <div className="result-header"><span>Name</span><span>Vault</span><span>Source</span><span>Status</span></div>
            {visibleRows.map((row) => (
              <button key={row.name} className={selected.name === row.name ? 'result-row selected' : 'result-row'} onClick={() => setSelected(row)}>
                <span><KeyRound size={17} /><span><b>{row.name}</b><small>{row.kind} · {row.updated}</small></span></span>
                <span>{row.vault}</span>
                <span>{row.tenant}<small>{row.subscription}</small></span>
                <span><Pill tone={row.state === 'Stale' ? 'warn' : row.state.startsWith('Expires') ? 'warn' : 'good'}>{row.state}</Pill></span>
              </button>
            ))}
            {visibleRows.length === 0 && <div className="empty-state"><Filter size={28} /><b>No results in this scope</b><span>Change a populated filter or synchronize another source.</span></div>}
          </div>
        </div>
        <aside className="detail-panel panel">
          <div className="object-icon"><KeyRound size={22} /></div>
          <Pill>{selected.kind}</Pill>
          <h2>{selected.name}</h2>
          <dl>
            <div><dt>Workspace</dt><dd>{selected.workspace}</dd></div>
            <div><dt>Tenant</dt><dd>{selected.tenant}</dd></div>
            <div><dt>Subscription</dt><dd>{selected.subscription}</dd></div>
            <div><dt>Vault</dt><dd>{selected.vault}</dd></div>
          </dl>
          <Callout title="Value not retrieved">Only metadata is currently open.</Callout>
          <Button variant="primary" icon={Eye} onClick={onReveal}>Reveal safely</Button>
          <Button icon={Copy}>Copy securely</Button>
        </aside>
      </div>
    </div>
  )
}

function RevealScreen() {
  const [revealed, setRevealed] = useState(false)
  const [grace, setGrace] = useState(true)
  return (
    <div className="screen reveal-screen">
      <div className="back-line"><ArrowLeft size={16} /> Back to 4 search results</div>
      <SectionHeading
        eyebrow="EXPLICIT SECRET ACCESS"
        title="sql-admin-password"
        description="Contoso · Contoso Production · prod-eus-api"
        action={<Pill tone="good">Read only</Pill>}
      />
      <div className="reveal-layout">
        <div className="panel reveal-card">
          <div className="secret-context">
            <div className="object-icon"><KeyRound size={22} /></div>
            <div><b>Current version</b><small>Updated two hours ago · No expiration</small></div>
            <Button variant="quiet" icon={MoreHorizontal}>Actions</Button>
          </div>
          <div className={revealed ? 'secret-value revealed' : 'secret-value'}>
            <code>{revealed ? 'correct-horse-example' : '•••••••••••••••••••••'}</code>
            <span>{revealed ? 'Hides automatically in 10 seconds' : 'Value has not been retrieved'}</span>
            {revealed && <button aria-label="Hide now" onClick={() => setRevealed(false)}><EyeOff size={18} /></button>}
          </div>
          <div className="button-row">
            <Button variant="primary" icon={revealed ? EyeOff : Eye} onClick={() => setRevealed(!revealed)}>{revealed ? 'Hide now' : grace ? 'Reveal' : 'Verify and reveal'}</Button>
            <Button icon={Copy}>{grace ? 'Copy securely' : 'Verify and copy'}</Button>
            <Button icon={Download}>Cache offline…</Button>
          </div>
          <Callout tone="warn" title="Offline cache is separate">
            Cache offline explicitly encrypts this selected value for a limited time. The reveal grace period never prefetches or stores plaintext.
          </Callout>
        </div>
        <aside className="panel presence-card">
          <div className="presence-ring"><ShieldCheck size={31} /></div>
          <h3>Verified presence active</h3>
          <p>You verified with Windows Hello 13 seconds ago.</p>
          <div className="timer"><span style={{ width: grace ? '74%' : '0%' }} /><b>{grace ? '00:47' : 'Expired'}</b></div>
          <small>During this window, each Reveal remains explicit but does not prompt again.</small>
          <button className="text-link" onClick={() => setGrace(!grace)}>{grace ? 'End verification window now' : 'Simulate fresh verification'}</button>
          <div className="boundary-list">
            <b>Ends immediately when you</b>
            <span><MonitorDown size={14} /> Minimize or use the notification area</span>
            <span><LockKeyhole size={14} /> Lock Windows or the application</span>
            <span><Users size={14} /> Change identity or workspace</span>
          </div>
        </aside>
      </div>
    </div>
  )
}

function WorkspacesScreen() {
  const [selected, setSelected] = useState('Customer · Contoso')
  const workspaces = [
    { name: 'Customer · Contoso', detail: '1 identity · 1 tenant · 1 subscription · 2 vaults', color: 'teal' },
    { name: 'Corporate', detail: '1 identity · 1 tenant · 1 subscription · 1 vault', color: 'blue' },
    { name: 'Lab and demos', detail: '2 identities · 1 tenant · 1 subscription · 1 vault', color: 'amber' },
  ]
  return (
    <div className="screen workspaces-screen">
      <SectionHeading
        eyebrow="LOCAL ORGANIZATION"
        title="Workspaces"
        description="Group the identities, tenants, subscriptions, and vaults you use together. Workspaces never change Azure or grant access."
        action={<Button variant="primary" icon={FolderKanban}>New workspace</Button>}
      />
      <Callout title="Think of a workspace as a local view">
        Use one for a customer, employer, project, lab, or personal environment. A resource can appear in more than one workspace.
      </Callout>
      <div className="workspace-layout">
        <div className="workspace-list">
          {workspaces.map((workspace) => (
            <button key={workspace.name} className={selected === workspace.name ? 'workspace-card selected' : 'workspace-card'} onClick={() => setSelected(workspace.name)}>
              <span className={`workspace-swatch ${workspace.color}`} />
              <span><b>{workspace.name}</b><small>{workspace.detail}</small></span>
              <ArrowRight size={17} />
            </button>
          ))}
        </div>
        <div className="panel workspace-detail">
          <div className="panel-title"><div><Pill>Selected workspace</Pill><h2>{selected}</h2></div><Button icon={Settings}>Edit</Button></div>
          <div className="resource-groups">
            <div><span><Users size={17} /><b>Identity</b></span><p>kris@hybridsolutions.cloud</p></div>
            <div><span><Cloud size={17} /><b>Tenant</b></span><p>Contoso</p></div>
            <div><span><Boxes size={17} /><b>Subscription</b></span><p>Contoso Production</p></div>
            <div><span><Vault size={17} /><b>Vaults</b></span><p>prod-eus-api, prod-eus-payments</p></div>
          </div>
          <h3>Workspace safeguards</h3>
          <label className="check-row"><input type="checkbox" /><span><b>Encrypted offline cache</b><small>Disabled for this workspace</small></span></label>
          <label className="check-row"><input type="checkbox" defaultChecked /><span><b>Clipboard allowed</b><small>Clears after 30 seconds when unchanged</small></span></label>
        </div>
      </div>
    </div>
  )
}

function BrowserScreen() {
  const [step, setStep] = useState(1)
  return (
    <div className="screen browser-screen">
      <SectionHeading
        eyebrow="GUIDED ONE-TIME FILL"
        title="Fill the field you selected"
        description="The browser supplied the destination context. You do not need to type an origin or construct a mapping."
        action={<Pill tone="good">Chrome connected</Pill>}
      />
      <div className="browser-flow">
        <div className="browser-preview panel">
          <div className="browser-chrome">
            <div className="browser-dots"><span /><span /><span /></div>
            <div className="address"><LockKeyhole size={13} /> login.contoso.com</div>
          </div>
          <div className="mock-page">
            <div className="mock-logo">CONTOSO</div>
            <h3>Sign in</h3>
            <label>Email<input defaultValue="admin@contoso.com" readOnly /></label>
            <label>Password<div className="focus-field">•••••••••••• <Pill tone="accent">Selected field</Pill></div></label>
            <button>Sign in</button>
          </div>
        </div>
        <div className="panel browser-assistant">
          <ol className="compact-stepper">
            <li className={step >= 1 ? 'active' : ''}><span>{step > 1 ? <Check size={13} /> : 1}</span>Destination</li>
            <li className={step >= 2 ? 'active' : ''}><span>{step > 2 ? <Check size={13} /> : 2}</span>Secret</li>
            <li className={step >= 3 ? 'active' : ''}><span>3</span>Confirm</li>
          </ol>
          {step === 1 && (
            <div className="flow-step">
              <Pill tone="good">Captured automatically</Pill>
              <h3>Review the destination</h3>
              <dl>
                <div><dt>Top page</dt><dd>https://login.contoso.com</dd></div>
                <div><dt>Target frame</dt><dd>Same as top page</dd></div>
                <div><dt>Field</dt><dd>Password</dd></div>
                <div><dt>Policy</dt><dd>Allowed by Contoso browser policy</dd></div>
              </dl>
              <Button variant="primary" icon={ArrowRight} onClick={() => setStep(2)}>Choose a secret</Button>
            </div>
          )}
          {step === 2 && (
            <div className="flow-step">
              <h3>Choose the source</h3>
              <div className="mini-search"><Search size={16} /><input defaultValue="sql-admin" /></div>
              <button className="secret-choice selected"><KeyRound size={18} /><span><b>sql-admin-password</b><small>Contoso · prod-eus-api</small></span><Check size={16} /></button>
              <button className="secret-choice"><KeyRound size={18} /><span><b>sql-readonly-password</b><small>Contoso · prod-eus-api</small></span></button>
              <Button variant="primary" icon={ArrowRight} onClick={() => setStep(3)}>Review one-time fill</Button>
            </div>
          )}
          {step === 3 && (
            <div className="flow-step">
              <Pill tone="warn">Fresh verification required</Pill>
              <h3>Fill once into this exact field?</h3>
              <div className="fill-summary"><span><b>sql-admin-password</b><small>prod-eus-api</small></span><ArrowRight size={18} /><span><b>Password field</b><small>login.contoso.com</small></span></div>
              <Callout title="Nothing is stored in the browser">
                The value is retrieved only after verification and sent to this unchanged focused field.
              </Callout>
              <Button variant="primary" icon={Fingerprint}>Verify and fill once</Button>
              <button className="text-link" onClick={() => setStep(1)}>Cancel and start over</button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function AdminScreen() {
  const [customerOnly, setCustomerOnly] = useState(true)
  const candidates = customerOnly
    ? [
      ['contoso-order-api', 'Customer application', 'Assess access'],
      ['contoso-reporting-mi', 'User-assigned managed identity', 'Assess access'],
      ['fabrikam-data-export', 'Customer application', 'Assess access'],
    ]
    : [
      ['contoso-order-api', 'Customer application', 'Assess access'],
      ['Microsoft Azure CLI', 'Microsoft first-party', 'Excluded'],
      ['Windows Azure Service Management API', 'Microsoft infrastructure', 'Excluded'],
      ['contoso-reporting-mi', 'User-assigned managed identity', 'Assess access'],
    ]
  return (
    <div className="screen admin-screen">
      <SectionHeading
        eyebrow="ADVANCED ADMINISTRATION"
        title="Workload identities"
        description="Discover and assess non-human Azure identities used by applications and Azure resources."
        action={<Pill tone="warn">Not required for normal use</Pill>}
      />
      <Callout title="Interactive users and workload identities are different">
        Your connected accounts are human identities. Managed identities and service principals are non-human identities used by software. This page never grants access automatically.
      </Callout>
      <div className="admin-layout">
        <div className="panel admin-controls">
          <h3>Discovery scope</h3>
          <SelectField label="Administrator identity" value="kris@hybridsolutions.cloud" options={['kris@hybridsolutions.cloud']} />
          <SelectField label="Tenant" value="Contoso · 71c…97a" options={['Contoso · 71c…97a', 'Hybrid Solutions Cloud · d6f…e83']} />
          <label className="switch-row">
            <span><b>Customer-manageable candidates only</b><small>Exclude Microsoft first-party and infrastructure principals</small></span>
            <input type="checkbox" checked={customerOnly} onChange={(event) => setCustomerOnly(event.target.checked)} />
          </label>
          <Button variant="primary" icon={CloudDownload}>Refresh candidates</Button>
          <small className="quiet-copy">Uses delegated directory-read permission only. No Graph write permission is requested.</small>
        </div>
        <div className="panel candidates">
          <div className="panel-title">
            <div><h3>Relevant candidates</h3><p>{customerOnly ? '3 customer-manageable identities' : '4 visible principals · 2 excluded'}</p></div>
            <div className="mini-search"><Search size={15} /><input placeholder="Search candidates" /></div>
          </div>
          <div className="candidate-header"><span>Name</span><span>Classification</span><span>Action</span></div>
          {candidates.map(([name, classification, action]) => (
            <div className={action === 'Excluded' ? 'candidate-row excluded' : 'candidate-row'} key={name}>
              <span><BriefcaseBusiness size={17} /><b>{name}</b></span>
              <span>{classification}</span>
              <span>{action === 'Excluded' ? <Pill>Excluded by default</Pill> : <Button variant="quiet">{action}</Button>}</span>
            </div>
          ))}
          <Callout tone="good" title="184 Microsoft principals hidden">
            They remain in Microsoft Entra. Vault Prospector only removes them from the default candidate view.
          </Callout>
        </div>
      </div>
    </div>
  )
}

function ActivityScreen() {
  const events = [
    ['4:42:18 PM', 'Sync completed with errors', '2 vaults · 124 objects · 3 isolated errors', 'warn'],
    ['4:39:02 PM', 'Identity connected', 'kris@hybridsolutions.cloud · Contoso', 'good'],
    ['4:31:47 PM', 'Application unlocked', 'Local Windows session · Windows Hello', 'good'],
    ['Yesterday', 'Update check completed', 'Version 0.3 preview is current', 'neutral'],
  ] as const
  return (
    <div className="screen activity-screen">
      <SectionHeading
        eyebrow="ACTIVITY AND SUPPORT"
        title="Understand what happened"
        description="Privacy-safe events, actionable errors, and a support bundle you can inspect before sharing."
        action={<Button icon={FileArchive}>Create support bundle</Button>}
      />
      <div className="activity-layout">
        <div className="panel activity-feed">
          <div className="panel-title"><div><h3>Recent activity</h3><p>No secret values or tokens are recorded</p></div><Button variant="quiet" icon={Filter}>Filter</Button></div>
          {events.map(([time, title, detail, tone]) => (
            <div className="event-row" key={`${time}-${title}`}>
              <span className={`event-marker ${tone}`} />
              <time>{time}</time>
              <span><b>{title}</b><small>{detail}</small></span>
              <Button variant="quiet">Details</Button>
            </div>
          ))}
        </div>
        <aside className="panel support-panel">
          <div className="support-icon"><FileArchive size={24} /></div>
          <h3>Redacted support bundle</h3>
          <p>Review an inventory before saving. Nothing is uploaded automatically.</p>
          <div className="bundle-list">
            <span><Check size={14} /> Application version and platform</span>
            <span><Check size={14} /> Redacted operational events</span>
            <span><Check size={14} /> Configuration and policy state</span>
            <span><X size={14} /> No secret values or tokens</span>
            <span><X size={14} /> No usernames or object names</span>
          </div>
          <Button variant="primary" icon={Download}>Review and save bundle</Button>
          <button className="text-link"><FolderKanban size={15} /> Open external log location</button>
        </aside>
      </div>
    </div>
  )
}

function SettingsScreen() {
  return (
    <div className="screen settings-screen">
      <SectionHeading
        eyebrow="PREFERENCES AND LIFECYCLE"
        title="Settings"
        description="Your effective settings combine personal choices, workspace safeguards, and enterprise policy."
      />
      <Callout tone="good" title="Version 0.3 preview is ready">
        Includes the redesigned setup, actionable sync errors, and notification-area improvements.
        <Button variant="primary" icon={CloudDownload}>Review update</Button>
      </Callout>
      <div className="settings-layout">
        <nav className="settings-nav">
          <button className="active"><Shield size={17} /> Security</button>
          <button><Bell size={17} /> Background</button>
          <button><CloudDownload size={17} /> Updates</button>
          <button><HardDrive size={17} /> Local data</button>
          <button><BookOpen size={17} /> Help</button>
        </nav>
        <div className="panel settings-content">
          <div className="setting-section">
            <h3>Secret access</h3>
            <SelectField label="Consecutive reveal verification" value="60 seconds" options={['Off · verify every reveal', '30 seconds', '60 seconds', '120 seconds']} hint="Enterprise policy can shorten or disable this window." />
            <label className="switch-row"><span><b>Clipboard allowed</b><small>Clear after 30 seconds if the value is unchanged</small></span><input type="checkbox" defaultChecked /></label>
            <label className="switch-row"><span><b>Encrypted offline cache</b><small>Disabled by default; each value is cached explicitly</small></span><input type="checkbox" /></label>
          </div>
          <div className="setting-section">
            <h3>Window and notification area</h3>
            <label className="switch-row"><span><b>Hide in notification area when minimized</b><small>Lock sensitive presentation and remove the taskbar entry</small></span><input type="checkbox" defaultChecked /></label>
            <SelectField label="When the close button is selected" value="Ask every time" options={['Ask every time', 'Lock and continue in notification area', 'Exit Vault Prospector']} />
            <label className="switch-row"><span><b>Metadata-only background synchronization</b><small>Never retrieves values; follows network and power policy</small></span><input type="checkbox" /></label>
          </div>
        </div>
      </div>
    </div>
  )
}

function ScreenContent({ screen, setScreen }: { screen: Screen; setScreen: (screen: Screen) => void }) {
  switch (screen) {
    case 'install':
      return <InstallerScreen onNext={() => setScreen('unlock')} />
    case 'unlock':
      return <UnlockScreen onNext={() => setScreen('connect')} />
    case 'connect':
      return <ConnectScreen onNext={() => setScreen('sync')} />
    case 'sync':
      return <SyncScreen onNext={() => setScreen('search')} />
    case 'search':
      return <SearchScreen onReveal={() => setScreen('reveal')} />
    case 'reveal':
      return <RevealScreen />
    case 'workspaces':
      return <WorkspacesScreen />
    case 'browser':
      return <BrowserScreen />
    case 'admin':
      return <AdminScreen />
    case 'activity':
      return <ActivityScreen />
    case 'settings':
      return <SettingsScreen />
  }
}

function ContextStrip({ direction }: { direction: Direction }) {
  if (direction === 'command') {
    return (
      <div className="context-strip command-context">
        <span><span className="status-dot good" />2 identities ready</span>
        <span><span className="status-dot good" />2 vaults indexed</span>
        <span><span className="status-dot warn" />3 isolated errors</span>
        <span><ShieldCheck size={14} />Read-only policy</span>
      </div>
    )
  }
  if (direction === 'atlas') {
    return (
      <div className="context-strip atlas-context">
        <span className="context-label">ACTIVE WORKSPACE</span>
        <button><span className="workspace-swatch teal" />Customer · Contoso <ChevronDown size={14} /></button>
        <span>kris@hybridsolutions.cloud</span>
        <span>Contoso Production</span>
        <Pill tone="good">Ready</Pill>
      </div>
    )
  }
  return (
    <div className="context-strip compass-context">
      <span><ShieldCheck size={15} />Unlocked securely</span>
      <span>2 identities</span>
      <span>124 searchable objects</span>
      <Pill tone="warn">3 items need attention</Pill>
    </div>
  )
}

function ProductShell({
  direction,
  screen,
  setScreen,
}: {
  direction: Direction
  screen: Screen
  setScreen: (screen: Screen) => void
}) {
  const current = navigation.find((item) => item.id === screen)!
  const currentIndex = navigation.findIndex((item) => item.id === screen)
  const next = navigation[currentIndex + 1]
  const previous = navigation[currentIndex - 1]

  if (screen === 'install') {
    return (
      <div className={`product-shell direction-${direction} installer-shell`}>
        <ScreenContent screen={screen} setScreen={setScreen} />
      </div>
    )
  }

  return (
    <div className={`product-shell direction-${direction}`}>
      <header className="product-header">
        <div className="brand">
          <span className="brand-mark"><Vault size={20} /></span>
          <span><b>Vault Prospector</b><small>Secure Azure retrieval</small></span>
        </div>
        {direction === 'command' && (
          <button className="global-command"><TerminalSquare size={16} /> Search or run an action <kbd>Ctrl K</kbd></button>
        )}
        <div className="header-actions">
          <button aria-label="Activity"><Activity size={18} /><span className="notification-count">3</span></button>
          <button className="avatar-button">KT</button>
          <button aria-label="More"><MoreHorizontal size={18} /></button>
        </div>
      </header>
      <ContextStrip direction={direction} />
      <div className="shell-body">
        <aside className="product-nav">
          {direction === 'atlas' && <div className="nav-caption">WORKSPACE TOOLS</div>}
          {(['Start', 'Use', 'Manage'] as const).map((phase) => (
            <div className="nav-group" key={phase}>
              <div className="nav-phase">{phase}</div>
              {navigation.filter((item) => item.phase === phase && item.id !== 'install').map((item) => {
                const Icon = item.icon
                return (
                  <button key={item.id} className={screen === item.id ? 'active' : ''} onClick={() => setScreen(item.id)}>
                    <Icon size={17} />
                    <span>{direction === 'command' ? item.shortName : item.name}</span>
                    {item.id === 'activity' && <span className="nav-badge">3</span>}
                  </button>
                )
              })}
            </div>
          ))}
          <div className="nav-footer">
            <span className="status-dot good" />
            <span><b>Running securely</b><small>Minimize to notification area</small></span>
          </div>
        </aside>
        <main className="product-content">
          <ScreenContent screen={screen} setScreen={setScreen} />
          <div className="prototype-pagination">
            <Button variant="quiet" icon={ArrowLeft} onClick={() => previous && setScreen(previous.id)} disabled={!previous}>
              {previous?.shortName ?? 'Previous'}
            </Button>
            <span>{current.name} · {currentIndex + 1} of {navigation.length}</span>
            <Button variant="quiet" icon={ArrowRight} onClick={() => next && setScreen(next.id)} disabled={!next}>
              {next?.shortName ?? 'Complete'}
            </Button>
          </div>
        </main>
      </div>
    </div>
  )
}

export default function App() {
  const [direction, setDirection] = useState<Direction>('compass')
  const [screen, setScreen] = useState<Screen>('install')
  const selectedDirection = directions[direction]
  return (
    <div className={`prototype prototype-${direction}`}>
      <header className="study-header">
        <div className="study-title">
          <Sparkles size={18} />
          <span><b>Vault Prospector complete UI redesign</b><small>Interactive synthetic-data study · 25 July 2026</small></span>
        </div>
        <div className="direction-picker" role="group" aria-label="Design direction">
          {(Object.entries(directions) as [Direction, typeof directions[Direction]][]).map(([id, item]) => (
            <button key={id} className={direction === id ? 'active' : ''} onClick={() => setDirection(id)}>
              <span>{item.label}</span>
              <b>{item.name}</b>
              <small>{item.bestFor}</small>
            </button>
          ))}
        </div>
        <div className="study-actions">
          <button title="Prototype help"><CircleHelp size={18} /></button>
          <button title="Close study"><X size={18} /></button>
        </div>
      </header>
      <div className="direction-summary">
        <div><Pill tone="accent">Direction {selectedDirection.label}</Pill><b>{selectedDirection.name}</b><span>{selectedDirection.description}</span></div>
        <div className="lifecycle-jump" aria-label="Lifecycle screens">
          {navigation.map((item) => {
            const Icon = item.icon
            return <button key={item.id} title={item.name} className={screen === item.id ? 'active' : ''} onClick={() => setScreen(item.id)}><Icon size={15} /><span>{item.shortName}</span></button>
          })}
        </div>
      </div>
      <ProductShell direction={direction} screen={screen} setScreen={setScreen} />
      <footer className="study-footer">
        <span><Info size={14} />Concept only. All names, identifiers, values, and organizations shown are synthetic.</span>
        <span><AppWindow size={14} />Compare the same 11 lifecycle screens across all three directions.</span>
      </footer>
    </div>
  )
}
