export type Tool = { name: string; description: string; sampleArgs: Record<string, unknown> }

export type GatewayState = { blockedAll: boolean; blockedTools: string[]; rateLimit: number; agentConfigured: boolean; apim: string; priceInput?: number; priceCached?: number; priceOutput?: number }

export type Guardrail = { found: boolean; jailbreakBlocking: boolean; egressMode: string; allowedHosts: string[] }

export type AuditEntry = { at: string; client: string; tool: string; args: string; outcome: string; status: number | null }

export type AuditResult = { configured: boolean; entries: AuditEntry[] }

export type Outcome = 'ok' | 'bloqueado' | 'limitado' | 'error'

export type LogEntry = { at: string; kind: string; detail: string; status: number | null; ms: number; outcome: Outcome }

export type ToolResult = { mode: string; status: number; ms: number; outcome: Outcome; body: string }

export type StepState = 'ok' | 'blocked' | 'limited' | 'egress' | 'error' | 'pending'

export type TraceStep = { label: string; detail?: string; state: StepState }

export type Trace = { id: number; title: string; at: string; ms?: number; steps: TraceStep[] }

export type ToolChip = { name: string; state: StepState }

export type Usage = { input: number; cached: number; output: number }

export type Message = { role: 'user' | 'assistant'; text: string; tools: ToolChip[]; usage?: Usage }

export type Mode = 'gateway' | 'direct'
