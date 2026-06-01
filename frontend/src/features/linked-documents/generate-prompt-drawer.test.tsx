import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPrompt } from '@/api/prompts'
import { renderPromptDraft } from '@/api/prompt-templates'
import type { PromptTemplate } from '@/api/schemas'
import { GeneratePromptDrawer } from './generate-prompt-drawer'

vi.mock('@/api/prompt-templates', () => ({
  renderPromptDraft: vi.fn(),
}))

vi.mock('@/api/prompts', () => ({
  createPrompt: vi.fn(),
}))

vi.mock('@tanstack/react-router', () => ({
  useNavigate: () => vi.fn(),
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

const template: PromptTemplate = {
  key: 'ReviewPlan',
  displayName: 'Revisar plano',
  description: 'Valida o plano',
  defaultTargetAgent: 'Codex',
  defaultKind: 'Planning',
  input: null,
}
const prTemplate: PromptTemplate = {
  key: 'ReviewPullRequest',
  displayName: 'Revisar PR',
  description: 'Valida a PR',
  defaultTargetAgent: 'Codex',
  defaultKind: 'General',
  input: {
    key: 'pullRequest',
    label: 'PR',
    placeholder: '#123 ou URL da PR',
    helpText: 'Informe o numero ou link da PR.',
    required: true,
  },
}
const draftContent =
  'Dado o plano "C:\\Users\\psiel\\.claude\\plans\\plan.md", valide o plano, aprove-o ou aponte melhorias.'

function renderDrawer(templateOverride = template) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <GeneratePromptDrawer
        linkedDocumentId="019e9f6a-94e7-7a23-965d-c8b05c63ee59"
        template={templateOverride}
        onClose={vi.fn()}
      />
    </QueryClientProvider>,
  )
}

describe('GeneratePromptDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(renderPromptDraft).mockResolvedValue({
      templateKey: 'ReviewPlan',
      linkedDocumentId: '019e9f6a-94e7-7a23-965d-c8b05c63ee59',
      workingDirectoryId: '019e9f6a-9fb2-7f24-ac3a-bf099d2c93c0',
      parentPromptId: '019e9f6a-a269-7991-95d5-4e602dcf773d',
      title: 'Revisar plano: plan.md',
      content: draftContent,
      targetAgent: 'Codex',
      kind: 'Planning',
    })
    vi.mocked(createPrompt).mockResolvedValue({
      id: '019e9f6a-a5c7-78b8-9683-69966d7ecdbc',
      workingDirectoryId: '019e9f6a-9fb2-7f24-ac3a-bf099d2c93c0',
      parentPromptId: '019e9f6a-a269-7991-95d5-4e602dcf773d',
      title: 'Prompt revisado',
      content: draftContent,
      targetAgent: 'Codex',
      kind: 'Planning',
      status: 'Draft',
      currentVersion: 1,
      rowVersion: '0',
      createdAtUtc: '2026-05-31T00:00:00Z',
      updatedAtUtc: '2026-05-31T00:00:00Z',
      mentions: [],
    })
  })

  afterEach(() => {
    cleanup()
  })

  it('loads a draft, allows editing, and creates a persisted prompt', async () => {
    const user = userEvent.setup()
    renderDrawer()

    const titleInput = await screen.findByDisplayValue('Revisar plano: plan.md')
    await user.clear(titleInput)
    await user.type(titleInput, 'Prompt revisado')

    expect(screen.getByLabelText('Agente')).toHaveValue('Codex')
    expect(screen.getByLabelText('Tipo')).toHaveValue('Planning')

    await user.click(screen.getByRole('button', { name: /^Criar filho$/ }))

    await waitFor(() => {
      expect(createPrompt).toHaveBeenCalledWith({
        workingDirectoryId: '019e9f6a-9fb2-7f24-ac3a-bf099d2c93c0',
        parentPromptId: '019e9f6a-a269-7991-95d5-4e602dcf773d',
        title: 'Prompt revisado',
        content: draftContent,
        targetAgent: 'Codex',
        kind: 'Planning',
        status: 'Draft',
        mentions: [],
      })
    })
  })

  it('shows the create and copy action', async () => {
    renderDrawer()

    await screen.findByDisplayValue('Revisar plano: plan.md')
    expect(screen.getByRole('button', { name: /^Criar e copiar$/ })).toBeInTheDocument()
  })

  it('asks for the PR before rendering a pull request review draft', async () => {
    const user = userEvent.setup()
    renderDrawer(prTemplate)

    expect(renderPromptDraft).not.toHaveBeenCalled()
    await user.type(screen.getByLabelText('PR'), '42')
    await user.click(screen.getByRole('button', { name: /^Gerar$/ }))

    await waitFor(() => {
      expect(renderPromptDraft).toHaveBeenCalledWith(
        '019e9f6a-94e7-7a23-965d-c8b05c63ee59',
        'ReviewPullRequest',
        { pullRequest: '42' },
      )
    })
  })
})
