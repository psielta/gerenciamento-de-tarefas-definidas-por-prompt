import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { api } from './client'
import { createDiagram, getDiagram, listDiagrams, setDiagramArchived, updateDiagram } from './diagrams'

vi.mock('./client', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}))

const apiMock = api as unknown as {
  get: Mock
  post: Mock
  put: Mock
  delete: Mock
}

function jsonResponse(payload: unknown) {
  return { json: () => Promise.resolve(payload) }
}

const workspaceId = '019e9f6a-9fb2-7f24-ac3a-bf099d2c93c0'
const diagramId = '019e9f6a-94e7-7a23-965d-c8b05c63ee59'

const sampleSummary = {
  id: diagramId,
  workingDirectoryId: workspaceId,
  title: 'Board',
  description: null,
  type: 'Excalidraw',
  isArchived: false,
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: '2026-06-01T00:00:00Z',
}

const sampleDiagram = { ...sampleSummary, content: '{}', metadataJson: null }

describe('diagrams api', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('lists diagrams with workspace, type, search and archived filters', async () => {
    apiMock.get.mockReturnValue(jsonResponse([sampleSummary]))

    await expect(
      listDiagrams({ workingDirectoryId: workspaceId, type: 'Mermaid', q: 'redis', includeArchived: true }),
    ).resolves.toEqual([sampleSummary])

    const [path, options] = apiMock.get.mock.calls[0]
    expect(path).toBe('diagrams')
    expect((options.searchParams as URLSearchParams).toString()).toBe(
      `workingDirectoryId=${workspaceId}&type=Mermaid&q=redis&includeArchived=true`,
    )
  })

  it('sends only the workspace when no extra filters are set', async () => {
    apiMock.get.mockReturnValue(jsonResponse([]))

    await listDiagrams({ workingDirectoryId: workspaceId })

    const [, options] = apiMock.get.mock.calls[0]
    expect((options.searchParams as URLSearchParams).toString()).toBe(`workingDirectoryId=${workspaceId}`)
  })

  it('rejects invalid payloads', async () => {
    apiMock.get.mockReturnValue(jsonResponse([{ id: 'not-a-uuid', title: 'x' }]))

    await expect(listDiagrams({ workingDirectoryId: workspaceId })).rejects.toThrow()
  })

  it('fetches a diagram detail with content', async () => {
    apiMock.get.mockReturnValue(jsonResponse(sampleDiagram))

    await expect(getDiagram(diagramId)).resolves.toMatchObject({ id: diagramId, content: '{}' })
    expect(apiMock.get).toHaveBeenCalledWith(`diagrams/${diagramId}`)
  })

  it('creates a diagram', async () => {
    apiMock.post.mockReturnValue(jsonResponse(sampleDiagram))

    await expect(
      createDiagram({ workingDirectoryId: workspaceId, title: 'Board', type: 'Excalidraw', content: '{}' }),
    ).resolves.toMatchObject({ id: diagramId })
    expect(apiMock.post).toHaveBeenCalledWith('diagrams', {
      json: { workingDirectoryId: workspaceId, title: 'Board', type: 'Excalidraw', content: '{}' },
    })
  })

  it('updates a diagram', async () => {
    apiMock.put.mockReturnValue(jsonResponse({ ...sampleDiagram, title: 'Board v2' }))

    await expect(updateDiagram(diagramId, { title: 'Board v2', content: '{}' })).resolves.toMatchObject({
      title: 'Board v2',
    })
    expect(apiMock.put).toHaveBeenCalledWith(`diagrams/${diagramId}`, {
      json: { title: 'Board v2', content: '{}' },
    })
  })

  it('archives a diagram', async () => {
    apiMock.post.mockReturnValue(jsonResponse({ ...sampleDiagram, isArchived: true }))

    await expect(setDiagramArchived(diagramId, true)).resolves.toMatchObject({ isArchived: true })
    expect(apiMock.post).toHaveBeenCalledWith(`diagrams/${diagramId}/archive`, { json: { isArchived: true } })
  })
})
