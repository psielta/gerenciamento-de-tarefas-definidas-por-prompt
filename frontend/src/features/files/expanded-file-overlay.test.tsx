import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as filesApi from '@/api/files'
import * as workingDirectoriesApi from '@/api/working-directories'
import type { WorkingDirectory } from '@/api/schemas'
import { ExpandedFileOverlay } from './expanded-file-overlay'

vi.mock('@/api/files')
vi.mock('@/api/working-directories')
vi.mock('./file-viewer-panel', () => ({
  FileViewerPanel: ({ relativePath }: { relativePath: string }) => (
    <div data-testid="file-viewer-panel">{`viewer:${relativePath}`}</div>
  ),
}))

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

type OverlayProps = Partial<Parameters<typeof ExpandedFileOverlay>[0]>

function renderOverlay(props: OverlayProps = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const onExit = vi.fn()
  const onSelectFile = vi.fn()

  render(
    <QueryClientProvider client={queryClient}>
      <ExpandedFileOverlay
        workingDirectoryId="ws-1"
        onExit={onExit}
        onSelectFile={onSelectFile}
        {...props}
      />
    </QueryClientProvider>,
  )

  return { onExit, onSelectFile }
}

describe('ExpandedFileOverlay', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    vi.mocked(workingDirectoriesApi.listWorkingDirectories).mockResolvedValue([workspace])
    vi.mocked(filesApi.browseDirectory).mockResolvedValue([
      { name: 'README.md', relativePath: 'README.md', isDirectory: false },
    ])
    vi.mocked(filesApi.searchFiles).mockResolvedValue([])
  })

  afterEach(() => {
    cleanup()
  })

  it('loads workspaces and opens the initial file', async () => {
    renderOverlay({ initialPath: 'README.md' })

    expect(await screen.findByTestId('file-viewer-panel')).toHaveTextContent('viewer:README.md')
    expect(await screen.findByText('README.md')).toBeInTheDocument()
  })

  it('restores the last opened file when there is no initial path', async () => {
    localStorage.setItem('prompt-tasks:files:last-opened:ws-1', 'docs/guia.md')

    renderOverlay()

    expect(await screen.findByTestId('file-viewer-panel')).toHaveTextContent('viewer:docs/guia.md')
  })

  it('selects a file, notifies the origin surface and persists the last opened file', async () => {
    const { onSelectFile } = renderOverlay()

    expect(await screen.findByText('Nenhum arquivo aberto.')).toBeInTheDocument()

    await userEvent.click(await screen.findByText('README.md'))

    expect(onSelectFile).toHaveBeenCalledWith('ws-1', 'README.md')
    expect(screen.getByTestId('file-viewer-panel')).toHaveTextContent('viewer:README.md')
    expect(localStorage.getItem('prompt-tasks:files:last-opened:ws-1')).toBe('README.md')
  })

  it('shows the loading fallback with an exit action while workspaces load', async () => {
    vi.mocked(workingDirectoriesApi.listWorkingDirectories).mockReturnValue(
      new Promise<WorkingDirectory[]>(() => {}),
    )
    const { onExit } = renderOverlay()

    expect(screen.getByText('Carregando diretorios...')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }))
    expect(onExit).toHaveBeenCalledTimes(1)
  })
})
