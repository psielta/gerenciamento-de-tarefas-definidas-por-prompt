import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as filesApi from '@/api/files'
import * as gitApi from '@/api/git'
import { FileViewerPanel } from './file-viewer-panel'

const monacoMocks = vi.hoisted(() => {
  const decorationsSet = vi.fn()
  const decorationsClear = vi.fn()
  const createDecorationsCollection = vi.fn(() => ({
    set: decorationsSet,
    clear: decorationsClear,
  }))
  const Range = vi.fn(function Range(
    this: { startLineNumber: number; startColumn: number; endLineNumber: number; endColumn: number },
    startLineNumber: number,
    startColumn: number,
    endLineNumber: number,
    endColumn: number,
  ) {
    this.startLineNumber = startLineNumber
    this.startColumn = startColumn
    this.endLineNumber = endLineNumber
    this.endColumn = endColumn
  })

  return { decorationsSet, decorationsClear, createDecorationsCollection, Range }
})

vi.mock('@/api/files')
vi.mock('@/api/git')
vi.mock('./use-file-subscription', () => ({
  useFileSubscription: () => {},
}))
vi.mock('@/components/theme/theme-provider', () => ({
  useTheme: () => ({ resolvedTheme: 'light' }),
}))
vi.mock('./monaco-setup', () => ({}))

type MonacoMockProps = {
  value: string
  options: { fontSize: number; minimap: { enabled: boolean }; wordWrap: string; glyphMargin?: boolean }
  onMount?: (editor: unknown, monaco: unknown) => void
}

vi.mock('@monaco-editor/react', () => ({
  default: ({ value, options, onMount }: MonacoMockProps) => {
    onMount?.(
      {
        createDecorationsCollection: monacoMocks.createDecorationsCollection,
      },
      {
        Range: monacoMocks.Range,
        editor: {
          OverviewRulerLane: { Left: 1 },
          MinimapPosition: { Gutter: 1 },
        },
      },
    )

    return (
      <div
        data-testid="monaco-editor"
        data-font-size={String(options.fontSize)}
        data-minimap={String(options.minimap.enabled)}
        data-word-wrap={options.wordWrap}
        data-glyph-margin={String(options.glyphMargin ?? false)}
      >
        {value}
      </div>
    )
  },
}))

function renderPanel(relativePath: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <FileViewerPanel workingDirectoryId="ws-1" relativePath={relativePath} />
    </QueryClientProvider>,
  )
}

