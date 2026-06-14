import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createTerminal } from '@/api/terminals'
import type { Prompt, TerminalSession } from '@/api/schemas'
import { CreateAgentTerminalDialog } from './create-agent-terminal-dialog'

vi.mock('@/api/terminals', () => ({
  createTerminal: vi.fn(),
}))

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}))

const prompt: Prompt = {
  id: '019e9f6a-a5c7-78b8-9683-69966d7ecdbc',
  workingDirectoryId: '019e9f6a-9fb2-7f24-ac3a-bf099d2c93c0',
  parentPromptId: '019e9f6a-a269-7991-95d5-4e602dcf773d',
  futureTaskId: null,
  taskNumber: null,
  title: 'Revisar PR #42',
  content: '/review a PR',
  targetAgent: 'Codex',
  kind: 'General',
  status: 'Draft',
  currentVersion: 1,
  rowVersion: '0',
  createdAtUtc: '2026-05-31T00:00:00Z',
  updatedAtUtc: '2026-05-31T00:00:00Z',
  mentions: [],
}

const session: TerminalSession = {
  id: '019e9f6a-b111-7000-9000-000000000001',
  promptId: prompt.id,
  shell: 'pwsh.exe',
  cwd: 'C:/repo',
  createdAtUtc: '2026-05-31T00:00:00Z',
}

function renderDialog(promptOverride: Prompt = prompt) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })
  const onCancel = vi.fn()
  const onCreated = vi.fn()

  render(
    <QueryClientProvider client={queryClient}>
      <CreateAgentTerminalDialog prompt={promptOverride} onCancel={onCancel} onCreated={onCreated} />
    </QueryClientProvider>,
  )

  return { onCancel, onCreated }
}

describe('CreateAgentTerminalDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(createTerminal).mockResolvedValue(session)
  })

  afterEach(() => {
    cleanup()
  })

  it('defaults the agent to the child target agent', () => {
    renderDialog()
    expect(screen.getByLabelText('Agente')).toHaveValue('Codex')
  })

  it('maps the ClaudeCode target agent to the Claude launcher', () => {
    renderDialog({ ...prompt, targetAgent: 'ClaudeCode' })
    expect(screen.getByLabelText('Agente')).toHaveValue('Claude')
  })

  it('creates a terminal submitting the prompt and notifies on success', async () => {
    const user = userEvent.setup()
    const { onCreated } = renderDialog()

    await user.click(screen.getByRole('button', { name: /Criar e abrir/ }))

    await waitFor(() => {
      expect(createTerminal).toHaveBeenCalledWith(prompt.id, { agentLaunch: 'Codex', submitPrompt: true })
    })
    await waitFor(() => {
      expect(onCreated).toHaveBeenCalledWith(prompt.id, session)
    })
  })

  it('cancels without creating a terminal', async () => {
    const user = userEvent.setup()
    const { onCancel } = renderDialog()

    await user.click(screen.getByRole('button', { name: /Agora nao/ }))

    expect(onCancel).toHaveBeenCalled()
    expect(createTerminal).not.toHaveBeenCalled()
  })
})
