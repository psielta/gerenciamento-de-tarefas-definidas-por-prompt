import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { listPromptTemplates } from '@/api/prompt-templates'
import type { PromptTemplate, TaskSummary, Workflow, WorkflowPhase } from '@/api/schemas'
import * as workflowApi from '@/api/workflow'
import { TaskCard } from './task-card'

vi.mock('@tanstack/react-router', () => ({
  Link: ({
    to,
    params,
    children,
    className,
  }: {
    to: string
    params?: Record<string, string>
    children: ReactNode
    className?: string
  }) => {
    const href = Object.entries(params ?? {}).reduce(
      (current, [key, value]) => current.replace(`$${key}`, value),
      to,
    )

    return <a href={href} className={className}>{children}</a>
  },
}))

vi.mock('@/api/prompt-templates', () => ({
  listPromptTemplates: vi.fn().mockResolvedValue([]),
}))

vi.mock('@/api/workflow')

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(listPromptTemplates).mockResolvedValue([])
})

function makeTemplate(key: string, displayName: string): PromptTemplate {
  return {
    key,
    displayName,
    description: 'desc',
    defaultTargetAgent: 'Codex',
    defaultKind: key === 'ReReviewPlan' ? 'Planning' : 'General',
    input: null,
    inputs: [],
  }
}

function makeWorkflow(phases: WorkflowPhase[], currentPhaseId: string, rowVersion = '7'): Workflow {
  const current = phases.find((phase) => phase.id === currentPhaseId)
  return {
    id: 'workflow-1',
    promptId: 'prompt-1',
    status: 'Active',
    currentPhaseId,
    currentPhaseName: current?.name ?? null,
    currentPhaseColor: current?.color ?? null,
    currentActor: current?.defaultActor ?? null,
    startedAtUtc: '2026-06-01T12:00:00Z',
    enteredCurrentPhaseAtUtc: '2026-06-01T12:00:00Z',
    currentPhaseIteration: 1,
    reviewVerdictSourcePhaseName: null,
    updatedAtUtc: '2026-06-01T12:00:00Z',
    rowVersion,
    phases,
    events: [],
  }
}

function renderTask(task: TaskSummary, onGenerate?: (task: TaskSummary, template: PromptTemplate) => void) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <TaskCard task={task} onGenerate={onGenerate} />
    </QueryClientProvider>,
  )
}

function makeTask(taskNumber: string | null, currentPhaseIteration = 1): TaskSummary {
  return {
    promptId: 'prompt-1',
    workingDirectoryId: 'workspace-1',
    workingDirectoryName: 'repo',
    taskNumber,
    title: 'Implement numbering',
    promptStatus: 'Draft',
    workflowStatus: 'Active',
    currentPhaseId: 'phase-1',
    currentPhaseName: 'Planejamento',
    currentPhaseColor: '#2563eb',
    currentActor: 'ClaudeCode',
    enteredCurrentPhaseAtUtc: '2026-06-01T12:00:00Z',
    currentPhaseIteration,
    reviewVerdictSourcePhaseName: null,
    updatedAtUtc: '2026-06-01T12:00:00Z',
    hasChildPrompts: false,
    hasLinkedPlan: false,
    linkedDocumentId: null,
    pullRequestReference: null,
    promptRowVersion: '0',
    phases: [
      { id: 'phase-1', name: 'Planejamento', defaultActor: 'ClaudeCode', orderIndex: 0, color: '#2563eb', role: 'Planning' },
      { id: 'phase-2', name: 'Implementacao', defaultActor: 'Codex', orderIndex: 1, color: '#0d9488', role: 'Implementation' },
    ],
    rowVersion: '0',
  }
}

function renderCard(taskNumber: string | null, currentPhaseIteration = 1) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })

  render(
    <QueryClientProvider client={client}>
      <TaskCard task={makeTask(taskNumber, currentPhaseIteration)} />
    </QueryClientProvider>,
  )
}

