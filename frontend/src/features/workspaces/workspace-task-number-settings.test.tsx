import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getWorkingDirectory, updateWorkingDirectory } from '@/api/working-directories'
import { WorkspaceTaskNumberSettings } from './workspace-task-number-settings'

vi.mock('@/api/working-directories', () => ({
  getWorkingDirectory: vi.fn(),
  updateWorkingDirectory: vi.fn(),
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

const workspace = {
  id: 'workspace-1',
  name: 'Repo',
  absolutePath: 'D:\\repo',
  respectGitignore: true,
  enableAiContext: false,
  taskNumberPattern: 'BP{N}{Date}',
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: '2026-06-01T00:00:00Z',
}

function renderSettings() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })

  render(
    <QueryClientProvider client={client}>
      <WorkspaceTaskNumberSettings workspaceId="workspace-1" />
    </QueryClientProvider>,
  )
}

describe('WorkspaceTaskNumberSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getWorkingDirectory).mockResolvedValue(workspace)
    vi.mocked(updateWorkingDirectory).mockResolvedValue(workspace)
  })

  afterEach(() => {
    cleanup()
  })

  it('updates the preview while the user edits the pattern', async () => {
    const user = userEvent.setup()
    renderSettings()

    const input = await screen.findByRole('textbox', { name: 'Padrao' })
    expect(await screen.findByText(/^Preview: BP1/)).toBeInTheDocument()

    await user.clear(input)
    await user.click(input)
    await user.paste('TASK-{N:00}-{Date:yyyyMMdd}')

    expect(await screen.findByText(/^Preview: TASK-01-\d{8}$/)).toBeInTheDocument()
  })

  it('shows a validation error for unsupported date tokens', async () => {
    const user = userEvent.setup()
    renderSettings()

    const input = await screen.findByRole('textbox', { name: 'Padrao' })
    await user.clear(input)
    await user.click(input)
    await user.paste('BP{N}{Date:MMMM}')
    await user.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByText('Use apenas dd, MM, yy e yyyy no formato de data.')).toBeInTheDocument()
    await waitFor(() => expect(updateWorkingDirectory).not.toHaveBeenCalled())
  })
})
