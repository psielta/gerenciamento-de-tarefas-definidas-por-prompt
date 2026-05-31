import type { TaskSummary, WorkflowPhase } from '@/api/schemas'

export type BoardColumnKind = 'no-workflow' | 'phase' | 'done'

export type BoardColumn = {
  id: string
  title: string
  kind: BoardColumnKind
  tasks: TaskSummary[]
  phaseName?: string
  phaseOrderIndex?: number
  droppable: boolean
}

/**
 * Groups board tasks into "Sem fluxo", one column per template phase (in order),
 * any non-template phases that still have active tasks, and a trailing "Concluídas".
 */
export function buildColumns(tasks: TaskSummary[], templatePhases: Pick<WorkflowPhase, 'name' | 'orderIndex'>[]): BoardColumn[] {
  const noWorkflow = tasks.filter((task) => task.workflowStatus === null)
  const done = tasks.filter((task) => task.workflowStatus === 'Done')
  const active = tasks.filter((task) => task.workflowStatus === 'Active')

  const activeByPhase = new Map<string, TaskSummary[]>()
  for (const task of active) {
    const key = task.currentPhaseName ?? 'Outras fases'
    const list = activeByPhase.get(key) ?? []
    list.push(task)
    activeByPhase.set(key, list)
  }

  const columns: BoardColumn[] = []
  if (noWorkflow.length > 0) {
    columns.push({
      id: 'no-workflow',
      title: 'Sem fluxo',
      kind: 'no-workflow',
      tasks: noWorkflow,
      droppable: false,
    })
  }

  const orderedTemplatePhases = [...templatePhases].sort((a, b) => a.orderIndex - b.orderIndex)
  for (const phase of orderedTemplatePhases) {
    columns.push({
      id: `phase:${phase.orderIndex}:${phase.name}`,
      title: phase.name,
      kind: 'phase',
      tasks: activeByPhase.get(phase.name) ?? [],
      phaseName: phase.name,
      phaseOrderIndex: phase.orderIndex,
      droppable: true,
    })
  }

  const templatePhaseNames = orderedTemplatePhases.map((phase) => phase.name)
  for (const [name, list] of activeByPhase) {
    if (!templatePhaseNames.includes(name)) {
      columns.push({
        id: `phase:custom:${name}`,
        title: name,
        kind: 'phase',
        tasks: list,
        phaseName: name,
        droppable: true,
      })
    }
  }

  columns.push({
    id: 'done',
    title: 'Concluídas',
    kind: 'done',
    tasks: done,
    droppable: true,
  })

  return columns
}
