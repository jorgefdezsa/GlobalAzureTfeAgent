import type { Trace } from './types'

type Props = { traces: Trace[]; index: number | null; onIndex: (i: number) => void }

export default function TracePanel({ traces, index, onIndex }: Props) {
  const current = index ?? traces.length - 1
  const trace = traces[current]

  return (
    <section className="panel trace">
      <h2>Traza de la última llamada</h2>
      {!trace ? (
        <p className="muted">Envía un mensaje o prueba una tool para ver por dónde pasa la llamada.</p>
      ) : (
        <div className="trace-body">
          <div className="trace-head">
            <strong>{trace.title}</strong>
            <span className="muted">
              {trace.at}
              {trace.ms !== undefined ? ` · ${(trace.ms / 1000).toFixed(1)} s` : ''}
              {traces.length > 1 ? ` · ${current + 1}/${traces.length}` : ''}
            </span>
          </div>
          <ol className="steps">
            {trace.steps.map((s, i) => (
              <li key={i} className={`step ${s.state}`}>
                <i className="step-dot" />
                <div>
                  <div className="step-label">{s.label}</div>
                  {s.detail && <div className="step-detail">{s.detail}</div>}
                </div>
              </li>
            ))}
          </ol>
        </div>
      )}
      <button className="nav prev" aria-label="Llamada anterior" disabled={current <= 0} onClick={() => onIndex(current - 1)}>
        ‹
      </button>
      <button className="nav next" aria-label="Llamada siguiente" disabled={current >= traces.length - 1} onClick={() => onIndex(current + 1)}>
        ›
      </button>
    </section>
  )
}
