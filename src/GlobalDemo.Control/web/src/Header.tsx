import type { Guardrail, Mode } from './types'

type Props = {
  online: boolean
  guardrail: Guardrail | null
  mode: Mode
  onMode: (m: Mode) => void
  session: string | null
  onNewConversation: () => void
}

export default function Header({ online, guardrail, mode, onMode, session, onNewConversation }: Props) {
  return (
    <header className="topbar">
      <div>
        <h1>¿Cuándo aterriza? · asistente-vuelos</h1>
        <p className="subtitle">
          Foundry Hosted Agent · .NET 10 · Agent Framework | tools por MCP | gobernado desde Foundry (AI Gateway)
        </p>
      </div>
      <div className="topbar-controls">
        {guardrail && (
          <>
            <span
              className={`pill guard ${guardrail.found && guardrail.jailbreakBlocking ? 'on' : 'off'}`}
              title="Lee la RAI policy de Foundry: el jailbreak se bloquea antes de que el agente se ejecute (400 content_filter)."
            >
              {guardrail.found && guardrail.jailbreakBlocking ? '🛡 Guardrail activo' : '⚠ Sin guardrail'}
            </span>
            {guardrail.found && guardrail.egressMode && (
              <span
                className={`pill guard ${guardrail.egressMode === 'Enforced' ? 'on' : 'warn'}`}
                title={`Egress ${guardrail.egressMode}: el agente solo puede salir a ${guardrail.allowedHosts.join(', ') || 'nada'} (más lo que la plataforma necesita).`}
              >
                Egress · {guardrail.egressMode === 'Enforced' ? 'solo el AI Gateway' : 'en auditoría'}
              </span>
            )}
          </>
        )}
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
