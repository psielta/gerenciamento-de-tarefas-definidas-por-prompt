import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2, Save, X } from 'lucide-react'
import { useEffect } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { createFutureTask, updateFutureTask } from '@/api/future-tasks'
import { queryKeys } from '@/api/query-keys'
import { type FutureTask } from '@/api/schemas'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'
import { LABEL_OPTIONS, TYPE_OPTIONS, futureTaskFormSchema, type FutureTaskFormValues } from './constants'

type FutureTaskFormDialogProps = {
  workspaceId: string
  task?: FutureTask
  onClose: () => void
}

export function FutureTaskFormDialog({ workspaceId, task, onClose }: FutureTaskFormDialogProps) {
  const queryClient = useQueryClient()
  const isEditing = Boolean(task)

  const form = useForm<FutureTaskFormValues>({
    resolver: zodResolver(futureTaskFormSchema),
    defaultValues: {
      title: task?.title ?? '',
      description: task?.description ?? '',
      type: task?.type ?? 'Task',
      labels: task?.labels ?? [],
      issueGithubId: task?.issueGithubId ?? '',
    },
  })

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  const selectedLabels = useWatch({ control: form.control, name: 'labels' })

  const toggleLabel = (label: string) => {
    const next = selectedLabels.includes(label)
      ? selectedLabels.filter((item) => item !== label)
      : [...selectedLabels, label]
    form.setValue('labels', next, { shouldDirty: true })
  }

  const saveMutation = useMutation({
    mutationFn: (values: FutureTaskFormValues) => {
      const issueGithubId = values.issueGithubId.trim() ? values.issueGithubId.trim() : null
      if (task) {
        return updateFutureTask(task.id, {
          title: values.title,
          description: values.description,
          type: values.type,
          labels: values.labels,
          issueGithubId,
          rowVersion: task.rowVersion,
        })
      }

      return createFutureTask({
        workingDirectoryId: workspaceId,
        title: values.title,
        description: values.description,
        type: values.type,
        labels: values.labels,
        issueGithubId,
      })
    },
    onSuccess: async (saved) => {
      queryClient.setQueryData(queryKeys.futureTasks.detail(saved.id), saved)
      await queryClient.invalidateQueries({ queryKey: queryKeys.futureTasks.all })
      toast.success(isEditing ? 'Tarefa atualizada.' : 'Tarefa criada.')
      onClose()
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="future-task-dialog-title"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
    >
      <div className="grid max-h-[90vh] w-full max-w-xl gap-4 overflow-y-auto rounded-lg border border-border bg-card p-4 shadow-2xl">
        <div className="flex items-start justify-between gap-3">
          <h2 id="future-task-dialog-title" className="text-base font-semibold text-foreground">
            {isEditing ? 'Editar tarefa futura' : 'Nova tarefa futura'}
          </h2>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="h-8 w-8 shrink-0 text-muted-foreground"
            onClick={onClose}
            aria-label="Fechar"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>

        <form onSubmit={form.handleSubmit((values) => saveMutation.mutate(values))} className="grid gap-3">
          <FormField label="Titulo" htmlFor="future-task-title" error={form.formState.errors.title?.message}>
            <Input
              id="future-task-title"
              placeholder="Suportar tema escuro"
              autoFocus
              {...form.register('title')}
            />
          </FormField>

          <div className="grid gap-3 sm:grid-cols-2">
            <FormField label="Tipo" htmlFor="future-task-type">
              <Select id="future-task-type" {...form.register('type')}>
                {TYPE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </Select>
            </FormField>

            <FormField
              label="Issue do GitHub (opcional)"
              htmlFor="future-task-issue"
              error={form.formState.errors.issueGithubId?.message}
            >
              <Input id="future-task-issue" placeholder="Ex.: 42" {...form.register('issueGithubId')} />
            </FormField>
          </div>

          <FormField label="Labels">
            <div className="flex flex-wrap gap-2">
              {LABEL_OPTIONS.map((label) => {
                const active = selectedLabels.includes(label)
                return (
                  <button
                    key={label}
                    type="button"
                    onClick={() => toggleLabel(label)}
                    aria-pressed={active}
                    className={cn(
                      'rounded-md border px-2 py-1 text-xs font-medium transition-colors',
                      active
                        ? 'border-primary bg-primary text-primary-foreground'
                        : 'border-border bg-card text-muted-foreground hover:border-ring',
                    )}
                  >
                    {label}
                  </button>
                )
              })}
            </div>
          </FormField>

          <FormField
            label="Descricao (Markdown)"
            htmlFor="future-task-description"
            error={form.formState.errors.description?.message}
          >
            <Textarea
              id="future-task-description"
              className="min-h-40"
              placeholder="Contexto, escopo e criterios de aceite..."
              {...form.register('description')}
            />
          </FormField>

          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={onClose} disabled={saveMutation.isPending}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saveMutation.isPending}>
              {saveMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
              Salvar
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
