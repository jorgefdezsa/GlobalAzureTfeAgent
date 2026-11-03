import { useEffect, useState } from 'react'
import * as api from './api'
import type { AuditResult } from './types'

const label: Record<string, string> = {
  allowed: 'permitida',
  blocked_tool: 'tool bloqueada',
  blocked_server: 'servidor bloqueado',
  rate_limited: 'límite',
  error: 'error',
}
const kind = (o: string) => (o === 'allowed' ? 'ok' : o.startsWith('blocked') ? 'bloqueado' : o === 'rate_limited' ? 'limitado' : 'error')

export default function AuditList() {
  const [data, setData] = useState<AuditResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const load = () =>
      api
        .getAudit(60)
        .then((d) => {
          setData(d)
          setError(null)
        })
        .catch((e) => setError(e instanceof Error ? e.message : String(e)))
    load()
    const timer = setInterval(load, 15000)
    return () => clearInterval(timer)
  }, [])

  if (error) return <div className="banner error">{error}</div>
  if (!data) return <p className="muted">Consultando Application Insights…</p>
  if (!data.configured) return <p className="muted">Falta Control__AppInsights: ejecuta deploy/setup-audit.sh y arranca con deploy/run-control.sh.</p>

  return (
    <>
      <ul className="requests audit">
        {data.entries.length === 0 && <li className="muted">Sin llamadas en la última hora (Application Insights tarda unos minutos).</li>}
        {data.entries.map((e, i) => (
          <li key={i} className={kind(e.outcome)} title={e.args}>
            <span className="time">{new Date(e.at).toLocaleTimeString()}</span>
            <span className="kind">{e.client || '—'}</span>
            <span className="detail">
              {e.tool} <small>{e.args}</small>
            </span>
            <span className="code">{e.status ?? '—'}</span>
            <span className="ms">{label[e.outcome] ?? e.outcome}</span>
          </li>
        ))}
      </ul>
      <p className="hint">Quién llamó, a qué tool y con qué argumentos: trazas del gateway en Application Insights (retraso de unos minutos).</p>
    </>
  )
}
