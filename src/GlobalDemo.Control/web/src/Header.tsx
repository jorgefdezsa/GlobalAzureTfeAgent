import type { Mode } from './types'

type Props = {
  online: boolean
  mode: Mode
  onMode: (m: Mode) => void
  session: string | null
  onNewConversation: () => void
}

export default function Header({ online, mode, onMode, session, onNewConversation }: Props) {
  return (
    <header className="topbar">
      <div>
        <h1>¿Cuándo aterriza? · asistente-vuelos</h1>
        <p className="subtitle">
          Foundry Hosted Agent · .NET 10 · Agent Framework | tools por MCP | gobernado desde Foundry (AI Gateway)
        </p>
      </div>
      <div className="topbar-controls">
        <span className={`pill status ${online ? 'on' : 'off'}`}>
          <i className="dot" /> {online ? 'Agente activo' : 'Sin conexión'}
        </span>
        <div className="segmented" title="Ruta de «Probar sin agente». El agente siempre pasa por el gateway.">
          <button className={mode === 'gateway' ? 'active' : ''} onClick={() => onMode('gateway')}>
            Vía AI Gateway
          </button>
          <button className={mode === 'direct' ? 'active' : ''} onClick={() => onMode('direct')}>
            Directo al backend
          </button>
        </div>
        <span className="session">{session ? `Sesión ${session}` : 'Sesión nueva'}</span>
        <button className="pill ghost" onClick={onNewConversation}>
          Nueva conversación
        </button>
      </div>
    </header>
  )
}
