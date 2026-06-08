import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as diagramsApi from '@/api/diagrams'
import type { DiagramSummary } from '@/api/schemas'
import { DiagramList } from './diagram-list'

vi.mock('@/api/diagrams')

const sampleDiagram: DiagramSummary = {
  id: 'diagram-1',
  workingDirectoryId: 'ws-1',
  title: 'Arquitetura',
  description: null,
  type: 'Mermaid',
  isArchived: false,
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: '2026-06-01T00:00:00Z',
}

function renderList(onSelect = vi.fn()) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={queryClient}>
      <DiagramList workspaceId="ws-1" selectedDiagramId={null} onSelect={onSelect} />
    </QueryClientProvider>,
  )

  return { onSelect }
}

describe('DiagramList', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(diagramsApi.listDiagrams).mockResolvedValue([sampleDiagram])
    vi.mocked(diagramsApi.createDiagram).mockResolvedValue({
      ...sampleDiagram,
      id: 'diagram-2',
      title: 'Novo diagrama',
      content: '',
      metadataJson: null,
    })
  })

  afterEach(() => {
    cleanup()
  })

  it('renders diagrams filtered by the current workspace', async () => {
    renderList()

    expect(await screen.findByText('Arquitetura')).toBeInTheDocument()
    expect(diagramsApi.listDiagrams).toHaveBeenCalledWith({
      workingDirectoryId: 'ws-1',
      type: undefined,
      q: undefined,
      includeArchived: false,
    })
  })

  it('creates a Mermaid diagram and selects it', async () => {
    const { onSelect } = renderList()
    await screen.findByText('Arquitetura')

    await userEvent.click(screen.getByRole('button', { name: 'Novo' }))
    await userEvent.click(screen.getByRole('button', { name: 'Mermaid' }))

    await waitFor(() =>
      expect(diagramsApi.createDiagram).toHaveBeenCalledWith(
        expect.objectContaining({ workingDirectoryId: 'ws-1', type: 'Mermaid', title: 'Novo diagrama' }),
      ),
    )
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('diagram-2'))
  })

  it('shows an empty state when there are no diagrams', async () => {
    vi.mocked(diagramsApi.listDiagrams).mockResolvedValue([])
    renderList()

    expect(await screen.findByText('Nenhum diagrama neste workspace ainda.')).toBeInTheDocument()
  })
})
