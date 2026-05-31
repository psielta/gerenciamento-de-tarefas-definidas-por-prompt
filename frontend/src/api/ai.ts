import { apiBaseUrl } from '@/env'
import { api } from './client'
import {
  aiChatSessionListSchema,
  aiChatSessionSchema,
  aiSettingsSchema,
  geminiModelListSchema,
  refinedPromptSchema,
  type AiChatSession,
  type AiSettings,
  type GeminiModel,
  type RefinedPrompt,
} from './schemas'

export async function getAiModels(): Promise<GeminiModel[]> {
  const data = await api.get('ai/models').json()
  return geminiModelListSchema.parse(data)
}

export async function getAiSettings(): Promise<AiSettings> {
  const data = await api.get('ai/settings').json()
  return aiSettingsSchema.parse(data)
}

export async function updateAiSettings(settings: {
  model: string
  temperature: number
  thinkingEnabled: boolean
  thinkingBudget: number | null
  thinkingLevel: string | null
}): Promise<AiSettings> {
  const data = await api.put('ai/settings', { json: settings }).json()
  return aiSettingsSchema.parse(data)
}

export async function refinePrompt(params: {
  content: string
  model: string
  temperature: number
  thinkingMode?: string
  thinkingBudget?: number | null
  thinkingLevel?: string | null
  workingDirectoryId?: string
}): Promise<RefinedPrompt> {
  const data = await api.post('ai/refine', { json: params }).json()
  return refinedPromptSchema.parse(data)
}

export async function listChatSessions(params: {
  promptId?: string
  workingDirectoryId?: string
}): Promise<AiChatSession[]> {
  const searchParams = new URLSearchParams()
  if (params.promptId) searchParams.set('promptId', params.promptId)
  if (params.workingDirectoryId) searchParams.set('workingDirectoryId', params.workingDirectoryId)
  const data = await api.get(`ai/sessions?${searchParams}`).json()
  return aiChatSessionListSchema.parse(data)
}

export async function getChatSession(id: string): Promise<AiChatSession> {
  const data = await api.get(`ai/sessions/${id}`).json()
  return aiChatSessionSchema.parse(data)
}

export async function startChatSession(params: {
  title?: string
  workingDirectoryId?: string
  promptId?: string
  model: string
  temperature: number
  thinkingEnabled: boolean
  thinkingBudget?: number | null
  thinkingLevel?: string | null
}): Promise<AiChatSession> {
  const data = await api.post('ai/sessions', { json: params }).json()
  return aiChatSessionSchema.parse(data)
}

export async function deleteChatSession(id: string): Promise<void> {
  await api.delete(`ai/sessions/${id}`)
}

type ChatChunk = { text: string; isThought: boolean; done: boolean; cachedTokens: number | null }

function parseSseLine(line: string): ChatChunk | null {
  const trimmed = line.trim()
  if (!trimmed || trimmed.startsWith(':')) return null
  const data = trimmed.startsWith('data: ') ? trimmed.slice(6) : trimmed
  try {
    return JSON.parse(data) as ChatChunk
  } catch {
    return null
  }
}

export async function* streamChatMessage(params: {
  sessionId: string
  message: string
  includePromptContext: boolean
  promptContent?: string
}): AsyncGenerator<ChatChunk> {
  const response = await fetch(`${apiBaseUrl}/ai/sessions/${params.sessionId}/messages`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
    body: JSON.stringify({
      message: params.message,
      includePromptContext: params.includePromptContext,
      promptContent: params.promptContent ?? null,
    }),
  })

  if (!response.ok) {
    const text = await response.text().catch(() => '')
    throw new Error(`HTTP ${response.status}${text ? `: ${text}` : ''}`)
  }

  const reader = response.body?.getReader()
  if (!reader) return

  const decoder = new TextDecoder()
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })

    // SSE events are separated by double newlines
    const events = buffer.split('\n\n')
    buffer = events.pop() ?? ''

    for (const event of events) {
      if (!event.trim()) continue
      // each event may have multiple "data:" lines — join them
      for (const line of event.split('\n')) {
        const chunk = parseSseLine(line)
        if (chunk) yield chunk
      }
    }
  }

  // flush remaining buffer
  if (buffer.trim()) {
    for (const line of buffer.split('\n')) {
      const chunk = parseSseLine(line)
      if (chunk) yield chunk
    }
  }
}
