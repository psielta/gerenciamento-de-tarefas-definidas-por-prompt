import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as filesApi from '@/api/files'
import * as gitApi from '@/api/git'
import { GitCommitDiffViewer } from './git-commit-diff-viewer'

vi.mock('@/api/files')
vi.mock('@/api/git')
vi.mock('@/components/theme/theme-provider', () => ({
  useTheme: () => ({ resolvedTheme: 'light' }),
}))
vi.mock('./monaco-setup', () => ({
  resolveMonacoTheme: () => 'vs',
}))

type DiffEditorMockProps = {
  original: string
  modified: string
  language: string
}

vi.mock('@monaco-editor/react', () => ({
  DiffEditor: ({ original, modified, language }: DiffEditorMockProps) => (
    <div data-testid="diff-editor" data-original={original} data-modified={modified} data-language={language} />
  ),
}))

function renderViewer(props: Partial<React.ComponentProps<typeof GitCommitDiffViewer>> = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={queryClient}>
      <GitCommitDiffViewer
        workingDirectoryId="ws-1"
        path="src/app.ts"
        original={{ kind: 'hash', hash: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' }}
        modified={{ kind: 'hash', hash: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb' }}
        {...props}
      />
    </QueryClientProvider>,
  )
}

describe('GitCommitDiffViewer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(gitApi.getGitCommitContent).mockImplementation(async (_wd, _path, hash) => ({
      content: hash.startsWith('aaaa') ? 'old' : 'new',
      exists: true,
      isBinary: false,
      truncated: false,
    }))
    vi.mocked(filesApi.getFileContent).mockResolvedValue({
      relativePath: 'src/app.ts',
      content: 'working',
      sizeBytes: 10,
      truncated: false,
      isBinary: false,
    })
  })

  afterEach(() => {
    cleanup()
  })

  it('wires two-commit diff sources', async () => {
    renderViewer()

    const editor = await screen.findByTestId('diff-editor')
    expect(editor).toHaveAttribute('data-original', 'old')
    expect(editor).toHaveAttribute('data-modified', 'new')
  })

  it('wires vs-working-tree mode', async () => {
    renderViewer({
      original: { kind: 'hash', hash: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' },
      modified: { kind: 'working' },
    })

    const editor = await screen.findByTestId('diff-editor')
    expect(editor).toHaveAttribute('data-original', 'old')
    expect(editor).toHaveAttribute('data-modified', 'working')
  })

  it('short-circuits binary content', async () => {
    vi.mocked(gitApi.getGitCommitContent).mockResolvedValue({
      content: '',
      exists: true,
      isBinary: true,
      truncated: false,
    })

    renderViewer()

    expect(await screen.findByText('Arquivo binario. Visualizacao de diff indisponivel.')).toBeInTheDocument()
    expect(screen.queryByTestId('diff-editor')).not.toBeInTheDocument()
  })

  it('short-circuits truncated content', async () => {
    vi.mocked(gitApi.getGitCommitContent).mockResolvedValue({
      content: '',
      exists: true,
      isBinary: false,
      truncated: true,
    })

    renderViewer()

    expect(
      await screen.findByText('Arquivo truncado para visualizacao. Abra no editor local para ver o conteudo completo.'),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('diff-editor')).not.toBeInTheDocument()
  })
})