import { api } from './client'
import {
  gitCommitContentSchema,
  gitDiffSchema,
  gitFileHistorySchema,
  gitOriginalFileSchema,
  gitStatusListSchema,
} from './schemas'

export async function getGitStatus(workingDirectoryId: string) {
  const searchParams = new URLSearchParams({
    workingDirectoryId,
  })

  const data = await api.get('git/status', { searchParams }).json<unknown>()
  return gitStatusListSchema.parse(data)
}

export async function getGitOriginalFile(workingDirectoryId: string, path: string) {
  const searchParams = new URLSearchParams({
    workingDirectoryId,
    path,
  })

  const data = await api.get('git/original-file', { searchParams }).json<unknown>()
  return gitOriginalFileSchema.parse(data)
}

export async function getGitDiff(workingDirectoryId: string, path: string) {
  const searchParams = new URLSearchParams({
    workingDirectoryId,
    path,
  })

  const data = await api.get('git/diff', { searchParams }).json<unknown>()
  return gitDiffSchema.parse(data)
}

export async function getFileGitHistory(workingDirectoryId: string, path: string) {
  const searchParams = new URLSearchParams({
    workingDirectoryId,
    path,
  })

  const data = await api.get('git/history', { searchParams }).json<unknown>()
  return gitFileHistorySchema.parse(data)
}

export async function getGitCommitContent(workingDirectoryId: string, path: string, hash: string) {
  const searchParams = new URLSearchParams({
    workingDirectoryId,
    path,
    hash,
  })

  const data = await api.get('git/file-content', { searchParams }).json<unknown>()
  return gitCommitContentSchema.parse(data)
}
