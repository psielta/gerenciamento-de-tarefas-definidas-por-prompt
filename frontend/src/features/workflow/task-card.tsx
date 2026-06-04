import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Archive, ArrowRight, CheckCircle2, FolderGit2, Loader2, PlayCircle } from 'lucide-react'
import type { DragEvent } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { getPrompt, updatePromptStatus } from '@/api/prompts'
import { queryKeys } from '@/api/query-keys'
import type { TaskSummary } from '@/api/schemas'
import { advancePhase, completeWorkflow, getWorkflow, startWorkflow } from '@/api/workflow'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ActorBadge, PhaseBadge } from './badges'
import { formatRelativeTime } from './constants'

type TaskCardProps = {
  task: TaskSummary
  dragging?: boolean
  moveDisabled?: boolean
  onDragStart?: (task: TaskSummary, event: DragEvent<HTMLDivElement>) => void
  onDragEnd?: () => void
}

const WORKSPACE_NAME_MAX_LENGTH = 36

function formatWorkspaceName(name: string) {
  const normalizedName = name.trim()
  if (normalizedName.length <= WORKSPACE_NAME_MAX_LENGTH) {
    return normalizedName
  }

  return `${normalizedName.slice(0, WORKSPACE_NAME_MAX_LENGTH - 3).trimEnd()}...`
}

export function TaskCard({ task, dragging, moveDisabled, onDragStart, onDragEnd }: TaskCardProps) {
  const queryClient = useQueryClient()

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    void queryClient.invalidateQueries({ queryKey: queryKeys.workflow.detail(task.promptId) })
    void queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
  }

  const advanceOrComplete = useMutation({
    mutationFn: async () => {
      const workflow = await getWorkflow(task.promptId)
      if (!workflow || workflow.status !== 'Active') {
        throw new Error('Recarregue o quadro antes de avançar esta tarefa.')
      }

      const phases = [...workflow.phases].sort((a, b) => a.orderIndex - b.orderIndex)
      const currentIndex = phases.findIndex((phase) => phase.id === workflow.currentPhaseId)
      const hasNextPhase = currentIndex >= 0 && currentIndex < phases.length - 1
      if (!hasNextPhase) {
        return completeWorkflow(task.promptId, workflow.rowVersion)
      }

      return advancePhase(task.promptId, workflow.rowVersion)
    },
    onSuccess: invalidate,
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const start = useMutation({
    mutationFn: () => startWorkflow(task.promptId),
    onSuccess: invalidate,
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const archive = useMutation({
    mutationFn: async () => {
      const prompt = await getPrompt(task.promptId)
      if (prompt.status === 'Archived') {
        return prompt
      }

      return updatePromptStatus(task.promptId, 'Archived', prompt.rowVersion)
    },
    onSuccess: (prompt) => {
      queryClient.setQueryData(queryKeys.prompts.detail(task.promptId), prompt)
      queryClient.setQueriesData<unknown>({ queryKey: queryKeys.workflow.all }, (current: unknown) => {
        if (!Array.isArray(current) || !current.every((item) => item && typeof item === 'object' && 'promptId' in item)) {
          return current
        }

        return current.filter((item) => (item as TaskSummary).promptId !== task.promptId)
      })
      invalidate()
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const isHumanTurn = task.currentActor === 'Human'
  const currentPhase = task.phases.find((phase) => phase.id === task.currentPhaseId)
  const isLastPhase = task.workflowStatus === 'Active' && currentPhase
    ? currentPhase.orderIndex === Math.max(...task.phases.map((phase) => phase.orderIndex))
    : false
  const isBusy = start.isPending || advanceOrComplete.isPending || archive.isPending
  const workspaceName = formatWorkspaceName(task.workingDirectoryName)
  const linkContent = (
    <>
      <span className="flex min-w-0 flex-wrap items-center gap-1.5">
        {task.taskNumber ? <Badge variant="blue">{task.taskNumber}</Badge> : null}
        <span
          className="line-clamp-2 min-w-0 max-w-full break-words text-sm font-semibold leading-5 text-foreground [overflow-wrap:anywhere]"
          title={task.title}
        >
          {task.title}
        </span>
      </span>
      <span className="flex items-center gap-1 text-xs text-muted-foreground">
        <FolderGit2 className="h-3.5 w-3.5 shrink-0" />
        <span className="min-w-0 truncate" title={task.workingDirectoryName}>
          {workspaceName}
        </span>
      </span>
    </>
  )

  return (
    <div
      draggable={!moveDisabled && !isBusy}
      onDragStart={(event) => onDragStart?.(task, event)}
      onDragEnd={onDragEnd}
      className={`grid min-w-0 gap-3 rounded-lg border bg-card p-3 transition-colors ${
        isHumanTurn ? 'border-warning-solid' : 'border-border'
      } ${moveDisabled || isBusy ? '' : 'cursor-grab active:cursor-grabbing'} ${dragging || archive.isPending ? 'opacity-45' : ''}`}
    >
      {task.taskNumber ? (
        <Link
          to="/workspaces/$workspaceId/tasks/$taskNumber"
          params={{ workspaceId: task.workingDirectoryId, taskNumber: task.taskNumber }}
          search={{}}
          className="grid min-w-0 gap-2"
        >
          {linkContent}
        </Link>
      ) : (
        <Link
          to="/workspaces/$workspaceId/prompts/$promptId"
          params={{ workspaceId: task.workingDirectoryId, promptId: task.promptId }}
          search={{}}
          className="grid min-w-0 gap-2"
        >
          {linkContent}
        </Link>
      )}

      <div className="flex flex-wrap items-center gap-1.5">
        {task.currentPhaseName ? (
          <PhaseBadge name={task.currentPhaseName} color={task.currentPhaseColor} />
        ) : (
          <span className="rounded-md bg-muted px-2 py-1 text-xs font-medium text-muted-foreground">Fluxo não iniciado</span>
        )}
        {task.currentPhaseIteration > 1 ? (
          <Badge variant="blue">re-review #{task.currentPhaseIteration}</Badge>
        ) : null}
        {task.currentActor ? <ActorBadge actor={task.currentActor} highlight /> : null}
      </div>

      <div className="flex items-center justify-between gap-2">
        <span className="min-w-0 text-xs text-subtle-foreground">
          {task.enteredCurrentPhaseAtUtc
            ? `nesta fase ${formatRelativeTime(task.enteredCurrentPhaseAtUtc)}`
            : `atualizada ${formatRelativeTime(task.updatedAtUtc)}`}
        </span>
        <div className="flex shrink-0 items-center gap-1.5">
          {task.workflowStatus === null ? (
            <Button type="button" variant="secondary" size="sm" onClick={() => start.mutate()} disabled={isBusy}>
              {start.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <PlayCircle className="h-3.5 w-3.5" />}
              Iniciar
            </Button>
          ) : task.workflowStatus === 'Active' ? (
            <Button type="button" variant="secondary" size="sm" onClick={() => advanceOrComplete.mutate()} disabled={isBusy}>
              {advanceOrComplete.isPending ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : isLastPhase ? (
                <CheckCircle2 className="h-3.5 w-3.5" />
              ) : (
                <ArrowRight className="h-3.5 w-3.5" />
              )}
              {isLastPhase ? 'Concluir' : 'Avançar'}
            </Button>
          ) : null}
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-muted-foreground"
            onClick={() => archive.mutate()}
            disabled={isBusy || moveDisabled || task.promptStatus === 'Archived'}
            title="Arquivar"
            aria-label="Arquivar prompt"
          >
            {archive.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Archive className="h-4 w-4" />}
          </Button>
        </div>
      </div>
    </div>
  )
}