describe('FileViewerPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    vi.mocked(gitApi.getGitStatus).mockResolvedValue([])
    vi.mocked(gitApi.getGitOriginalFile).mockResolvedValue({ content: '' })
    vi.mocked(filesApi.getFileContent).mockImplementation(async (_workingDirectoryId, relativePath) => ({
      relativePath,
      content: relativePath.endsWith('.md')
        ? '# Titulo\n\nParagrafo do plano.\n\n## Secao A\n\nDetalhes da secao.'
        : 'const total = 1',
      sizeBytes: 100,
      truncated: false,
      isBinary: false,
    }))
  })

  afterEach(() => {
    cleanup()
  })

  it('renders the Monaco viewer without the markdown toggle for non-markdown files', async () => {
    renderPanel('src/app.ts')

    expect(await screen.findByTestId('monaco-editor')).toHaveTextContent('const total = 1')
    expect(screen.queryByRole('group', { name: 'Modo de visualizacao do markdown' })).not.toBeInTheDocument()
    expect(screen.getByTestId('monaco-editor')).toHaveAttribute('data-glyph-margin', 'true')
  })

  it('toggles a markdown file between code and rendered preview', async () => {
    renderPanel('docs/plano.md')

    expect(await screen.findByTestId('monaco-editor')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Visual' }))
    expect(screen.getByRole('heading', { name: 'Titulo' })).toBeInTheDocument()
    expect(screen.queryByTestId('monaco-editor')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Codigo' }))
    expect(await screen.findByTestId('monaco-editor')).toBeInTheDocument()
  })

  it('persists the markdown view preference between mounts', async () => {
    const first = renderPanel('docs/plano.md')
    await screen.findByTestId('monaco-editor')
    await userEvent.click(screen.getByRole('button', { name: 'Visual' }))
    first.unmount()

    renderPanel('docs/plano.md')

    expect(await screen.findByRole('heading', { name: 'Titulo' })).toBeInTheDocument()
    expect(screen.queryByTestId('monaco-editor')).not.toBeInTheDocument()
  })

  it('shows the document outline in preview mode and scrolls to the clicked heading', async () => {
    const scrollIntoView = vi.fn()
    Element.prototype.scrollIntoView = scrollIntoView

    renderPanel('docs/plano.md')
    await screen.findByTestId('monaco-editor')
    await userEvent.click(screen.getByRole('button', { name: 'Visual' }))

    const outline = await screen.findByRole('navigation', { name: 'Sumario do documento' })
    expect(within(outline).getByRole('button', { name: 'Titulo' })).toBeInTheDocument()

    await userEvent.click(within(outline).getByRole('button', { name: 'Secao A' }))
    expect(scrollIntoView).toHaveBeenCalledTimes(1)

    await userEvent.click(screen.getByRole('button', { name: 'Alternar sumario' }))
    expect(screen.queryByRole('navigation', { name: 'Sumario do documento' })).not.toBeInTheDocument()
  })

  it('adjusts font size, minimap and word wrap through the toolbar controls', async () => {
    renderPanel('src/app.ts')

    const editor = await screen.findByTestId('monaco-editor')
    expect(editor).toHaveAttribute('data-font-size', '13')
    expect(editor).toHaveAttribute('data-minimap', 'true')
    expect(editor).toHaveAttribute('data-word-wrap', 'on')

    await userEvent.click(screen.getByRole('button', { name: 'Aumentar fonte do editor' }))
    expect(screen.getByTestId('monaco-editor')).toHaveAttribute('data-font-size', '14')

    await userEvent.click(screen.getByRole('button', { name: 'Restaurar tamanho padrao da fonte' }))
    expect(screen.getByTestId('monaco-editor')).toHaveAttribute('data-font-size', '13')

    await userEvent.click(screen.getByRole('button', { name: 'Alternar minimapa' }))
    expect(screen.getByTestId('monaco-editor')).toHaveAttribute('data-minimap', 'false')

    await userEvent.click(screen.getByRole('button', { name: 'Alternar quebra de linha' }))
    expect(screen.getByTestId('monaco-editor')).toHaveAttribute('data-word-wrap', 'off')
  })

  it('adds git line decorations for modified files opened in the regular editor', async () => {
    vi.mocked(gitApi.getGitStatus).mockResolvedValue([{ path: 'src/app.ts', status: 'Modified', originalPath: null }])
    vi.mocked(gitApi.getGitOriginalFile).mockResolvedValue({
      content: 'const total = 1\nconst enabled = true',
    })
    vi.mocked(filesApi.getFileContent).mockResolvedValue({
      relativePath: 'src/app.ts',
      content: 'const total = 2\nconst enabled = true',
      sizeBytes: 42,
      truncated: false,
      isBinary: false,
    })

    renderPanel('src/app.ts')
    await screen.findByTestId('monaco-editor')

    await waitFor(() => {
      const decorations = monacoMocks.decorationsSet.mock.calls.at(-1)?.[0] ?? []
      expect(decorations).toEqual(
        expect.arrayContaining([
          expect.objectContaining({
            range: expect.objectContaining({ startLineNumber: 1, endLineNumber: 1 }),
            options: expect.objectContaining({
              className: expect.stringContaining('git-line-change-background-modified'),
              glyphMarginClassName: expect.stringContaining('git-line-change-glyph-modified'),
            }),
          }),
        ]),
      )
    })
    expect(gitApi.getGitOriginalFile).toHaveBeenCalledWith('ws-1', 'src/app.ts')
  })

  it('marks untracked files as added without fetching original content', async () => {
    vi.mocked(gitApi.getGitStatus).mockResolvedValue([{ path: 'src/new-file.ts', status: 'Untracked' }])
    vi.mocked(filesApi.getFileContent).mockResolvedValue({
      relativePath: 'src/new-file.ts',
      content: 'const created = true\nexport { created }',
      sizeBytes: 48,
      truncated: false,
      isBinary: false,
    })

    renderPanel('src/new-file.ts')
    await screen.findByTestId('monaco-editor')

    await waitFor(() => {
      const decorations = monacoMocks.decorationsSet.mock.calls.at(-1)?.[0] ?? []
      expect(decorations).toEqual(
        expect.arrayContaining([
          expect.objectContaining({
            range: expect.objectContaining({ startLineNumber: 1, endLineNumber: 2 }),
            options: expect.objectContaining({
              className: expect.stringContaining('git-line-change-background-added'),
            }),
          }),
        ]),
      )
    })
    expect(gitApi.getGitOriginalFile).not.toHaveBeenCalled()
  })
})
