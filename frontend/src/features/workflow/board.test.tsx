import { describe, expect, it } from 'vitest'
import type { TaskSummary } from '@/api/schemas'
import { buildColumns } from './board-columns'

const templatePhases = [
  { name: 'Planejamento', orderIndex: 0 },
  { name: 'Revisao do plano', orderIndex: 1 },
  { name: 'Implementacao', orderIndex: 2 },
]

let counter = 0
function makeTask(partial: Partial<TaskSummary>): TaskSummary {
  counter += 1
  return {
    promptId: `prompt-${counter}`,
    workingDirectoryId: `wd-${counter}`,
    workingDirectoryName: 'repo',
    taskNumber: null,
    title: `Tarefa ${counter}`,
    promptStatus: 'Draft',
    workflowStatus: 'Active',
    currentPhaseId: `phase-${counter}`,
    currentPhaseName: 'Planejamento',
    currentPhaseColor: '#2563eb',
    currentActor: 'ClaudeCode',
    enteredCurrentPhaseAtUtc: '2026-06-01T12:00:00Z',
    currentPhaseIteration: 1,
    updatedAtUtc: '2026-06-01T12:00:00Z',
    hasChildPrompts: false,
    hasLinkedPlan: false,
    promptRowVersion: '0',
    phases: [
      { id: 'phase-planning', name: 'Planejamento', defaultActor: 'ClaudeCode', orderIndex: 0, color: '#2563eb' },
      { id: 'phase-review', name: 'Revisao do plano', defaultActor: 'Codex', orderIndex: 1, color: '#7c3aed' },
      { id: 'phase-implementation', name: 'Implementacao', defaultActor: 'Codex', orderIndex: 2, color: '#0d9488' },
    ],
    rowVersion: '0',
    ...partial,
  }
}

describe('buildColumns', () => {
  it('groups tasks into "Sem fluxo", the template phases and "Concluídas"', () => {
    const tasks = [
      makeTask({ currentPhaseName: 'Planejamento' }),
      makeTask({ currentPhaseName: 'Revisao do plano' }),
      makeTask({
        workflowStatus: null,
        currentPhaseId: null,
        currentPhaseName: null,
        currentActor: null,
        enteredCurrentPhaseAtUtc: null,
        phases: [],
        rowVersion: null,
      }),
      makeTask({ workflowStatus: 'Done', currentPhaseName: 'Commit/Merge' }),
    ]

    const columns = buildColumns(tasks, templatePhases)
    const titles = columns.map((column) => column.title)

    expect(titles[0]).toBe('Sem fluxo')
    expect(titles).toContain('Implementacao')
    expect(titles[titles.length - 1]).toBe('Concluídas')
    expect(columns.find((column) => column.title === 'Planejamento')?.tasks).toHaveLength(1)
    expect(columns.find((column) => column.title === 'Implementacao')?.tasks).toHaveLength(0)
    expect(columns.find((column) => column.title === 'Concluídas')?.tasks).toHaveLength(1)
    expect(columns.find((column) => column.title === 'Planejamento')?.droppable).toBe(true)
    expect(columns.find((column) => column.title === 'Sem fluxo')?.droppable).toBe(false)
  })

  it('places active tasks with a non-template phase under its own column', () => {
    const columns = buildColumns([makeTask({ currentPhaseName: 'Fase custom' })], templatePhases)
    expect(columns.find((column) => column.title === 'Fase custom')?.tasks).toHaveLength(1)
  })

  it('omits the optional no-workflow column when there are no matching tasks', () => {
    const columns = buildColumns([makeTask({ currentPhaseName: 'Planejamento' })], templatePhases)
    expect(columns.map((column) => column.title)).not.toContain('Sem fluxo')
    expect(columns.map((column) => column.title)).toContain('Concluídas')
  })
})
