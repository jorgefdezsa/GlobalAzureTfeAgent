import { useCallback, useEffect, useRef, useState } from 'react'
import * as api from './api'
import ChatPanel from './ChatPanel'
import GatewayPanel from './GatewayPanel'
import Header from './Header'
import TracePanel from './TracePanel'
import type { GatewayState, Guardrail, LogEntry, Message, Mode, StepState, Tool, ToolResult, Trace, TraceStep } from './types'

const errorText = (e: unknown) => (e instanceof Error ? e.message : String(e))

export default function App() {
  const [tools, setTools] = useState<Tool[]>([])
  const [gw, setGw] = useState<GatewayState | null>(null)
  const [gwError, setGwError] = useState<string | null>(null)
  const [guardrail, setGuardrail] = useState<Guardrail | null>(null)
  const [requests, setRequests] = useState<LogEntry[]>([])
  const [mode, setMode] = useState<Mode>('gateway')
  const [messages, setMessages] = useState<Message[]>([])
  const [previousId, setPreviousId] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [testing, setTesting] = useState(false)
  const [lastResult, setLastResult] = useState<ToolResult | null>(null)
  const [traces, setTraces] = useState<Trace[]>([])
  const [traceIndex, setTraceIndex] = useState<number | null>(null)
  const abort = useRef<AbortController | null>(null)

  const refreshState = useCallback(async () => {
    try {
      setGw(await api.getState())
      setGwError(null)
    } catch (e) {
      setGwError(errorText(e))
    }
  }, [])
  // Estado real de la RAI policy (guardrail + egress). Si no se puede leer, la cabecera no muestra nada en lugar de inventar.
  useEffect(() => {
    const load = () => api.getGuardrail().then(setGuardrail).catch(() => setGuardrail(null))
    load()
    const t = setInterval(load, 30000)
    return () => clearInterval(t)
  }, [])
  const refreshRequests = useCallback(() => api.getRequests().then(setRequests).catch(() => {}), [])

  useEffect(() => {
    api.getTools().then(setTools).catch((e) => setGwError(errorText(e)))
    refreshState()
    refreshRequests()
    // Los interruptores también se mueven desde el terminal (gateway-switch.sh): se refresca el estado cada pocos segundos.
    const timer = setInterval(() => {
      refreshState()
      refreshRequests()
    }, 5000)
    return () => clearInterval(timer)
  }, [refreshState, refreshRequests])

  const addTrace = (t: Trace) => {
    setTraces((ts) => [...ts, t])
    setTraceIndex(null)
  }
  const patchTrace = (id: number, fn: (t: Trace) => Trace) => setTraces((ts) => ts.map((t) => (t.id === id ? fn(t) : t)))
  const patchLastAssistant = (fn: (m: Message) => Message) =>
    setMessages((ms) => ms.map((m, i) => (i === ms.length - 1 ? fn(m) : m)))

  const send = async (text: string) => {
    if (!text.trim() || busy) return
    setBusy(true)
    const id = Date.now()
    const started = performance.now()
    setMessages((ms) => [...ms, { role: 'user', text, tools: [] }, { role: 'assistant', text: '', tools: [] }])
    addTrace({
      id,
      title: text,
      at: new Date().toLocaleTimeString(),
      steps: [{ label: 'Panel → Agente (Foundry)', detail: 'POST /responses · stream', state: 'pending' }],
    })

    const setStep = (index: number, patch: Partial<TraceStep>) =>
      patchTrace(id, (t) => ({ ...t, steps: t.steps.map((s, i) => (i === index ? { ...s, ...patch } : s)) }))
    const addStep = (step: TraceStep) => patchTrace(id, (t) => ({ ...t, steps: [...t.steps, step] }))
    const lastPending = (t: Trace) => t.steps.map((s) => s.state).lastIndexOf('pending')
    abort.current = new AbortController()

    try {
      const res = await api.chat(text, previousId, abort.current.signal)
      if (!res.ok) throw new Error(`El agente respondió HTTP ${res.status}: ${(await res.text()).slice(0, 300)}`)
      setStep(0, { state: 'ok' })

      for await (const ev of api.readSse(res)) {
        if (ev.type === 'response.output_text.delta') {
          patchLastAssistant((m) => ({ ...m, text: m.text + ev.delta }))
        } else if (ev.type === 'response.output_item.done' && ev.item?.type === 'function_call') {
          patchLastAssistant((m) => ({ ...m, tools: [...m.tools, { name: ev.item.name, state: 'pending' }] }))
          addStep({ label: ev.item.name === 'consultar_url' ? 'Agente → Internet (egress del agente)' : 'Agente → AI Gateway', detail: `tools/call ${ev.item.name} ${ev.item.arguments ?? ''}`, state: 'pending' })
        } else if (ev.type === 'response.output_item.done' && ev.item?.type === 'function_call_output') {
          const out = typeof ev.item.output === 'string' ? ev.item.output : JSON.stringify(ev.item.output)
          const blocked = out.includes('"status":"blocked"')
          const limited = out.includes('"status":"rate_limited"')
          const egress = out.includes('"status":"egress_denied"')
          const failed = !blocked && !limited && !egress && /"status":"(gateway_error|tool_error|error|http_error)"/.test(out)
          const state: StepState = blocked ? 'blocked' : limited ? 'limited' : egress ? 'egress' : failed ? 'error' : 'ok'
          patchLastAssistant((m) => {
            const at = m.tools.map((t) => t.state).lastIndexOf('pending')
            return { ...m, tools: m.tools.map((t, i) => (i === at ? { ...t, state } : t)) }
          })
          patchTrace(id, (t) => {
            const at = lastPending(t)
            if (at < 0) return t
            const detail = blocked
              ? '403 AccessBlockedByGateway · la policy corta la llamada y no llega al MCP'
              : limited
                ? '429 · el rate limit del gateway corta la llamada y no llega al MCP'
                : egress
                ? '403 · el proxy de egress de Foundry deniega el destino: el agente solo puede salir por el AI Gateway'
                : failed
                ? out.slice(0, 160)
                : `200 · el MCP responde (${out.length} caracteres)`
            return { ...t, steps: t.steps.map((s, i) => (i === at ? { ...s, state, detail: `${s.detail}\n${detail}` } : s)) }
          })
        } else if (ev.type === 'response.completed') {
          setPreviousId(ev.response.id)
          const u = ev.response.usage
          if (u) patchLastAssistant((m) => ({ ...m, usage: { input: u.input_tokens ?? 0, cached: u.input_tokens_details?.cached_tokens ?? 0, output: u.output_tokens ?? 0 } }))
        } else if (ev.type === 'response.failed') {
          throw new Error(ev.response?.error?.message ?? 'La respuesta falló')
        }
      }
      addStep({ label: 'Agente → Panel', detail: 'respuesta del modelo', state: 'ok' })
    } catch (e) {
      if (!(e instanceof DOMException && e.name === 'AbortError')) {
        patchLastAssistant((m) => ({ ...m, text: m.text || `⚠ ${errorText(e)}` }))
        patchTrace(id, (t) => ({ ...t, steps: t.steps.map((s) => (s.state === 'pending' ? { ...s, state: 'error' } : s)) }))
      }
    } finally {
      patchTrace(id, (t) => ({ ...t, ms: performance.now() - started }))
      setBusy(false)
      refreshRequests()
    }
  }

  const newConversation = () => {
    abort.current?.abort()
    setMessages([])
    setPreviousId(null)
    setBusy(false)
  }

  const toggleTool = async (name: string) => {
    if (!gw) return
    const next = gw.blockedTools.includes(name) ? gw.blockedTools.filter((t) => t !== name) : [...gw.blockedTools, name]
    setGw({ ...gw, blockedTools: next })
    try {
      const state = await api.setBlockedTools(next)
      setGw((g) => (g ? { ...g, ...state } : g))
      setGwError(null)
    } catch (e) {
      setGwError(errorText(e))
      refreshState()
    }
    refreshRequests()
  }

  const toggleAll = async () => {
    if (!gw) return
    setGw({ ...gw, blockedAll: !gw.blockedAll })
    try {
      const state = await api.setBlockedAll(!gw.blockedAll)
      setGw((g) => (g ? { ...g, ...state } : g))
      setGwError(null)
    } catch (e) {
      setGwError(errorText(e))
      refreshState()
    }
    refreshRequests()
  }

  const changeRateLimit = async (calls: number) => {
    if (!gw) return
    setGw({ ...gw, rateLimit: calls })
    try {
      const state = await api.setRateLimit(calls)
      setGw((g) => (g ? { ...g, ...state } : g))
      setGwError(null)
    } catch (e) {
      setGwError(errorText(e))
      refreshState()
    }
    refreshRequests()
  }

  const runTest = async (tool: string, args: unknown) => {
    setTesting(true)
    const id = Date.now()
    const started = performance.now()
    const target = mode === 'direct' ? 'Backend (Container Apps)' : 'AI Gateway'
    addTrace({
      id,
      title: `Probar ${tool}`,
      at: new Date().toLocaleTimeString(),
      steps: [{ label: `Panel → ${target}`, detail: `tools/call ${tool} ${JSON.stringify(args)}`, state: 'pending' }],
    })
    try {
      const result = await api.testTool(tool, args, mode)
      setLastResult(result)
      const steps: TraceStep[] = []
      if (result.status === 403) {
        steps.push({ label: 'AI Gateway · policy de bloqueo', detail: `403 · ${tool} bloqueada, no llega al MCP`, state: 'blocked' })
      } else if (result.status === 429) {
        steps.push({ label: 'AI Gateway · rate limit', detail: `429 · límite de ${gw?.rateLimit ?? '?'} llamadas/min superado, no llega al MCP`, state: 'limited' })
      } else if (result.status === 401) {
        steps.push({
          label: mode === 'direct' ? 'Backend cerrado' : 'AI Gateway rechaza la clave',
          detail: mode === 'direct' ? '401 · solo acepta la clave de backend que inyecta el gateway' : '401 · falta o es incorrecta la clave x-mcp-key',
          state: 'blocked',
        })
      } else if (result.outcome === 'ok') {
        if (mode === 'gateway') steps.push({ label: 'AI Gateway → MCP (Container Apps)', detail: 'clave de backend inyectada', state: 'ok' })
        steps.push({ label: 'MCP → AviationStack', detail: `${result.status} · ${result.ms} ms`, state: 'ok' })
      } else {
        steps.push({ label: 'Error', detail: `HTTP ${result.status || '—'} · ${result.body.slice(0, 160)}`, state: 'error' })
      }
      patchTrace(id, (t) => ({
        ...t,
        ms: performance.now() - started,
        steps: [{ ...t.steps[0], state: result.status === 0 ? 'error' : 'ok' }, ...steps],
      }))
    } catch (e) {
      patchTrace(id, (t) => ({ ...t, steps: [{ ...t.steps[0], state: 'error', detail: errorText(e) }] }))
    } finally {
      setTesting(false)
      refreshRequests()
    }
  }

  return (
    <div className="app">
      <Header
        online={!!gw && !gwError && gw.agentConfigured}
        guardrail={guardrail}
        mode={mode}
        onMode={setMode}
        session={previousId ? previousId.slice(-6) : null}
        onNewConversation={newConversation}
      />
      <main className="grid">
        <ChatPanel messages={messages} busy={busy} onSend={send} priceInput={gw?.priceInput ?? 0} priceCached={gw?.priceCached ?? 0} priceOutput={gw?.priceOutput ?? 0} />
        <div className="right">
          <TracePanel traces={traces} index={traceIndex} onIndex={setTraceIndex} />
          <GatewayPanel
            tools={tools}
            gw={gw}
            error={gwError}
            requests={requests}
            mode={mode}
            testing={testing}
            lastResult={lastResult}
            onToggleTool={toggleTool}
            onToggleAll={toggleAll}
            onRateLimit={changeRateLimit}
            onTest={runTest}
          />
        </div>
      </main>
    </div>
  )
}
