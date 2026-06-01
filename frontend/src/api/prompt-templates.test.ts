import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { api } from './client'
import { listPromptTemplates, renderPromptDraft } from './prompt-templates'

vi.mock('./client', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
  },
}))

const apiMock = api as unknown as {
  get: Mock
  post: Mock
}

function jsonResponse(payload: unknown) {
  return {
    json: () => Promise.resolve(payload),
  }
}

describe('prompt template api', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('lists prompt templates from the API', async () => {
    apiMock.get.mockReturnValue(
      jsonResponse([
        {
          key: 'ReviewPlan',
          displayName: 'Revisar plano',
          description: 'Valida o plano',
          defaultTargetAgent: 'Codex',
          defaultKind: 'Planning',
          input: null,
        },
      ]),
    )

    await expect(listPromptTemplates()).resolves.toEqual([
      {
        key: 'ReviewPlan',
        displayName: 'Revisar plano',
        description: 'Valida o plano',
        defaultTargetAgent: 'Codex',
        defaultKind: 'Planning',
        input: null,
      },
    ])
    expect(apiMock.get).toHaveBeenCalledWith('prompt-templates')
  })

  it('renders prompt drafts using opaque template keys', async () => {
    apiMock.post.mockReturnValue(
      jsonResponse({
        templateKey: 'ReviewPlan',
        linkedDocumentId: '019e9f6a-94e7-7a23-965d-c8b05c63ee59',
        workingDirectoryId: '019e9f6a-9fb2-7f24-ac3a-bf099d2c93c0',
        parentPromptId: '019e9f6a-a269-7991-95d5-4e602dcf773d',
        title: 'Revisar plano',
        content: 'Dado o plano "C:/plan.md", valide o plano.',
        targetAgent: 'Codex',
        kind: 'Planning',
      }),
    )

    await expect(
      renderPromptDraft('019e9f6a-94e7-7a23-965d-c8b05c63ee59', 'ReviewPlan'),
    ).resolves.toMatchObject({
      templateKey: 'ReviewPlan',
      targetAgent: 'Codex',
      kind: 'Planning',
    })
    expect(apiMock.post).toHaveBeenCalledWith(
      'linked-documents/019e9f6a-94e7-7a23-965d-c8b05c63ee59/prompt-drafts',
      { json: { templateKey: 'ReviewPlan', pullRequest: undefined } },
    )
  })

  it('sends the pull request when rendering PR review drafts', async () => {
    apiMock.post.mockReturnValue(
      jsonResponse({
        templateKey: 'ReviewPullRequest',
        linkedDocumentId: '019e9f6a-94e7-7a23-965d-c8b05c63ee59',
        workingDirectoryId: '019e9f6a-9fb2-7f24-ac3a-bf099d2c93c0',
        parentPromptId: '019e9f6a-a269-7991-95d5-4e602dcf773d',
        title: 'Revisar PR #42',
        content: 'Revise a PR #42.',
        targetAgent: 'Codex',
        kind: 'General',
      }),
    )

    await renderPromptDraft('019e9f6a-94e7-7a23-965d-c8b05c63ee59', 'ReviewPullRequest', {
      pullRequest: '42',
    })

    expect(apiMock.post).toHaveBeenCalledWith(
      'linked-documents/019e9f6a-94e7-7a23-965d-c8b05c63ee59/prompt-drafts',
      { json: { templateKey: 'ReviewPullRequest', pullRequest: '42' } },
    )
  })

  it('rejects invalid payloads', async () => {
    apiMock.get.mockReturnValue(jsonResponse([{ key: '', displayName: 'Invalid' }]))

    await expect(listPromptTemplates()).rejects.toThrow()
  })
})
