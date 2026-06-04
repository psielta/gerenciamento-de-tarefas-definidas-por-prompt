import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, within } from '@testing-library/react'
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

  it('shows the task number badge and links to the task-number route when present', () => {
    renderCard('BP001010626')

    const link = screen.getByRole('link')
    expect(within(link).getByText('BP001010626')).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/workspaces/workspace-1/tasks/BP001010626')
  })

  it('hides the badge and keeps the prompt-id route when task number is absent', () => {
    renderCard(null)

    expect(screen.queryByText('BP001010626')).not.toBeInTheDocument()
    expect(screen.getByRole('link')).toHaveAttribute('href', '/workspaces/workspace-1/prompts/prompt-1')
  })

  it('shows a re-review badge only after the first phase iteration', () => {
    renderCard(null, 2)

    expect(screen.getByText('re-review #2')).toBeInTheDocument()

    cleanup()
    renderCard(null, 1)
    expect(screen.queryByText('re-review #1')).not.toBeInTheDocument()
  })
})
