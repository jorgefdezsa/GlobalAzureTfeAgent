import type { GatewayState, LogEntry, Mode, Tool, ToolResult } from './types'

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let detail = ''
    try {
      const body = await res.json()
      detail = body.detail ?? body.title ?? ''
    } catch {
      detail = await res.text().catch(() => '')
    }
    throw new Error(detail || `HTTP ${res.status}`)
  }
  return res.json() as Promise<T>
}

const post = (url: string, body: unknown) =>
  fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })

export const getTools = () => fetch('/api/tools').then((r) => json<Tool[]>(r))
export const getState = () => fetch('/api/state').then((r) => json<GatewayState>(r))
export const getRequests = () => fetch('/api/requests').then((r) => json<LogEntry[]>(r))
export const setBlockedAll = (blocked: boolean) => post('/api/blocks/all', { blocked }).then((r) => json<GatewayState>(r))
export const setBlockedTools = (tools: string[]) => post('/api/blocks/tools', { tools }).then((r) => json<GatewayState>(r))
export const testTool = (tool: string, args: unknown, mode: Mode) =>
  post('/api/tool-test', { tool, arguments: args, mode }).then((r) => json<ToolResult>(r))
export const chat = (message: string, previousResponseId: string | null, signal?: AbortSignal) =>
  fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message, previousResponseId }),
    signal,
  })

/** Lee un stream SSE y emite cada evento `data:` ya parseado. */
export async function* readSse(res: Response): AsyncGenerator<Record<string, any>> {
  const reader = res.body!.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    let end: number
    while ((end = buffer.indexOf('\n\n')) >= 0) {
      const block = buffer.slice(0, end)
      buffer = buffer.slice(end + 2)
      for (const line of block.split('\n')) {
        if (!line.startsWith('data: ') || line === 'data: [DONE]') continue
        try {
          yield JSON.parse(line.slice(6))
        } catch {
          /* línea parcial o no JSON */
        }
      }
    }
  }
}
