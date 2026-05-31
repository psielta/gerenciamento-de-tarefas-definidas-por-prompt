import { api } from './client'
import {
  validatePathResponseSchema,
  workingDirectoryListSchema,
  workingDirectorySchema,
  type ValidatePathResponse,
  type WorkingDirectory,
} from './schemas'

export type WorkingDirectoryPayload = {
  name: string
  absolutePath: string
  respectGitignore: boolean
  enableAiContext: boolean
}

export async function listWorkingDirectories() {
  const data = await api.get('working-directories').json<unknown>()
  return workingDirectoryListSchema.parse(data)
}

export async function getWorkingDirectory(id: string) {
  const data = await api.get(`working-directories/${id}`).json<unknown>()
  return workingDirectorySchema.parse(data)
}

export async function validateWorkingDirectoryPath(absolutePath: string): Promise<ValidatePathResponse> {
  const data = await api
    .post('working-directories/validate-path', {
      json: { absolutePath },
    })
    .json<unknown>()

  return validatePathResponseSchema.parse(data)
}

export async function createWorkingDirectory(payload: WorkingDirectoryPayload): Promise<WorkingDirectory> {
  const data = await api
    .post('working-directories', {
      json: payload,
    })
    .json<unknown>()

  return workingDirectorySchema.parse(data)
}

export async function updateWorkingDirectory(id: string, payload: WorkingDirectoryPayload): Promise<WorkingDirectory> {
  const data = await api
    .put(`working-directories/${id}`, {
      json: payload,
    })
    .json<unknown>()

  return workingDirectorySchema.parse(data)
}

export async function deleteWorkingDirectory(id: string) {
  await api.delete(`working-directories/${id}`)
}
