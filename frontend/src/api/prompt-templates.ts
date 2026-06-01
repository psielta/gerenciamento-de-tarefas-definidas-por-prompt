import { api } from './client'
import {
  promptDraftSchema,
  promptTemplateListSchema,
  type GeneratedPromptDraft,
  type PromptTemplate,
} from './schemas'

export async function listPromptTemplates(): Promise<PromptTemplate[]> {
  const data = await api.get('prompt-templates').json<unknown>()
  return promptTemplateListSchema.parse(data)
}

export async function renderPromptDraft(
  linkedDocumentId: string,
  templateKey: string,
  options?: { pullRequest?: string },
): Promise<GeneratedPromptDraft> {
  const data = await api
    .post(`linked-documents/${linkedDocumentId}/prompt-drafts`, {
      json: { templateKey, pullRequest: options?.pullRequest },
    })
    .json<unknown>()

  return promptDraftSchema.parse(data)
}