describe('TaskCard', () => {
  afterEach(() => {
    cleanup()
  })

  it('shows the task number badge and opens the detail drawer on click', () => {
    const onOpen = vi.fn()
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <TaskCard task={makeTask('BP001010626')} onOpen={onOpen} />
      </QueryClientProvider>,
    )

    expect(screen.getByText('BP001010626')).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /abrir detalhes do prompt/i }))
    expect(onOpen).toHaveBeenCalledTimes(1)
  })

  it('hides the task number badge when absent', () => {
    renderCard(null)

    expect(screen.queryByText('BP001010626')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /abrir detalhes do prompt/i })).toBeInTheDocument()
  })

  it('shows a re-review badge only after the first phase iteration', () => {
    renderCard(null, 2)

    expect(screen.getByText('re-review #2')).toBeInTheDocument()

    cleanup()
    renderCard(null, 1)
    expect(screen.queryByText('re-review #1')).not.toBeInTheDocument()
  })

  it('shows the generate-child dropdown only when the card has a linked plan', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
    const task: TaskSummary = { ...makeTask(null), linkedDocumentId: 'doc-1', hasLinkedPlan: true }

    render(
      <QueryClientProvider client={client}>
        <TaskCard task={task} />
      </QueryClientProvider>,
    )

    expect(screen.getByRole('button', { name: /gerar prompt filho/i })).toBeInTheDocument()
  })

  it('hides the generate-child dropdown without a linked plan', () => {
    renderCard(null)

    expect(screen.queryByRole('button', { name: /gerar prompt filho/i })).not.toBeInTheDocument()
  })

  it('shows a link-plan button and calls onLinkPlan when there is no linked plan', () => {
    const onLinkPlan = vi.fn()
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <TaskCard task={makeTask(null)} onLinkPlan={onLinkPlan} />
      </QueryClientProvider>,
    )

    fireEvent.click(screen.getByRole('button', { name: /vincular plano/i }))
    expect(onLinkPlan).toHaveBeenCalledTimes(1)
  })

  it('shows the review-verdict button only when the current phase is a review phase', () => {
    renderCard(null)
    expect(screen.queryByRole('button', { name: /adicionar nota de revisão/i })).not.toBeInTheDocument()

    cleanup()

    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
    const reviewTask: TaskSummary = {
      ...makeTask(null),
      currentPhaseId: 'phase-review',
      currentPhaseName: 'Revisão do plano',
      phases: [
        { id: 'phase-review', name: 'Revisão do plano', defaultActor: 'Codex', orderIndex: 0, color: '#7c3aed', role: 'PlanReview' },
      ],
    }
    render(
      <QueryClientProvider client={client}>
        <TaskCard task={reviewTask} />
      </QueryClientProvider>,
    )
    expect(screen.getByRole('button', { name: /adicionar nota de revisão/i })).toBeInTheDocument()
  })

  it('shows the verdict source badge and hides the review button outside review phases', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
    const task: TaskSummary = {
      ...makeTask(null),
      reviewVerdictSourcePhaseName: 'Revisão de código',
    }
    render(
      <QueryClientProvider client={client}>
        <TaskCard task={task} />
      </QueryClientProvider>,
    )

    expect(screen.getByText(/Revisão de código/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /adicionar nota de revisão/i })).not.toBeInTheDocument()
  })

  const PLAN_REVIEW_PHASES: WorkflowPhase[] = [
    { id: 'phase-review', name: 'Revisão do plano', defaultActor: 'Codex', orderIndex: 0, color: '#7c3aed', role: 'PlanReview' },
    { id: 'phase-plan-correction', name: 'Correção do plano', defaultActor: 'ClaudeCode', orderIndex: 1, color: '#d97706', role: 'PlanCorrection' },
    { id: 'phase-impl', name: 'Implementação', defaultActor: 'Codex', orderIndex: 2, color: '#0d9488', role: 'Implementation' },
  ]
  const PLANNING_PHASES: WorkflowPhase[] = [
    { id: 'phase-planning', name: 'Planejamento', defaultActor: 'ClaudeCode', orderIndex: 0, color: '#2563eb', role: 'Planning' },
    { id: 'phase-review', name: 'Revisão do plano', defaultActor: 'Codex', orderIndex: 1, color: '#7c3aed', role: 'PlanReview' },
    { id: 'phase-impl', name: 'Implementação', defaultActor: 'Codex', orderIndex: 2, color: '#0d9488', role: 'Implementation' },
  ]
  const CODE_REVIEW_PHASES: WorkflowPhase[] = [
    { id: 'phase-code-review', name: 'Revisão de código', defaultActor: 'Codex', orderIndex: 0, color: '#7c3aed', role: 'CodeReview' },
    { id: 'phase-review-correction', name: 'Correção da revisão', defaultActor: 'ClaudeCode', orderIndex: 1, color: '#d97706', role: 'ReviewCorrection' },
    { id: 'phase-practical', name: 'Teste prático', defaultActor: 'Human', orderIndex: 2, color: '#16a34a', role: 'PracticalTest' },
  ]

  it('opens the ReReviewPlan child-prompt drawer from PlanCorrection', async () => {
    vi.mocked(listPromptTemplates).mockResolvedValue([makeTemplate('ReReviewPlan', 'Re-review do plano')])
    const onGenerate = vi.fn()
    renderTask(
      {
        ...makeTask(null),
        linkedDocumentId: 'doc-1',
        hasLinkedPlan: true,
        currentPhaseId: 'phase-plan-correction',
        currentPhaseName: 'Correção do plano',
        phases: PLAN_REVIEW_PHASES,
      },
      onGenerate,
    )

    fireEvent.click(await screen.findByRole('button', { name: /re-review do plano/i }))
    expect(onGenerate).toHaveBeenCalledWith(
      expect.objectContaining({ promptId: 'prompt-1' }),
      expect.objectContaining({ key: 'ReReviewPlan' }),
    )
  })

  it('opens the ReReviewPullRequest child-prompt drawer from ReviewCorrection', async () => {
    vi.mocked(listPromptTemplates).mockResolvedValue([makeTemplate('ReReviewPullRequest', 'Re-review de PR')])
    const onGenerate = vi.fn()
    renderTask(
      {
        ...makeTask(null),
        linkedDocumentId: 'doc-1',
        hasLinkedPlan: true,
        pullRequestReference: '#42',
        currentPhaseId: 'phase-review-correction',
        currentPhaseName: 'Correção da revisão',
        phases: CODE_REVIEW_PHASES,
      },
      onGenerate,
    )

    fireEvent.click(await screen.findByRole('button', { name: /re-review de pr/i }))
    expect(onGenerate).toHaveBeenCalledWith(
      expect.objectContaining({ promptId: 'prompt-1' }),
      expect.objectContaining({ key: 'ReReviewPullRequest' }),
    )
  })

  it('hides the re-review button when the correction task has no linked plan', () => {
    vi.mocked(listPromptTemplates).mockResolvedValue([makeTemplate('ReReviewPlan', 'Re-review do plano')])
    renderTask({
      ...makeTask(null),
      linkedDocumentId: null,
      currentPhaseId: 'phase-plan-correction',
      currentPhaseName: 'Correção do plano',
      phases: PLAN_REVIEW_PHASES,
    })

    expect(screen.queryByRole('button', { name: /re-review do plano/i })).not.toBeInTheDocument()
  })

  it('opens the selected plan-review child-prompt template from Planning', async () => {
    vi.mocked(listPromptTemplates).mockResolvedValue([
      makeTemplate('ReviewPlan', 'Basic plan review'),
      makeTemplate('ReviewPlanWithParentPrompt', 'Plan review with parent prompt'),
    ])
    const onGenerate = vi.fn()
    renderTask(
      {
        ...makeTask(null),
        linkedDocumentId: 'doc-1',
        hasLinkedPlan: true,
        currentPhaseId: 'phase-planning',
        currentPhaseName: 'Planning',
        phases: PLANNING_PHASES,
      },
      onGenerate,
    )

    const advanceButton = screen.getByRole('button', { name: /revis/i })
    await waitFor(() => expect(advanceButton).not.toBeDisabled())
    fireEvent.click(advanceButton)
    expect(screen.getByRole('dialog', { name: /escolher revis/i })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /with parent prompt/i }))

    expect(onGenerate).toHaveBeenCalledWith(
      expect.objectContaining({ promptId: 'prompt-1' }),
      expect.objectContaining({ key: 'ReviewPlanWithParentPrompt' }),
    )
    expect(vi.mocked(workflowApi.setPhase)).not.toHaveBeenCalled()
    expect(vi.mocked(workflowApi.advancePhase)).not.toHaveBeenCalled()
  })

  it('asks for the implementation template before advancing from PlanReview', async () => {
    vi.mocked(listPromptTemplates).mockResolvedValue([
      makeTemplate('ImplementPlan', 'Implementar plano'),
      makeTemplate('ImplementPlanInWorktree', 'Implementar em worktree'),
    ])
    renderTask({
      ...makeTask(null),
      linkedDocumentId: 'doc-1',
      hasLinkedPlan: true,
      currentPhaseId: 'phase-review',
      currentPhaseName: 'Revisão do plano',
      phases: PLAN_REVIEW_PHASES,
    })

    await waitFor(() => expect(screen.getByRole('button', { name: /implementa/i })).not.toBeDisabled())
    fireEvent.click(screen.getByRole('button', { name: /implementa/i }))
    expect(screen.getByRole('dialog', { name: /escolher implementa/i })).toBeInTheDocument()
    expect(vi.mocked(workflowApi.setPhase)).not.toHaveBeenCalled()
    expect(vi.mocked(workflowApi.advancePhase)).not.toHaveBeenCalled()
  })

  it('opens the selected implementation child-prompt template from PlanReview', async () => {
    vi.mocked(listPromptTemplates).mockResolvedValue([
      makeTemplate('ImplementPlan', 'Basic implementation'),
      makeTemplate('ImplementPlanInWorktree', 'Worktree implementation'),
    ])
    const onGenerate = vi.fn()
    renderTask(
      {
        ...makeTask(null),
        linkedDocumentId: 'doc-1',
        hasLinkedPlan: true,
        currentPhaseId: 'phase-review',
        currentPhaseName: 'Plan review',
        phases: PLAN_REVIEW_PHASES,
      },
      onGenerate,
    )

    const advanceButton = screen.getByRole('button', { name: /implementa/i })
    await waitFor(() => expect(advanceButton).not.toBeDisabled())
    fireEvent.click(advanceButton)
    fireEvent.click(screen.getByRole('button', { name: /worktree implementation/i }))

    expect(onGenerate).toHaveBeenCalledWith(
      expect.objectContaining({ promptId: 'prompt-1' }),
      expect.objectContaining({ key: 'ImplementPlanInWorktree' }),
    )
    expect(vi.mocked(workflowApi.setPhase)).not.toHaveBeenCalled()
    expect(vi.mocked(workflowApi.advancePhase)).not.toHaveBeenCalled()
  })

  it('jumps CodeReview straight to the PracticalTest phase', async () => {
    const workflow = makeWorkflow(CODE_REVIEW_PHASES, 'phase-code-review')
    vi.mocked(workflowApi.getWorkflow).mockResolvedValue(workflow)
    vi.mocked(workflowApi.setPhase).mockResolvedValue({ ...workflow, currentPhaseId: 'phase-practical' })
    renderTask({
      ...makeTask(null),
      currentPhaseId: 'phase-code-review',
      currentPhaseName: 'Revisão de código',
      phases: CODE_REVIEW_PHASES,
    })

    fireEvent.click(screen.getByRole('button', { name: /avançar para teste prático/i }))

    await waitFor(() => expect(vi.mocked(workflowApi.setPhase)).toHaveBeenCalledWith('prompt-1', 'phase-practical', '7'))
    expect(vi.mocked(workflowApi.advancePhase)).not.toHaveBeenCalled()
  })

  it('hides the approval-advance button when the target role phase is absent', () => {
    renderTask({
      ...makeTask(null),
      linkedDocumentId: 'doc-1',
      hasLinkedPlan: true,
      currentPhaseId: 'phase-review',
      currentPhaseName: 'Revisão do plano',
      phases: [
        { id: 'phase-review', name: 'Revisão do plano', defaultActor: 'Codex', orderIndex: 0, color: '#7c3aed', role: 'PlanReview' },
        { id: 'phase-plan-correction', name: 'Correção do plano', defaultActor: 'ClaudeCode', orderIndex: 1, color: '#d97706', role: 'PlanCorrection' },
      ],
    })

    expect(screen.queryByRole('button', { name: /avançar para implementação/i })).not.toBeInTheDocument()
  })

  it('hides the approval-advance button when the workflow is not active', () => {
    renderTask({
      ...makeTask(null),
      workflowStatus: 'Done',
      linkedDocumentId: 'doc-1',
      hasLinkedPlan: true,
      currentPhaseId: 'phase-review',
      currentPhaseName: 'Revisão do plano',
      phases: PLAN_REVIEW_PHASES,
    })

    expect(screen.queryByRole('button', { name: /avançar para implementação/i })).not.toBeInTheDocument()
  })
})
