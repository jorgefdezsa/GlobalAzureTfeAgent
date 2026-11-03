import { useEffect, useRef, useState } from 'react'
import type { Message } from './types'

const SUGGESTIONS = [
  '¿Cuál es el estado del vuelo RYR963?',
  '¿A qué hora llega el TOM1289 y tiene retraso?',
  '¿Qué vuelos hay de MAD a TFS?',
  'Busca el aeropuerto Reina Sofía',
  '¿Cuál es el código IATA de Iberia?',
]

/** Negritas básicas del modelo (**texto**); el resto del texto se muestra tal cual. */
const format = (text: string) =>
  text.split(/\*\*(.+?)\*\*/g).map((part, i) => (i % 2 === 1 ? <strong key={i}>{part}</strong> : part))

type Props = { messages: Message[]; busy: boolean; onSend: (text: string) => void }

export default function ChatPanel({ messages, busy, onSend }: Props) {
  const [draft, setDraft] = useState('')
  const list = useRef<HTMLDivElement>(null)

  // Solo se desplaza la lista de mensajes, no la página.
  useEffect(() => {
    const el = list.current
    if (el) el.scrollTop = el.scrollHeight
  }, [messages])

  const submit = () => {
    if (!draft.trim() || busy) return
    onSend(draft)
    setDraft('')
  }

  return (
    <section className="panel chat">
      <h2>Conversación</h2>
      <div className="messages" ref={list}>
        {messages.map((m, i) => (
          <div key={i} className={`msg ${m.role}`}>
            {m.tools.length > 0 && (
              <div className="tool-chips">
                {m.tools.map((t, j) => (
                  <span key={j} className={`tool-chip ${t.state}`}>
                    {t.state === 'blocked' ? '⛔' : t.state === 'limited' ? '⏱' : t.state === 'error' ? '⚠' : t.state === 'pending' ? '…' : '✓'} {t.name}
                    {t.state === 'blocked' ? ' · bloqueada' : t.state === 'limited' ? ' · límite' : ''}
                  </span>
                ))}
              </div>
            )}
            {m.text ? format(m.text) : (m.role === 'assistant' && busy && i === messages.length - 1 ? <span className="typing">Pensando…</span> : null)}
          </div>
        ))}
      </div>
      <div className="suggestions">
        {SUGGESTIONS.map((s) => (
          <button key={s} className="chip" disabled={busy} onClick={() => onSend(s)}>
            {s}
          </button>
        ))}
      </div>
      <div className="composer">
        <input
          value={draft}
          placeholder="Pregunta por un vuelo..."
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && submit()}
        />
        <button className="primary" disabled={busy || !draft.trim()} onClick={submit}>
          Enviar
        </button>
      </div>
    </section>
  )
}
