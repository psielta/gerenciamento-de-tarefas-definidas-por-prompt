import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Archive, ArrowRight, FolderGit2, Loader2, PlayCircle } from 'lucide-react'
import type { DragEvent } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { updatePromptStatus } from '@/api/prompts'
import { queryKeys } from '@/api/query-keys'
import type { TaskSummary } from '@/api/schemas'
import { advancePhase, startWorkflow } from '@/api/workflow'
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

export function TaskCard({ task, dragging, moveDisabled, onDragStart, onDragEnd }: TaskCardProps) {
  const queryClient = useQueryClient()

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    void queryClient.invalidateQueries({ queryKey: queryKeys.workflow.detail(task.promptId) })
    void queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
  }

  const advance = useMutation({
    mutationFn: () => advancePhase(task.promptId, task.rowVersion ?? ''),
    onSuccess: invalidate,
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const start = useMutation({
    mutationFn: () => startWorkflow(task.promptId),
    onSuccess: invalidate,
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const archive = useMutation({
    mutationFn: () => updatePromptStatus(task.promptId, 'Archived', task.promptRowVersion),
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
  const isBusy = start.isPending || advance.isPending || archive.isPending

  return (
    <div
      draggable={!moveDisabled && !isBusy}
      onDragStart={(event) => onDragStart?.(task, event)}
      onDragEnd={onDragEnd}
      className={`grid gap-3 rounded-lg border bg-white p-3 transition-colors ${
        isHumanTurn ? 'border-[#e0b84a]' : 'border-[#d9dfd5]'
      } ${moveDisabled || isBusy ? '' : 'cursor-grab active:cursor-grabbing'} ${dragging || archive.isPending ? 'opacity-45' : ''}`}
    >
      <Link
        to="/workspaces/$workspaceId/prompts/$promptId"
        params={{ workspaceId: task.workingDirectoryId, promptId: task.promptId }}
        search={{ tab: 'timeline' }}
        className="grid gap-2"
      >
        <span className="line-clamp-2 text-sm font-semibold text-[#172126]">{task.title}</span>
        <span className="flex items-center gap-1 text-xs text-[#66746b]">
          <FolderGit2 className="h-3.5 w-3.5 shrink-0" />
          <span className="truncate">{task.workingDirectoryName}</span>
        </span>
      </Link>

      <div className="flex flex-wrap items-center gap-1.5">
        {task.currentPhaseName ? (
          <PhaseBadge name={task.currentPhaseName} color={task.currentPhaseColor} />
        ) : (
          <span className="rounded-md bg-[#eef2eb] px-2 py-1 text-xs font-medium text-[#66746b]">Fluxo não iniciado</span>
        )}
        {task.currentActor ? <ActorBadge actor={task.currentActor} highlight /> : null}
      </div>

      <div className="flex items-center justify-between gap-2">
        <span className="min-w-0 text-xs text-[#8a958c]">
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
            <Button type="button" variant="secondary" size="sm" onClick={() => advance.mutate()} disabled={isBusy}>
              {advance.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <ArrowRight className="h-3.5 w-3.5" />}
              Avançar
            </Button>
          ) : null}
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-[#66746b]"
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
