import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Workflow } from '@/api/schemas'
import * as workflowApi from '@/api/workflow'
import { WorkflowPanel } from './workflow-panel'

vi.mock('@/api/workflow')

const workflow: Workflow = {
  id: 'w1',
  promptId: 'p1',
  status: 'Active',
  currentPhaseId: 'ph1',
  currentPhaseName: 'Planejamento',
  currentPhaseColor: '#2563eb',
  currentActor: 'ClaudeCode',
  startedAtUtc: '2026-06-01T12:00:00Z',
  enteredCurrentPhaseAtUtc: '2026-06-01T12:00:00Z',
  currentPhaseIteration: 1,
  updatedAtUtc: '2026-06-01T12:00:00Z',
  rowVersion: '0',
  phases: [
    { id: 'ph1', name: 'Planejamento', defaultActor: 'ClaudeCode', orderIndex: 0, color: '#2563eb' },
    { id: 'ph2', name: 'Revisão do plano', defaultActor: 'Codex', orderIndex: 1, color: '#7c3aed' },
  ],
  events: [
    {
      id: 'e1',
      type: 'WorkflowStarted',
      phaseId: 'ph1',
      phaseName: 'Planejamento',
      actor: 'ClaudeCode',
      note: null,
      occurredAtUtc: '2026-06-01T12:00:00Z',
    },
  ],
}

function renderPanel() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const result = render(
    <QueryClientProvider client={client}>
      <WorkflowPanel promptId="p1" onNavigateTab={() => {}} />
    </QueryClientProvider>,
  )
  return within(result.container)
}

describe('WorkflowPanel', () => {
  beforeEach(() => {
    vi.mocked(workflowApi.getWorkflow).mockResolvedValue(workflow)
    vi.mocked(workflowApi.getWorkflowTemplate).mockResolvedValue({ id: 't1', name: 'Fluxo padrão', phases: workflow.phases })
    vi.mocked(workflowApi.advancePhase).mockResolvedValue({
      ...workflow,
      currentPhaseId: 'ph2',
      currentPhaseName: 'Revisão do plano',
      currentActor: 'Codex',
      rowVersion: '1',
    })
    vi.mocked(workflowApi.addWorkflowNote).mockResolvedValue(workflow)
    vi.mocked(workflowApi.completeWorkflow).mockResolvedValue({ ...workflow, status: 'Done', rowVersion: '1' })
  })

  it('advances the phase when "Avançar" is clicked', async () => {
    const view = renderPanel()
    const advanceButton = await view.findByRole('button', { name: 'Avançar' })

    fireEvent.click(advanceButton)

    await waitFor(() => expect(vi.mocked(workflowApi.advancePhase)).toHaveBeenCalledWith('p1', '0'))
  })

  it('saves a note', async () => {
    const view = renderPanel()
    const noteField = await view.findByPlaceholderText('Ex.: cole aqui o feedback do Codex')

    fireEvent.change(noteField, { target: { value: 'feedback do codex' } })
    fireEvent.click(view.getByRole('button', { name: 'Salvar nota' }))

    await waitFor(() => expect(vi.mocked(workflowApi.addWorkflowNote)).toHaveBeenCalledWith('p1', 'feedback do codex'))
  })

  it('completes the workflow', async () => {
    const view = renderPanel()
    const completeButton = await view.findByRole('button', { name: 'Concluir' })

    fireEvent.click(completeButton)

    await waitFor(() => expect(vi.mocked(workflowApi.completeWorkflow)).toHaveBeenCalledWith('p1', '0'))
  })
})
