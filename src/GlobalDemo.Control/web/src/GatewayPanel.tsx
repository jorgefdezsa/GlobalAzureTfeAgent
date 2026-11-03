import { useEffect, useState } from 'react'
import AuditList from './AuditList'
import type { GatewayState, LogEntry, Mode, Tool, ToolResult } from './types'

type Props = {
  tools: Tool[]
  gw: GatewayState | null
  error: string | null
  requests: LogEntry[]
  mode: Mode
  testing: boolean
  lastResult: ToolResult | null
  onToggleTool: (name: string) => void
  onToggleAll: () => void
  onRateLimit: (callsPerMinute: number) => void
  onTest: (tool: string, args: unknown) => void
}

export default function GatewayPanel(p: Props) {
  const [tab, setTab] = useState<'requests' | 'audit'>('requests')
  const [tool, setTool] = useState('')
  const [args, setArgs] = useState('')
  const [argsError, setArgsError] = useState(false)

  useEffect(() => {
    if (!tool && p.tools.length) {
      setTool(p.tools[0].name)
      setArgs(JSON.stringify(p.tools[0].sampleArgs))
    }
  }, [p.tools, tool])

  const pick = (name: string) => {
    setTool(name)
    setArgs(JSON.stringify(p.tools.find((t) => t.name === name)?.sampleArgs ?? {}))
    setArgsError(false)
  }

  const run = () => {
    try {
      p.onTest(tool, JSON.parse(args || '{}'))
      setArgsError(false)
    } catch {
      setArgsError(true)
    }
  }

  const [draft, setDraft] = useState(5)
  useEffect(() => {
    if (p.gw && p.gw.rateLimit > 0) setDraft(p.gw.rateLimit)
  }, [p.gw?.rateLimit])
  const limitOn = !!p.gw && p.gw.rateLimit > 0
  const step = (delta: number) => {
    const next = Math.min(600, Math.max(1, draft + delta))
    setDraft(next)
    if (limitOn) p.onRateLimit(next)
  }

  const blocked = (name: string) => !!p.gw && (p.gw.blockedAll || p.gw.blockedTools.includes(name))

  return (
    <section className="panel gateway">
      <h2>Control en el AI Gateway</h2>
      {p.error && <div className="banner error">{p.error}</div>}

      <label className={`toggle all ${p.gw?.blockedAll ? 'blocked' : ''}`}>
        <span>
          <code>mcp-blocked</code> · todo el servidor MCP
        </span>
        <input type="checkbox" checked={!!p.gw?.blockedAll} disabled={!p.gw} onChange={p.onToggleAll} />
        <i className="switch" />
      </label>

      <div className="toggles">
        {p.tools.map((t) => (
          <label key={t.name} title={t.description} className={`toggle ${blocked(t.name) ? 'blocked' : ''}`}>
            <code>{t.name}</code>
            <input
              type="checkbox"
              checked={!!p.gw?.blockedTools.includes(t.name) || !!p.gw?.blockedAll}
              disabled={!p.gw || p.gw.blockedAll}
              onChange={() => p.onToggleTool(t.name)}
            />
            <i className="switch" />
          </label>
        ))}
      </div>
      <div className={`ratebox ${limitOn ? 'limited' : ''}`}>
        <label className="toggle bare">
          <span>
            <code>mcp-rate-limit</code> · límite de llamadas
          </span>
          <input type="checkbox" checked={limitOn} disabled={!p.gw} onChange={() => p.onRateLimit(limitOn ? 0 : draft)} />
          <i className="switch amber" />
        </label>
        <div className="stepper">
          <button className="pill ghost" disabled={!p.gw || draft <= 1} onClick={() => step(-1)}>−</button>
          <span>
            <b>{draft}</b> llamadas / minuto
          </span>
          <button className="pill ghost" disabled={!p.gw || draft >= 600} onClick={() => step(1)}>+</button>
        </div>
      </div>
      <p className="hint">
        Bloquear una tool cambia una named value en API Management (igual que el límite de llamadas){p.gw ? ` (${p.gw.apim})` : ''}: ni el agente ni el MCP se tocan.
      </p>

      <div className="tester">
        <select value={tool} onChange={(e) => pick(e.target.value)}>
          {p.tools.map((t) => (
            <option key={t.name} value={t.name}>
              {t.name}
            </option>
          ))}
        </select>
        <input className={argsError ? 'invalid' : ''} value={args} onChange={(e) => setArgs(e.target.value)} spellCheck={false} />
        <button className="pill ghost" disabled={p.testing || !tool} onClick={run}>
          {p.testing ? 'Probando…' : p.mode === 'direct' ? 'Probar directo' : 'Probar sin agente'}
        </button>
      </div>
      {p.lastResult && (
        <pre className={`result ${p.lastResult.outcome}`}>
          {p.lastResult.mode === 'direct' ? 'Directo' : 'Gateway'} · HTTP {p.lastResult.status || '—'} · {p.lastResult.ms} ms
          {'\n'}
          {p.lastResult.body}
        </pre>
      )}

      <div className="tabs">
        <button className={tab === 'requests' ? 'on' : ''} onClick={() => setTab('requests')}>Peticiones</button>
        <button className={tab === 'audit' ? 'on' : ''} onClick={() => setTab('audit')}>Auditoría</button>
      </div>
      {tab === 'audit' ? (
        <AuditList />
      ) : (
        <ul className="requests">
        {p.requests.length === 0 && <li className="muted">Aún no hay peticiones.</li>}
        {p.requests.map((r, i) => (
          <li key={i} className={r.outcome}>
            <span className="time">{new Date(r.at).toLocaleTimeString()}</span>
            <span className="kind">{r.kind}</span>
            <span className="detail">{r.detail}</span>
            <span className="code">{r.status ?? '—'}</span>
            <span className="ms">{r.ms} ms</span>
          </li>
        ))}
      </ul>
      )}
    </section>
  )
}
