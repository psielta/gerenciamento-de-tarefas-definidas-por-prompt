import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { api } from './client'
import { getFileGitHistory, getGitCommitContent, getGitDiff, getGitOriginalFile, getGitStatus } from './git'

vi.mock('./client', () => ({
  api: {
    get: vi.fn(),
  },
}))

const apiMock = api as unknown as {
  get: Mock
}

function jsonResponse(payload: unknown) {
  return { json: () => Promise.resolve(payload) }
}

describe('git api', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('fetches git status with a working directory id', async () => {
    apiMock.get.mockReturnValue(jsonResponse([{ path: 'src/app.ts', status: 'Modified', originalPath: null }]))

    await expect(getGitStatus('ws-1')).resolves.toEqual([
      { path: 'src/app.ts', status: 'Modified', originalPath: null },
    ])

    const [path, options] = apiMock.get.mock.calls[0]
    expect(path).toBe('git/status')
    expect((options.searchParams as URLSearchParams).toString()).toBe('workingDirectoryId=ws-1')
  })

  it('fetches original file content', async () => {
    apiMock.get.mockReturnValue(jsonResponse({ content: 'original' }))

    await expect(getGitOriginalFile('ws-1', 'src/app.ts')).resolves.toEqual({ content: 'original' })

    const [path, options] = apiMock.get.mock.calls[0]
    expect(path).toBe('git/original-file')
    expect((options.searchParams as URLSearchParams).toString()).toBe('workingDirectoryId=ws-1&path=src%2Fapp.ts')
  })

  it('fetches git diff', async () => {
    apiMock.get.mockReturnValue(jsonResponse({ diff: 'diff --git' }))

    await expect(getGitDiff('ws-1', 'src/app.ts')).resolves.toEqual({ diff: 'diff --git' })

    const [path, options] = apiMock.get.mock.calls[0]
    expect(path).toBe('git/diff')
    expect((options.searchParams as URLSearchParams).toString()).toBe('workingDirectoryId=ws-1&path=src%2Fapp.ts')
  })

  it('rejects invalid status values', async () => {
    apiMock.get.mockReturnValue(jsonResponse([{ path: 'src/app.ts', status: 'Changed' }]))

    await expect(getGitStatus('ws-1')).rejects.toThrow()
  })

  it('fetches file git history', async () => {
    apiMock.get.mockReturnValue(
      jsonResponse({
        isRepository: true,
        commits: [
          {
            hash: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
            shortHash: 'aaaaaaa',
            author: 'Author',
            date: '2026-01-01T00:00:00Z',
            message: 'Initial',
            parentHash: '',
          },
        ],
      }),
    )

    await expect(getFileGitHistory('ws-1', 'src/app.ts')).resolves.toEqual({
      isRepository: true,
      commits: [
        {
          hash: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
          shortHash: 'aaaaaaa',
          author: 'Author',
          date: '2026-01-01T00:00:00Z',
          message: 'Initial',
          parentHash: '',
        },
      ],
    })

    const [path, options] = apiMock.get.mock.calls[0]
    expect(path).toBe('git/history')
    expect((options.searchParams as URLSearchParams).toString()).toBe('workingDirectoryId=ws-1&path=src%2Fapp.ts')
  })

  it('fetches commit content at hash', async () => {
    apiMock.get.mockReturnValue(
      jsonResponse({ content: 'old', exists: true, isBinary: false, truncated: false }),
    )

    await expect(getGitCommitContent('ws-1', 'src/app.ts', 'abcdef0123456')).resolves.toEqual({
      content: 'old',
      exists: true,
      isBinary: false,
      truncated: false,
    })

    const [path, options] = apiMock.get.mock.calls[0]
    expect(path).toBe('git/file-content')
    expect((options.searchParams as URLSearchParams).toString()).toBe(
      'workingDirectoryId=ws-1&path=src%2Fapp.ts&hash=abcdef0123456',
    )
  })

  it('rejects invalid history payloads', async () => {
    apiMock.get.mockReturnValue(jsonResponse({ isRepository: 'yes', commits: [] }))

    await expect(getFileGitHistory('ws-1', 'src/app.ts')).rejects.toThrow()
  })
})
