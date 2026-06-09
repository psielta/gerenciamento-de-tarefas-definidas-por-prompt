import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as filesApi from '@/api/files'
import type { FileSearchResult } from '@/api/schemas'
import { FileSearchPalette } from './file-search-palette'

vi.mock('@/api/files')

const results: FileSearchResult[] = [
  { relativePath: 'src/components/button.tsx', fileName: 'button.tsx', isDirectory: false, score: 2 },
  { relativePath: 'src/components/badge.tsx', fileName: 'badge.tsx', isDirectory: false, score: 1 },
  { relativePath: 'src/components', fileName: 'components', isDirectory: true, score: 0.5 },
]

function renderPalette() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const onSelectFile = vi.fn()
  const onClose = vi.fn()

  render(
    <QueryClientProvider client={queryClient}>
      <FileSearchPalette workingDirectoryId="ws-1" onSelectFile={onSelectFile} onClose={onClose} />
    </QueryClientProvider>,
  )

  return { onSelectFile, onClose }
}

describe('FileSearchPalette', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(filesApi.searchFiles).mockResolvedValue(results)
  })

  afterEach(() => {
    cleanup()
  })

  it('searches files and hides directories from the results', async () => {
    renderPalette()

    await userEvent.type(screen.getByRole('textbox', { name: /buscar arquivos/i }), 'comp')

    expect(await screen.findByText('button.tsx')).toBeInTheDocument()
    expect(screen.getByText('badge.tsx')).toBeInTheDocument()
    expect(screen.queryByText('src/components', { exact: true })).not.toBeInTheDocument()
    expect(filesApi.searchFiles).toHaveBeenCalledWith('ws-1', 'comp', 30)
  })

  it('opens the highlighted result with the keyboard', async () => {
    const { onSelectFile, onClose } = renderPalette()

    await userEvent.type(screen.getByRole('textbox', { name: /buscar arquivos/i }), 'comp')
    await screen.findByText('button.tsx')

    await userEvent.keyboard('{ArrowDown}{Enter}')

    expect(onSelectFile).toHaveBeenCalledWith('src/components/badge.tsx')
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('opens a result on click', async () => {
    const { onSelectFile, onClose } = renderPalette()

    await userEvent.type(screen.getByRole('textbox', { name: /buscar arquivos/i }), 'comp')
    await userEvent.click(await screen.findByText('button.tsx'))

    expect(onSelectFile).toHaveBeenCalledWith('src/components/button.tsx')
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('shows an empty state when nothing matches', async () => {
    vi.mocked(filesApi.searchFiles).mockResolvedValue([])
    renderPalette()

    await userEvent.type(screen.getByRole('textbox', { name: /buscar arquivos/i }), 'nada')

    expect(await screen.findByText('Nenhum arquivo encontrado.')).toBeInTheDocument()
  })
})
