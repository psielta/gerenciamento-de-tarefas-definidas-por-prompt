import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as filesApi from '@/api/files'
import * as gitApi from '@/api/git'
import type { WorkingDirectory } from '@/api/schemas'
import { ExpandedFileExplorer } from './expanded-file-explorer'

vi.mock('@/api/files')
vi.mock('@/api/git')

const workspace: WorkingDirectory = {
  id: 'ws-1',
  name: 'repo',
  absolutePath: 'D:/repo',
  respectGitignore: true,
  enableAiContext: false,
  taskNumberPattern: null,
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: '2026-06-01T00:00:00Z',
}

function renderExpanded() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const onExit = vi.fn()
  const onSelectFile = vi.fn()

  render(
    <QueryClientProvider client={queryClient}>
      <ExpandedFileExplorer
        workingDirectoryId="ws-1"
        workspaces={[workspace]}
        onChangeWorkspace={vi.fn()}
        selectedPath={null}
        onSelectFile={onSelectFile}
        onExit={onExit}
      />
    </QueryClientProvider>,
  )

  return { onExit, onSelectFile }
}

describe('ExpandedFileExplorer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(filesApi.browseDirectory).mockResolvedValue([
      { name: 'README.md', relativePath: 'README.md', isDirectory: false },
    ])
    vi.mocked(filesApi.searchFiles).mockResolvedValue([])
    vi.mocked(gitApi.getGitStatus).mockResolvedValue([])
  })

  afterEach(() => {
    cleanup()
  })

  it('renders the tree, the empty viewer state and exits with Escape', async () => {
    const { onExit } = renderExpanded()

    expect(await screen.findByText('README.md')).toBeInTheDocument()
    expect(screen.getByText('Nenhum arquivo aberto.')).toBeInTheDocument()

    await userEvent.keyboard('{Escape}')

    expect(onExit).toHaveBeenCalledTimes(1)
  })

  it('opens the search palette with Ctrl+K and closes it with Escape before exiting', async () => {
    const { onExit } = renderExpanded()
    await screen.findByText('README.md')

    await userEvent.keyboard('{Control>}k{/Control}')
    expect(screen.getByRole('dialog', { name: 'Buscar arquivos no workspace' })).toBeInTheDocument()

    await userEvent.keyboard('{Escape}')
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Buscar arquivos no workspace' })).not.toBeInTheDocument(),
    )
    expect(onExit).not.toHaveBeenCalled()

    await userEvent.keyboard('{Escape}')
    expect(onExit).toHaveBeenCalledTimes(1)
  })

  it('collapses and restores the file tree', async () => {
    renderExpanded()
    await screen.findByText('README.md')

    await userEvent.click(screen.getByRole('button', { name: 'Recolher arvore de arquivos' }))
    expect(screen.queryByText('Arquivos do workspace')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Mostrar arvore de arquivos' }))
    expect(await screen.findByText('Arquivos do workspace')).toBeInTheDocument()
  })

  it('selects a file from the tree', async () => {
    const { onSelectFile } = renderExpanded()

    await userEvent.click(await screen.findByText('README.md'))

    expect(onSelectFile).toHaveBeenCalledWith('README.md')
  })
})
