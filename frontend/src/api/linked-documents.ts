import { api } from './client'
import {
  linkedDocumentContentSchema,
  linkedDocumentListSchema,
  linkedDocumentSchema,
  linkedDocumentVersionListSchema,
  type LinkedDocument,
  type LinkedDocumentContent,
  type LinkedDocumentType,
  type LinkedDocumentVersion,
} from './schemas'

export type LinkDocumentPayload = {
  absolutePath: string
  documentType?: LinkedDocumentType
  displayName?: string | null
}

export async function listLinkedDocuments(promptId: string): Promise<LinkedDocument[]> {
  const data = await api.get(`prompts/${promptId}/linked-documents`).json<unknown>()
  return linkedDocumentListSchema.parse(data)
}

export async function getLinkedDocument(id: string): Promise<LinkedDocument> {
  const data = await api.get(`linked-documents/${id}`).json<unknown>()
  return linkedDocumentSchema.parse(data)
}

export async function getLinkedDocumentContent(
  id: string,
  version?: number,
): Promise<LinkedDocumentContent> {
  const searchParams = new URLSearchParams()
  if (version) {
    searchParams.set('version', String(version))
  }

  const data = await api.get(`linked-documents/${id}/content`, { searchParams }).json<unknown>()
  return linkedDocumentContentSchema.parse(data)
}

export async function listLinkedDocumentVersions(id: string): Promise<LinkedDocumentVersion[]> {
  const data = await api.get(`linked-documents/${id}/versions`).json<unknown>()
  return linkedDocumentVersionListSchema.parse(data)
}

export async function linkLinkedDocument(
  promptId: string,
  payload: LinkDocumentPayload,
): Promise<LinkedDocument> {
  const data = await api
    .post(`prompts/${promptId}/linked-documents`, {
      json: payload,
    })
    .json<unknown>()

  return linkedDocumentSchema.parse(data)
}

export async function pauseLinkedDocument(id: string): Promise<LinkedDocument> {
  const data = await api.post(`linked-documents/${id}/pause`).json<unknown>()
  return linkedDocumentSchema.parse(data)
}

export async function resumeLinkedDocument(id: string): Promise<LinkedDocument> {
  const data = await api.post(`linked-documents/${id}/resume`).json<unknown>()
  return linkedDocumentSchema.parse(data)
}

export async function refreshLinkedDocument(id: string): Promise<LinkedDocument> {
  const data = await api.post(`linked-documents/${id}/refresh`).json<unknown>()
  return linkedDocumentSchema.parse(data)
}

export async function setLinkedDocumentPullRequest(
  id: string,
  pullRequest: string | null,
): Promise<LinkedDocument> {
  const data = await api
    .put(`linked-documents/${id}/pull-request`, { json: { pullRequest } })
    .json<unknown>()
  return linkedDocumentSchema.parse(data)
}

export async function removeLinkedDocument(id: string) {
  await api.delete(`linked-documents/${id}`)
}
