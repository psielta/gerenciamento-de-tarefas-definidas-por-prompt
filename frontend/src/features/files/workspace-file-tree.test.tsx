import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ComponentProps } from 'react'
import * as filesApi from '@/api/files'
import * as gitApi from '@/api/git'
import type { FileTreeNode, GitFileStatus } from '@/api/schemas'
import { WorkspaceFileTree } from './workspace-file-tree'

vi.mock('@/api/files')
vi.mock('@/api/git')

const sampleNodes: FileTreeNode[] = [
  { name: 'src', relativePath: 'src', isDirectory: true },
  { name: 'README.md', relativePath: 'README.md', isDirectory: false },
]

function renderTree(props: Partial<ComponentProps<typeof WorkspaceFileTree>> = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const onSelectFile = vi.fn()

  render(
    <QueryClientProvider client={queryClient}>
      <WorkspaceFileTree workingDirectoryId="ws-1" onSelectFile={onSelectFile} {...props} />
    </QueryClientProvider>,
  )

  return { onSelectFile: props.onSelectFile ?? onSelectFile }
}

describe('WorkspaceFileTree', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(filesApi.browseDirectory).mockResolvedValue(sampleNodes)
    vi.mocked(filesApi.searchFiles).mockResolvedValue([])
    vi.mocked(gitApi.getGitStatus).mockResolvedValue([])
  })

  afterEach(() => {
    cleanup()
  })

  it('renders the root nodes returned by the API', async () => {
    renderTree()

    expect(await screen.findByText('README.md')).toBeInTheDocument()
    expect(filesApi.browseDirectory).toHaveBeenCalledWith('ws-1', '')
  })

  it('reloads the tree when the refresh button is clicked', async () => {
    renderTree()
    await screen.findByText('README.md')
    expect(filesApi.browseDirectory).toHaveBeenCalledTimes(1)

    await userEvent.click(screen.getByRole('button', { name: 'Recarregar arquivos do workspace' }))

    await waitFor(() => expect(filesApi.browseDirectory).toHaveBeenCalledTimes(2))
    expect(filesApi.browseDirectory).toHaveBeenLastCalledWith('ws-1', '')
    expect(await screen.findByText('README.md')).toBeInTheDocument()
  })

  it('renders git status badges for changed files', async () => {
    vi.mocked(gitApi.getGitStatus).mockResolvedValue([
      { path: 'README.md', status: 'Modified', originalPath: null },
    ])

    renderTree()

    const badges = await screen.findAllByTitle('Modificado')
    expect(badges.some((badge) => badge.textContent === 'M')).toBe(true)
  })

  it('renders git changes inside the workspace file tree', async () => {
    const changes: GitFileStatus[] = [
      { path: 'src/app.ts', status: 'Modified', originalPath: null },
      { path: 'deleted.txt', status: 'Deleted', originalPath: null },
    ]
    vi.mocked(gitApi.getGitStatus).mockResolvedValue(changes)

    renderTree()

    expect(await screen.findByText('Alteracoes (git)')).toBeInTheDocument()
    expect(screen.getByText('app.ts')).toBeInTheDocument()
    expect(screen.getByText('deleted.txt')).toBeInTheDocument()
    expect(screen.getByTitle('Excluido')).toHaveTextContent('D')
  })

  it('emits selected git changes when a diff handler is provided', async () => {
    const changes: GitFileStatus[] = [
      { path: 'src/app.ts', status: 'Modified', originalPath: null },
      { path: 'deleted.txt', status: 'Deleted', originalPath: null },
    ]
    const onSelectGitChange = vi.fn()
    vi.mocked(gitApi.getGitStatus).mockResolvedValue(changes)

    renderTree({ onSelectGitChange })

    await userEvent.click(await screen.findByRole('button', { name: /deleted.txt/i }))

    expect(onSelectGitChange).toHaveBeenCalledWith(changes[1])
  })

  it('opens changed files from the integrated git list when no diff handler is provided', async () => {
    vi.mocked(gitApi.getGitStatus).mockResolvedValue([
      { path: 'src/app.ts', status: 'Modified', originalPath: null },
    ])
    const onSelectFile = vi.fn()
    const onOpenFile = vi.fn()

    renderTree({ onSelectFile, onOpenFile })

    await userEvent.click(await screen.findByRole('button', { name: /app.ts/i }))

    expect(onSelectFile).toHaveBeenCalledWith('src/app.ts')
    expect(onOpenFile).toHaveBeenCalledWith('src/app.ts')
  })
})
