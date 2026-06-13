import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { toast } from 'sonner'
import { listFutureTasks } from '@/api/future-tasks'
import { createPrompt } from '@/api/prompts'
import type { Prompt } from '@/api/schemas'
import { PromptForm } from './prompt-form'

const routerMocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}))

vi.mock('@tanstack/react-router', () => ({
  useNavigate: () => routerMocks.navigate,
}))

vi.mock('@/api/future-tasks', () => ({
  listFutureTasks: vi.fn(),
}))

vi.mock('@/api/prompts', () => ({
  createPrompt: vi.fn(),
  deletePrompt: vi.fn(),
  getPrompt: vi.fn(),
  updatePrompt: vi.fn(),
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    warning: vi.fn(),
    error: vi.fn(),
  },
}))

vi.mock('@/features/files/use-file-viewer', () => ({
  useFileViewer: () => ({ openFile: vi.fn() }),
}))

vi.mock('@/features/files/use-git-history', () => ({
  useGitHistory: () => ({ openHistory: vi.fn(), closeHistory: vi.fn(), target: null }),
}))

vi.mock('@/features/files/workspace-file-tree', () => ({
  WorkspaceFileTree: () => <div data-testid="workspace-file-tree" />,
}))

vi.mock('./prompt-editor', () => ({
  PromptEditor: ({
    value,
    onChange,
  }: {
    value: string
    onChange: (value: string, mentions: []) => void
  }) => (
    <textarea
      aria-label="Conteudo"
      value={value}
      onChange={(event) => onChange(event.currentTarget.value, [])}
    />
  ),
}))

const workingDirectoryId = '019e9f6a-9fb2-7f24-ac3a-bf099d2c93c0'
const createdPrompt: Prompt = {
  id: '019e9f6a-a5c7-78b8-9683-69966d7ecdbc',
  workingDirectoryId,
  parentPromptId: null,
  futureTaskId: null,
  taskNumber: null,
  title: 'Prompt copiado',
  content: 'Conteudo para copiar',
  targetAgent: 'Codex',
  kind: 'General',
  status: 'Draft',
  currentVersion: 1,
  rowVersion: '0',
  createdAtUtc: '2026-06-10T00:00:00Z',
  updatedAtUtc: '2026-06-10T00:00:00Z',
  mentions: [],
}

function stubClipboard() {
  const writeText = vi.fn().mockResolvedValue(undefined)
  Object.defineProperty(window.navigator, 'clipboard', {
    value: { writeText },
    configurable: true,
  })
  return writeText
}

function renderForm(onCreated = vi.fn()) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <PromptForm workingDirectoryId={workingDirectoryId} onCreated={onCreated} showWorkspaceFileTree={false} />
    </QueryClientProvider>,
  )
}

describe('PromptForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(listFutureTasks).mockResolvedValue([])
    vi.mocked(createPrompt).mockResolvedValue(createdPrompt)
  })

  afterEach(() => {
    cleanup()
  })

  it('creates a prompt and copies the content from the new prompt action', async () => {
    const user = userEvent.setup()
    const writeText = stubClipboard()
    const onCreated = vi.fn()

    renderForm(onCreated)

    await user.type(screen.getByLabelText('Titulo'), 'Prompt copiado')
    await user.type(screen.getByLabelText('Conteudo'), 'Conteudo para copiar')
    await user.click(screen.getByRole('button', { name: /^Salvar e copiar$/ }))

    await waitFor(() => {
      expect(createPrompt).toHaveBeenCalledWith({
        workingDirectoryId,
        futureTaskId: undefined,
        title: 'Prompt copiado',
        content: 'Conteudo para copiar',
        targetAgent: 'Codex',
        kind: 'General',
        status: 'Draft',
        mentions: [],
      })
    })
    expect(writeText).toHaveBeenCalledWith('Conteudo para copiar')
    expect(toast.success).toHaveBeenCalledWith('Prompt criado e copiado.')
    expect(onCreated).toHaveBeenCalledWith(createdPrompt)
  })
})
