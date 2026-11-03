export type Tool = { name: string; description: string; sampleArgs: Record<string, unknown> }

export type GatewayState = { blockedAll: boolean; blockedTools: string[]; agentConfigured: boolean; apim: string }

export type Outcome = 'ok' | 'bloqueado' | 'error'

export type LogEntry = { at: string; kind: string; detail: string; status: number | null; ms: number; outcome: Outcome }

export type ToolResult = { mode: string; status: number; ms: number; outcome: Outcome; body: string }

export type StepState = 'ok' | 'blocked' | 'error' | 'pending'

export type TraceStep = { label: string; detail?: string; state: StepState }

export type Trace = { id: number; title: string; at: string; ms?: number; steps: TraceStep[] }

export type ToolChip = { name: string; state: StepState }

export type Message = { role: 'user' | 'assistant'; text: string; tools: ToolChip[] }

export type Mode = 'gateway' | 'direct'
