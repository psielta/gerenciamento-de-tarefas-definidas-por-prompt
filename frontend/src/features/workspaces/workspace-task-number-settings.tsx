import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Hash, Loader2, Save } from 'lucide-react'
import { useEffect } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { getErrorMessage } from '@/api/client'
import { queryKeys } from '@/api/query-keys'
import { getWorkingDirectory, updateWorkingDirectory } from '@/api/working-directories'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { formatTaskNumberPreview, validateTaskNumberPattern } from './task-number-format'

const settingsSchema = z
  .object({
    taskNumberPattern: z.string(),
  })
  .superRefine((values, context) => {
    for (const error of validateTaskNumberPattern(values.taskNumberPattern)) {
      context.addIssue({ code: 'custom', path: ['taskNumberPattern'], message: error })
    }
  })

type SettingsValues = z.infer<typeof settingsSchema>

type WorkspaceTaskNumberSettingsProps = {
  workspaceId: string
}

export function WorkspaceTaskNumberSettings({ workspaceId }: WorkspaceTaskNumberSettingsProps) {
  const queryClient = useQueryClient()
  const workspaceQuery = useQuery({
    queryKey: queryKeys.workingDirectories.detail(workspaceId),
    queryFn: () => getWorkingDirectory(workspaceId),
  })
  const form = useForm<SettingsValues>({
    resolver: zodResolver(settingsSchema),
    defaultValues: { taskNumberPattern: '' },
  })
  const taskNumberPattern = useWatch({ control: form.control, name: 'taskNumberPattern' })
  const patternErrors = validateTaskNumberPattern(taskNumberPattern)
  const preview = taskNumberPattern.trim() && patternErrors.length === 0
    ? formatTaskNumberPreview(taskNumberPattern.trim(), 1, new Date())
    : null

  useEffect(() => {
    if (workspaceQuery.data) {
      form.reset({ taskNumberPattern: workspaceQuery.data.taskNumberPattern ?? '' })
    }
  }, [form, workspaceQuery.data])

  const updateMutation = useMutation({
    mutationFn: (values: SettingsValues) => {
      if (!workspaceQuery.data) {
        throw new Error('Diretorio de trabalho ainda nao carregado.')
      }

      return updateWorkingDirectory(workspaceId, {
        name: workspaceQuery.data.name,
        absolutePath: workspaceQuery.data.absolutePath,
        respectGitignore: workspaceQuery.data.respectGitignore,
        enableAiContext: workspaceQuery.data.enableAiContext,
        taskNumberPattern: values.taskNumberPattern.trim(),
      })
    },
    onSuccess: async (workspace) => {
      form.reset({ taskNumberPattern: workspace.taskNumberPattern ?? '' })
      queryClient.setQueryData(queryKeys.workingDirectories.detail(workspaceId), workspace)
      await queryClient.invalidateQueries({ queryKey: queryKeys.workingDirectories.all })
      toast.success('Numeracao de tarefas atualizada.')
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const onSubmit = form.handleSubmit((values) => updateMutation.mutate(values))

  return (
    <form onSubmit={onSubmit} className="grid gap-3 rounded-lg border border-border bg-card p-4">
      <div className="flex min-w-0 items-start gap-3">
        <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-md bg-muted">
          <Hash className="h-4 w-4 text-primary" />
        </div>
        <div className="min-w-0">
          <h2 className="text-sm font-semibold text-foreground">Numeracao de tarefas</h2>
          <p className="mt-1 text-sm text-muted-foreground">Prompts pai recebem o identificador ao serem criados.</p>
        </div>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="min-w-0 flex-1">
          <FormField label="Padrao" htmlFor="workspace-task-number-settings" error={form.formState.errors.taskNumberPattern?.message}>
            <Input id="workspace-task-number-settings" placeholder="BP{N}{Date}" {...form.register('taskNumberPattern')} />
          </FormField>
        </div>
        <Button type="submit" disabled={updateMutation.isPending || !form.formState.isDirty || workspaceQuery.isLoading}>
          {updateMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          Salvar
        </Button>
      </div>

      {preview ? <p className="text-sm text-muted-foreground">Preview: {preview}</p> : null}
    </form>
  )
}
