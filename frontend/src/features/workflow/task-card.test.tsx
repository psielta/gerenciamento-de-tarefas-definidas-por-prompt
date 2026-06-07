import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { TaskSummary } from '@/api/schemas'
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
    updatedAtUtc: '2026-06-01T12:00:00Z',
    hasChildPrompts: false,
    hasLinkedPlan: false,
    linkedDocumentId: null,
    pullRequestReference: null,
    promptRowVersion: '0',
    phases: [
      { id: 'phase-1', name: 'Planejamento', defaultActor: 'ClaudeCode', orderIndex: 0, color: '#2563eb' },
      { id: 'phase-2', name: 'Implementacao', defaultActor: 'Codex', orderIndex: 1, color: '#0d9488' },
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
})
