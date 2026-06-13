import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as filesApi from '@/api/files'
import * as gitApi from '@/api/git'
import { FileExplorer } from './file-explorer'

vi.mock('./use-git-history', () => ({
  useGitHistory: () => ({ openHistory: vi.fn(), closeHistory: vi.fn(), target: null }),
}))

vi.mock('@/api/files')
vi.mock('@/api/git')

function renderExplorer(selectedPath: string | null, onClearSelection = vi.fn()) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })

  render(
    <QueryClientProvider client={queryClient}>
      <FileExplorer
        workingDirectoryId="ws-1"
        selectedPath={selectedPath}
        onSelectFile={vi.fn()}
        onClearSelection={onClearSelection}
      />
    </QueryClientProvider>,
  )

  return { onClearSelection }
}

describe('FileExplorer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(filesApi.browseDirectory).mockResolvedValue([
      { name: 'README.md', relativePath: 'README.md', isDirectory: false },
    ])
    vi.mocked(filesApi.getFileContent).mockResolvedValue({
      relativePath: 'scripts/setup.ts',
      content: 'console.log("setup")',
      sizeBytes: 20,
      truncated: false,
      isBinary: false,
    })
    vi.mocked(filesApi.searchFiles).mockResolvedValue([])
    vi.mocked(gitApi.getGitStatus).mockResolvedValue([])
    vi.mocked(gitApi.getGitOriginalFile).mockResolvedValue({ content: '' })
  })

  afterEach(() => {
    cleanup()
  })

  it('does not fetch the selected file when the workspace directory is missing', async () => {
    vi.mocked(filesApi.browseDirectory).mockRejectedValue(new Error('Directory was not found.'))
    const onClearSelection = vi.fn()

    renderExplorer('scripts/setup.ts', onClearSelection)

    expect(await screen.findAllByText('Diretorio do workspace nao encontrado.')).not.toHaveLength(0)
    await waitFor(() => expect(onClearSelection).toHaveBeenCalledTimes(1))
    expect(filesApi.getFileContent).not.toHaveBeenCalled()
    expect(gitApi.getGitStatus).not.toHaveBeenCalled()
  })
})
