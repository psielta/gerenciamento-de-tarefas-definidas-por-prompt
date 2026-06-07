import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ClipboardCopy, Link2, Loader2, Pencil, Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { deleteFutureTask, listFutureTasks, updateFutureTaskStatus } from '@/api/future-tasks'
import { queryKeys } from '@/api/query-keys'
import { type FutureTask, type FutureTaskStatus, type FutureTaskType } from '@/api/schemas'
import { MarkdownPreview } from '@/components/markdown-preview'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { buildCreateIssuePrompt } from './build-create-issue-prompt'
import {
  STATUS_BADGE_VARIANTS,
  STATUS_LABELS,
  STATUS_OPTIONS,
  TYPE_BADGE_VARIANTS,
  TYPE_LABELS,
  TYPE_OPTIONS,
} from './constants'
import { FutureTaskFormDrawer } from './future-task-form-drawer'

type FutureTaskListProps = {
  workspaceId: string
}

type DialogState = { mode: 'create' } | { mode: 'edit'; task: FutureTask } | null

export function FutureTaskList({ workspaceId }: FutureTaskListProps) {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [status, setStatus] = useState<FutureTaskStatus | ''>('')
  const [type, setType] = useState<FutureTaskType | ''>('')
  const [dialog, setDialog] = useState<DialogState>(null)

  const filters = useMemo(
    () => ({
      workingDirectoryId: workspaceId,
      q: q.trim() || undefined,
      status: status || undefined,
      type: type || undefined,
    }),
    [q, status, type, workspaceId],
  )

  const tasksQuery = useQuery({
    queryKey: queryKeys.futureTasks.list(filters),
    queryFn: () => listFutureTasks(filters),
  })

  const statusMutation = useMutation({
    mutationFn: ({ task, next }: { task: FutureTask; next: FutureTaskStatus }) =>
      updateFutureTaskStatus(task.id, next, task.rowVersion),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.futureTasks.all })
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteFutureTask(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.futureTasks.all })
      toast.success('Tarefa removida.')
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const copyIssuePrompt = async (task: FutureTask) => {
    try {
      await navigator.clipboard.writeText(buildCreateIssuePrompt(task))
      toast.success('Prompt de criacao de issue copiado.')
    } catch {
      toast.error('Nao foi possivel copiar para a area de transferencia.')
    }
  }

  const removeTask = (task: FutureTask) => {
    if (window.confirm(`Remover a tarefa "${task.title}"? Os prompts vinculados sao mantidos.`)) {
      deleteMutation.mutate(task.id)
    }
  }

  const tasks = tasksQuery.data ?? []

  return (
    <div className="grid gap-4">
      <div className="flex flex-col gap-3 rounded-lg border border-border bg-card p-3 sm:flex-row sm:items-center">
        <Input
          placeholder="Buscar tarefas futuras..."
          value={q}
          onChange={(event) => setQ(event.target.value)}
          className="sm:max-w-xs"
        />
        <Select
          aria-label="Filtrar por status"
          value={status}
          onChange={(event) => setStatus(event.target.value as FutureTaskStatus | '')}
          className="sm:max-w-[12rem]"
        >
          <option value="">Todas (ativas)</option>
          {STATUS_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </Select>
        <Select
          aria-label="Filtrar por tipo"
          value={type}
          onChange={(event) => setType(event.target.value as FutureTaskType | '')}
          className="sm:max-w-[12rem]"
        >
          <option value="">Todos os tipos</option>
          {TYPE_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </Select>
        <Button type="button" className="sm:ml-auto" onClick={() => setDialog({ mode: 'create' })}>
          <Plus className="h-4 w-4" />
          Nova tarefa
        </Button>
      </div>

      {tasksQuery.isLoading ? (
        <div className="flex items-center gap-2 rounded-lg border border-border bg-card p-4 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Carregando tarefas
        </div>
      ) : tasksQuery.isError ? (
        <div className="rounded-lg border border-danger-border bg-danger-soft p-4 text-sm text-danger-soft-foreground">
          {getErrorMessage(tasksQuery.error)}
        </div>
      ) : tasks.length === 0 ? (
        <div className="rounded-lg border border-dashed border-border bg-card p-8 text-center text-sm text-muted-foreground">
          Nenhuma tarefa futura por aqui. Crie um item de backlog para comecar.
        </div>
      ) : (
        <div className="grid gap-3">
          {tasks.map((task) => (
            <div key={task.id} className="grid gap-3 rounded-lg border border-border bg-card p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <h3 className="truncate font-medium text-foreground" title={task.title}>
                    {task.title}
                  </h3>
                  <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                    <Badge variant={TYPE_BADGE_VARIANTS[task.type]}>{TYPE_LABELS[task.type]}</Badge>
                    <Badge variant={STATUS_BADGE_VARIANTS[task.status]}>{STATUS_LABELS[task.status]}</Badge>
                    {task.labels.map((label) => (
                      <Badge key={label} variant="neutral">
                        {label}
                      </Badge>
                    ))}
                    {task.issueGithubId ? (
                      <span className="text-xs text-muted-foreground">#{task.issueGithubId}</span>
                    ) : null}
                  </div>
                </div>
                {task.linkedPromptCount > 0 ? (
                  <span className="flex items-center gap-1 text-xs text-muted-foreground">
                    <Link2 className="h-3.5 w-3.5" />
                    {task.linkedPromptCount} prompt(s)
                  </span>
                ) : null}
              </div>

              {task.description.trim() ? (
                <MarkdownPreview className="mt-1 max-h-32">{task.description}</MarkdownPreview>
              ) : null}

              <div className="flex flex-wrap items-center gap-2">
                <Select
                  aria-label="Alterar status"
                  value={task.status}
                  disabled={statusMutation.isPending}
                  onChange={(event) =>
                    statusMutation.mutate({ task, next: event.target.value as FutureTaskStatus })
                  }
                  className="h-8 w-auto"
                >
                  {STATUS_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </Select>
                <Button type="button" variant="secondary" size="sm" onClick={() => copyIssuePrompt(task)}>
                  <ClipboardCopy className="h-4 w-4" />
                  Copiar prompt de issue
                </Button>
                <Button type="button" variant="ghost" size="sm" onClick={() => setDialog({ mode: 'edit', task })}>
                  <Pencil className="h-4 w-4" />
                  Editar
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8 text-muted-foreground"
                  onClick={() => removeTask(task)}
                  disabled={deleteMutation.isPending}
                  aria-label="Remover tarefa"
                  title="Remover"
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {dialog ? (
        <FutureTaskFormDrawer
          workspaceId={workspaceId}
          task={dialog.mode === 'edit' ? dialog.task : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}
