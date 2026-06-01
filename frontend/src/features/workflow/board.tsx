import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ChevronLeft, ChevronRight, Columns3, Loader2, Rows3, Search, Settings2, SlidersHorizontal, X } from 'lucide-react'
import type { DragEvent } from 'react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { queryKeys } from '@/api/query-keys'
import type { PromptStatus, PromptWorkflowStatus, TaskSummary, Workflow } from '@/api/schemas'
import { listWorkingDirectories } from '@/api/working-directories'
import { completeWorkflow, getBoard, getWorkflow, getWorkflowTemplate, reopenWorkflow, setPhase, startWorkflow } from '@/api/workflow'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { usePromptHub } from '@/realtime/prompt-hub'
import { buildColumns, type BoardColumn } from './board-columns'
import { TaskCard } from './task-card'

const PROMPT_STATUS_OPTIONS: Array<{ value: PromptStatus | ''; label: string }> = [
  { value: '', label: 'Não arquivadas' },
  { value: 'Draft', label: 'Rascunho' },
  { value: 'Ready', label: 'Pronto' },
  { value: 'Archived', label: 'Arquivadas' },
]

type BoardViewMode = 'kanban' | 'vertical'

export function Board() {
  const queryClient = useQueryClient()
  const hub = usePromptHub()
  const { joinTasks, leaveTasks } = hub
  const [q, setQ] = useState('')
  const [workingDirectoryId, setWorkingDirectoryId] = useState('')
  const [workflowStatus, setWorkflowStatus] = useState<PromptWorkflowStatus | ''>('')
  const [promptStatus, setPromptStatus] = useState<PromptStatus | ''>('')
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [viewMode, setViewMode] = useState<BoardViewMode>('kanban')
  const [draggedPromptId, setDraggedPromptId] = useState<string | null>(null)
  const [dragOverColumnId, setDragOverColumnId] = useState<string | null>(null)
  const boardScrollerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    joinTasks()
    return () => leaveTasks()
  }, [joinTasks, leaveTasks])

  const filters = useMemo(
    () => ({
      q: q.trim() || undefined,
      workingDirectoryId: workingDirectoryId || undefined,
      workflowStatus: workflowStatus || undefined,
      promptStatus: promptStatus || undefined,
    }),
    [promptStatus, q, workflowStatus, workingDirectoryId],
  )

  const boardQuery = useQuery({
    queryKey: queryKeys.workflow.board(filters),
    queryFn: () => getBoard(filters),
  })
  const templateQuery = useQuery({
    queryKey: queryKeys.workflow.template(),
    queryFn: getWorkflowTemplate,
  })
  const workspacesQuery = useQuery({
    queryKey: queryKeys.workingDirectories.all,
    queryFn: listWorkingDirectories,
  })

  const columns = useMemo(
    () => buildColumns(boardQuery.data ?? [], templateQuery.data?.phases ?? []),
    [boardQuery.data, templateQuery.data],
  )
  const taskByPromptId = useMemo(
    () => new Map((boardQuery.data ?? []).map((task) => [task.promptId, task])),
    [boardQuery.data],
  )
  const total = boardQuery.data?.length ?? 0
  const activeFiltersCount = [q.trim(), workingDirectoryId, workflowStatus, promptStatus].filter(Boolean).length

  const clearFilters = () => {
    setQ('')
    setWorkingDirectoryId('')
    setWorkflowStatus('')
    setPromptStatus('')
  }

  const invalidateWorkflow = (promptId?: string) => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    void queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
    if (promptId) {
      void queryClient.invalidateQueries({ queryKey: queryKeys.workflow.detail(promptId) })
    }
  }

  const moveTask = useMutation({
    mutationFn: ({ task, column }: { task: TaskSummary; column: BoardColumn }) => moveTaskToColumn(task, column),
    onSuccess: (workflow, variables) => {
      invalidateWorkflow(variables.task.promptId)
      if (workflow) {
        queryClient.setQueryData(queryKeys.workflow.detail(variables.task.promptId), workflow)
      }
    },
    onError: (error) => toast.error(getErrorMessage(error)),
    onSettled: () => {
      setDraggedPromptId(null)
      setDragOverColumnId(null)
    },
  })

  const moveTaskToColumn = async (task: TaskSummary, column: BoardColumn): Promise<Workflow | null> => {
    if (column.kind === 'no-workflow') {
      throw new Error('Não é possível voltar uma tarefa para Sem fluxo.')
    }

    if (task.workflowStatus === null) {
      if (column.kind === 'done') {
        throw new Error('Inicie o fluxo antes de concluir a tarefa.')
      }

      if (column.phaseOrderIndex === undefined) {
        throw new Error('Esta fase não está no template atual.')
      }

      return startWorkflow(task.promptId, column.phaseOrderIndex)
    }

    const workflow = await getWorkflow(task.promptId)
    if (!workflow) {
      throw new Error('Recarregue o quadro antes de mover esta tarefa.')
    }

    if (column.kind === 'done') {
      if (workflow.status === 'Done') {
        return null
      }

      if (workflow.status !== 'Active') {
        throw new Error('Inicie o fluxo antes de concluir a tarefa.')
      }

      return completeWorkflow(task.promptId, workflow.rowVersion)
    }

    const targetPhase = findWorkflowPhase(workflow, column)
    if (!targetPhase) {
      throw new Error('Esta tarefa não possui a fase de destino.')
    }

    if (workflow.currentPhaseId === targetPhase.id && workflow.status === 'Active') {
      return null
    }

    if (workflow.status === 'Done') {
      return reopenWorkflow(task.promptId, workflow.rowVersion, targetPhase.id)
    }

    return setPhase(task.promptId, targetPhase.id, workflow.rowVersion)
  }

  const findWorkflowPhase = (workflow: Workflow, column: BoardColumn) => {
    if (column.phaseOrderIndex !== undefined) {
      const byOrder = workflow.phases.find((phase) => phase.orderIndex === column.phaseOrderIndex)
      if (byOrder) {
        return byOrder
      }
    }

    return workflow.phases.find((phase) => phase.name === column.phaseName)
  }

  const handleDragStart = (task: TaskSummary, event: DragEvent<HTMLDivElement>) => {
    setDraggedPromptId(task.promptId)
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('application/x-prompt-task-id', task.promptId)
    event.dataTransfer.setData('text/plain', task.promptId)
  }

  const handleDrop = (column: BoardColumn, event: DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    setDragOverColumnId(null)

    const promptId = event.dataTransfer.getData('application/x-prompt-task-id') || draggedPromptId
    const task = promptId ? taskByPromptId.get(promptId) : undefined
    if (!task || !column.droppable || moveTask.isPending) {
      setDraggedPromptId(null)
      return
    }

    void moveTask.mutate({ task, column })
  }

  const scrollBoard = (direction: -1 | 1) => {
    const scroller = boardScrollerRef.current
    if (!scroller) {
      return
    }

    scroller.scrollBy({
      left: direction * Math.max(320, scroller.clientWidth * 0.8),
      behavior: 'smooth',
    })
  }

  const renderColumn = (column: BoardColumn, layout: BoardViewMode) => (
    <div
      key={column.id}
      onDragOver={(event) => {
        if (!column.droppable || moveTask.isPending) {
          return
        }
        event.preventDefault()
        event.dataTransfer.dropEffect = 'move'
        setDragOverColumnId(column.id)
      }}
      onDragLeave={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          setDragOverColumnId((current) => (current === column.id ? null : current))
        }
      }}
      onDrop={(event) => handleDrop(column, event)}
      className={`gap-3 rounded-lg p-2 transition-colors ${
        layout === 'kanban'
          ? 'flex w-[calc(100vw-2rem)] shrink-0 snap-start flex-col sm:w-[calc((100vw-3rem)/2)] lg:w-[calc((100vw-4rem)/3)] xl:w-[18.75rem]'
          : 'grid border border-border bg-card'
      } ${dragOverColumnId === column.id ? 'bg-accent' : ''}`}
    >
      <div className="flex items-center justify-between rounded-md bg-muted px-3 py-2">
        <span className="text-sm font-semibold text-foreground">{column.title}</span>
        <span className="rounded-full bg-card px-2 py-0.5 text-xs text-muted-foreground">{column.tasks.length}</span>
      </div>
      <div className={layout === 'kanban' ? 'grid gap-2' : 'grid gap-2 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4'}>
        {column.tasks.map((task) => (
          <TaskCard
            key={task.promptId}
            task={task}
            dragging={draggedPromptId === task.promptId}
            moveDisabled={moveTask.isPending}
            onDragStart={handleDragStart}
            onDragEnd={() => {
              setDraggedPromptId(null)
              setDragOverColumnId(null)
            }}
          />
        ))}
        {column.tasks.length === 0 ? (
          <p
            className={`rounded-md border border-dashed px-3 py-4 text-center text-xs ${
              column.droppable && draggedPromptId ? 'border-border text-muted-foreground' : 'border-border text-subtle-foreground'
            }`}
          >
            {column.droppable && draggedPromptId ? 'Solte aqui' : 'Vazio'}
          </p>
        ) : null}
      </div>
    </div>
  )

  return (
    <section className="grid min-w-0 gap-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex min-w-0 items-center gap-2">
          <Button type="button" variant="secondary" size="sm" onClick={() => setFiltersOpen((current) => !current)}>
            <SlidersHorizontal className="h-4 w-4" />
            Filtros
            {activeFiltersCount > 0 ? (
              <span className="rounded-full bg-primary px-1.5 py-0.5 text-[10px] font-semibold text-white">{activeFiltersCount}</span>
            ) : null}
          </Button>
          <span className="truncate text-xs text-muted-foreground">{total} tarefas</span>
          <div className="flex rounded-md border border-border bg-card p-0.5">
            <Button
              type="button"
              variant={viewMode === 'kanban' ? 'default' : 'ghost'}
              size="sm"
              className="h-7 px-2"
              onClick={() => setViewMode('kanban')}
              title="Visualização Kanban"
            >
              <Columns3 className="h-4 w-4" />
              Kanban
            </Button>
            <Button
              type="button"
              variant={viewMode === 'vertical' ? 'default' : 'ghost'}
              size="sm"
              className="h-7 px-2"
              onClick={() => setViewMode('vertical')}
              title="Visualização vertical"
            >
              <Rows3 className="h-4 w-4" />
              Vertical
            </Button>
          </div>
          {activeFiltersCount > 0 ? (
            <Button type="button" variant="ghost" size="sm" onClick={clearFilters}>
              <X className="h-4 w-4" />
              Limpar
            </Button>
          ) : null}
        </div>
        <div className="flex items-center gap-1">
          <Link to="/settings">
            <Button type="button" variant="secondary" size="icon" title="Configurar fases" aria-label="Configurar fases">
              <Settings2 className="h-4 w-4" />
            </Button>
          </Link>
          {total > 0 && viewMode === 'kanban' ? (
            <>
              <Button
                type="button"
                variant="secondary"
                size="icon"
                title="Rolar para esquerda"
                aria-label="Rolar quadro para esquerda"
                onClick={() => scrollBoard(-1)}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <Button
                type="button"
                variant="secondary"
                size="icon"
                title="Rolar para direita"
                aria-label="Rolar quadro para direita"
                onClick={() => scrollBoard(1)}
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </>
          ) : null}
        </div>
      </div>

      {filtersOpen ? (
        <div className="flex flex-col gap-2 rounded-lg border border-border bg-card p-3 lg:flex-row lg:items-end">
          <label className="grid flex-1 gap-1 text-xs font-medium text-foreground">
            Buscar
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input className="pl-9" value={q} onChange={(event) => setQ(event.target.value)} placeholder="Título ou conteúdo" />
            </div>
          </label>
          <label className="grid gap-1 text-xs font-medium text-foreground lg:w-48">
            Diretório
            <Select value={workingDirectoryId} onChange={(event) => setWorkingDirectoryId(event.target.value)}>
              <option value="">Todos</option>
              {workspacesQuery.data?.map((workspace) => (
                <option key={workspace.id} value={workspace.id}>
                  {workspace.name}
                </option>
              ))}
            </Select>
          </label>
          <label className="grid gap-1 text-xs font-medium text-foreground lg:w-40">
            Fluxo
            <Select value={workflowStatus} onChange={(event) => setWorkflowStatus(event.target.value as PromptWorkflowStatus | '')}>
              <option value="">Todos</option>
              <option value="Active">Em andamento</option>
              <option value="Done">Concluídas</option>
            </Select>
          </label>
          <label className="grid gap-1 text-xs font-medium text-foreground lg:w-40">
            Prompts
            <Select value={promptStatus} onChange={(event) => setPromptStatus(event.target.value as PromptStatus | '')}>
              {PROMPT_STATUS_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
          </label>
        </div>
      ) : null}

      {boardQuery.isLoading ? (
        <div className="flex items-center gap-2 rounded-lg border border-border bg-card p-4 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Carregando tarefas
        </div>
      ) : total === 0 ? (
        <div className="rounded-lg border border-dashed border-input bg-card p-6 text-sm text-muted-foreground">
          Nenhuma tarefa encontrada. Crie um prompt em um diretório para começar.
        </div>
      ) : viewMode === 'kanban' ? (
        <div
          ref={boardScrollerRef}
          tabIndex={0}
          onKeyDown={(event) => {
            if (event.key === 'ArrowLeft') {
              event.preventDefault()
              scrollBoard(-1)
            }
            if (event.key === 'ArrowRight') {
              event.preventDefault()
              scrollBoard(1)
            }
          }}
          className="h-[calc(100vh-10rem)] min-h-[24rem] min-w-0 snap-x snap-mandatory overflow-auto rounded-lg pb-2 pr-1 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <div className="flex min-w-max gap-4 pb-2">
            {columns.map((column) => renderColumn(column, 'kanban'))}
          </div>
        </div>
      ) : (
        <div className="grid max-h-[calc(100vh-9rem)] gap-3 overflow-auto pr-1">
          {columns.map((column) => renderColumn(column, 'vertical'))}
        </div>
      )}
    </section>
  )
}
