import { api } from './client'
import {
  fileContentSchema,
  fileReferenceValidationListSchema,
  fileSearchResultListSchema,
  fileTreeNodeListSchema,
} from './schemas'

export async function searchFiles(workingDirectoryId: string, query: string, limit = 30) {
  const searchParams = new URLSearchParams({
    workingDirectoryId,
    query,
    limit: limit.toString(),
  })

  const data = await api.get('files/search', { searchParams }).json<unknown>()
  return fileSearchResultListSchema.parse(data)
}

export async function validateFileReferences(workingDirectoryId: string, relativePaths: string[]) {
  const data = await api
    .post('files/validate-references', {
      json: {
        workingDirectoryId,
        relativePaths,
      },
    })
    .json<unknown>()

  return fileReferenceValidationListSchema.parse(data)
}

export async function browseDirectory(workingDirectoryId: string, relativePath = '') {
  const searchParams = new URLSearchParams({
    workingDirectoryId,
    relativePath,
  })

  const data = await api.get('files/tree', { searchParams }).json<unknown>()
  return fileTreeNodeListSchema.parse(data)
}

export async function getFileContent(workingDirectoryId: string, relativePath: string) {
  const searchParams = new URLSearchParams({
    workingDirectoryId,
    relativePath,
  })

  const data = await api.get('files/content', { searchParams }).json<unknown>()
  return fileContentSchema.parse(data)
}
