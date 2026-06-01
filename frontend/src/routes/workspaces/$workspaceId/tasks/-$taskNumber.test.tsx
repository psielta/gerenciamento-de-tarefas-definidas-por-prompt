import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createMemoryHistory, createRouter } from '@tanstack/react-router'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getPromptByTaskNumber } from '@/api/prompts'
import type { Prompt } from '@/api/schemas'
import { getWorkingDirectory } from '@/api/working-directories'
import { routeTree } from '@/routeTree.gen'
import { PromptDetailView } from '@/features/prompts/prompt-detail'

vi.mock('@/api/prompts', () => ({
  getPromptByTaskNumber: vi.fn(),
}))

vi.mock('@/api/working-directories', () => ({
  getWorkingDirectory: vi.fn(),
  updateWorkingDirectory: vi.fn(),
}))

vi.mock('@/features/prompts/prompt-detail', () => ({
  PromptDetailView: vi.fn(({ workspaceId, promptId, activeTab }) => (
    <div data-testid="prompt-detail-view">
      {workspaceId}:{promptId}:{activeTab}
    </div>
  )),
}))

vi.mock('@/features/agent-usage/usage-indicator', () => ({
  UsageIndicator: () => null,
}))

vi.mock('@/components/theme/theme-toggle', () => ({
  ThemeToggle: () => null,
}))

vi.mock('@/realtime/prompt-hub', () => ({
  usePromptHub: () => ({
    connected: true,
    joinWorkingDirectory: vi.fn(),
    leaveWorkingDirectory: vi.fn(),
    joinTasks: vi.fn(),
    leaveTasks: vi.fn(),
  }),
}))

const prompt: Prompt = {
  id: 'prompt-1',
  workingDirectoryId: 'workspace-1',
  parentPromptId: null,
  taskNumber: 'BP001010626',
  title: 'Numbered task',
  content: 'Task content',
  targetAgent: 'Codex',
  kind: 'General',
  status: 'Draft',
  currentVersion: 1,
  rowVersion: '0',
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: '2026-06-01T00:00:00Z',
  mentions: [],
}

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

function renderTaskNumberRoute(initialEntry: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const history = createMemoryHistory({ initialEntries: [initialEntry] })
  const router = createRouter({
    routeTree,
    history,
    defaultPreload: 'intent',
    scrollRestoration: false,
  })

  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  return router
}

describe('/workspaces/$workspaceId/tasks/$taskNumber', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getPromptByTaskNumber).mockResolvedValue(prompt)
    vi.mocked(getWorkingDirectory).mockResolvedValue(workspace)
  })

  afterEach(() => {
    cleanup()
  })

  it('renders the prompt detail view while preserving the task-number URL', async () => {
    const router = renderTaskNumberRoute('/workspaces/workspace-1/tasks/BP001010626')

    expect(await screen.findByTestId('prompt-detail-view')).toHaveTextContent('workspace-1:prompt-1:prompt')
    expect(getPromptByTaskNumber).toHaveBeenCalledWith('workspace-1', 'BP001010626')
    expect(PromptDetailView).toHaveBeenCalledWith(
      expect.objectContaining({
        workspaceId: 'workspace-1',
        promptId: 'prompt-1',
        activeTab: 'prompt',
      }),
      undefined,
    )
    await waitFor(() => expect(router.state.location.pathname).toBe('/workspaces/workspace-1/tasks/BP001010626'))
  })
})
