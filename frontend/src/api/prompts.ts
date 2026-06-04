import { api } from './client'
import { type PromptFilters } from './query-keys'
import {
  promptListSchema,
  promptSchema,
  promptVersionListSchema,
  type FileMention,
  type Prompt,
  type PromptKind,
  type PromptStatus,
  type PromptTemplateKey,
  type PromptVersion,
  type TargetAgent,
} from './schemas'

export type PromptPayload = {
  workingDirectoryId: string
  parentPromptId?: string | null
  title: string
  content: string
  targetAgent: TargetAgent
  kind: PromptKind
  status: PromptStatus
  sourceTemplateKey?: PromptTemplateKey
  mentions: FileMention[]
}

export type UpdatePromptPayload = Omit<PromptPayload, 'workingDirectoryId' | 'sourceTemplateKey'> & {
  rowVersion: string
}

export async function listPrompts(filters: PromptFilters): Promise<Prompt[]> {
  const searchParams = new URLSearchParams()
  if (filters.workingDirectoryId) {
    searchParams.set('workingDirectoryId', filters.workingDirectoryId)
  }
  if (filters.parentPromptId) {
    searchParams.set('parentPromptId', filters.parentPromptId)
  }
  if (filters.rootOnly) {
    searchParams.set('rootOnly', 'true')
  }
  if (filters.status) {
    searchParams.set('status', filters.status)
  }
  if (filters.agent) {
    searchParams.set('agent', filters.agent)
  }
  if (filters.kind) {
    searchParams.set('kind', filters.kind)
  }
  if (filters.q) {
    searchParams.set('q', filters.q)
  }

  const data = await api.get('prompts', { searchParams }).json<unknown>()
  return promptListSchema.parse(data)
}

export async function getPrompt(id: string): Promise<Prompt> {
  const data = await api.get(`prompts/${id}`).json<unknown>()
  return promptSchema.parse(data)
}

export async function getPromptByTaskNumber(workingDirectoryId: string, taskNumber: string): Promise<Prompt> {
  const searchParams = new URLSearchParams({ workingDirectoryId, taskNumber })
  const data = await api.get('prompts/by-task-number', { searchParams }).json<unknown>()
  return promptSchema.parse(data)
}

export async function createPrompt(payload: PromptPayload): Promise<Prompt> {
  const data = await api
    .post('prompts', {
      json: payload,
    })
    .json<unknown>()

  return promptSchema.parse(data)
}

export async function updatePrompt(id: string, payload: UpdatePromptPayload): Promise<Prompt> {
  const data = await api
    .put(`prompts/${id}`, {
      json: payload,
    })
    .json<unknown>()

  return promptSchema.parse(data)
}

export async function updatePromptStatus(
  id: string,
  status: PromptStatus,
  rowVersion: string,
): Promise<Prompt> {
  const data = await api
    .patch(`prompts/${id}/status`, {
      json: { status, rowVersion },
    })
    .json<unknown>()

  return promptSchema.parse(data)
}

export async function deletePrompt(id: string) {
  await api.delete(`prompts/${id}`)
}

export async function listPromptVersions(id: string): Promise<PromptVersion[]> {
  const data = await api.get(`prompts/${id}/versions`).json<unknown>()
  return promptVersionListSchema.parse(data)
}
