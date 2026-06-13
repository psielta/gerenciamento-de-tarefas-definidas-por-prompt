import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { Bot, Copy, Languages, Loader2, Save, Sparkles, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { FormField } from '@/components/form-field'
import { createPrompt, deletePrompt, getPrompt, updatePrompt } from '@/api/prompts'
import { listFutureTasks } from '@/api/future-tasks'
import { queryKeys } from '@/api/query-keys'
import { type FileMention, type Prompt } from '@/api/schemas'
import { getErrorMessage } from '@/api/client'
import {
  AGENT_OPTIONS,
  KIND_OPTIONS,
  STATUS_OPTIONS,
  promptFormSchema,
  type PromptFormValues,
} from './constants'
import { useFileViewer } from '@/features/files/use-file-viewer'
import { useGitHistory } from '@/features/files/use-git-history'
import { WorkspaceFileTree } from '@/features/files/workspace-file-tree'
import { buildSeededPromptContent } from '@/features/future-tasks/seed-prompt-content'
import { PromptEditor } from './prompt-editor'
import { RefineDialog } from './ai/refine-dialog'
import { AiAssistantPanel } from './ai/ai-assistant-panel'
import { TranslateDialog } from './ai/translate-dialog'

type PromptFormProps = {
  workingDirectoryId: string
  promptId?: string
  onDeleted?: () => void
  onCreated?: (prompt: Prompt) => void
  showWorkspaceFileTree?: boolean
  initialTitle?: string
  initialContent?: string
}

type CreatePromptFormPayload = {
  values: PromptFormValues
  copyAfterCreate: boolean
}

export function PromptForm({
  workingDirectoryId,
  promptId,
  onDeleted,
  onCreated,
  showWorkspaceFileTree = true,
  initialTitle,
  initialContent,
}: PromptFormProps) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { openFile } = useFileViewer()
  const { openHistory } = useGitHistory()
  const [editorMentions, setEditorMentions] = useState<{
    promptId?: string
    mentions: FileMention[]
  } | null>(null)
  const [showRefineDialog, setShowRefineDialog] = useState(false)
  const [showTranslateDialog, setShowTranslateDialog] = useState(false)
  const [showAiPanel, setShowAiPanel] = useState(false)
  const [futureTaskId, setFutureTaskId] = useState('')

  const promptQuery = useQuery({
    queryKey: promptId ? queryKeys.prompts.detail(promptId) : ['prompts', 'new'],
    queryFn: () => getPrompt(promptId ?? ''),
    enabled: Boolean(promptId),
  })

  const futureTasksQuery = useQuery({
    queryKey: queryKeys.futureTasks.list({ workingDirectoryId }),
    queryFn: () => listFutureTasks({ workingDirectoryId }),
    enabled: !promptId,
  })

  const form = useForm<PromptFormValues>({
    resolver: zodResolver(promptFormSchema),
    defaultValues: {
      title: promptId ? '' : initialTitle ?? '',
      targetAgent: 'Codex',
      kind: 'General',
      status: 'Draft',
      content: promptId ? '' : initialContent ?? '',
    },
  })

  const content = useWatch({
    control: form.control,
    name: 'content',
  })

  const title = useWatch({
    control: form.control,
    name: 'title',
  })

  useEffect(() => {
    if (!promptQuery.data) {
      return
    }

    form.reset({
      title: promptQuery.data.title,
      targetAgent: promptQuery.data.targetAgent,
      kind: promptQuery.data.kind,
      status: promptQuery.data.status,
      content: promptQuery.data.content,
    })
  }, [form, promptQuery.data])

  const mentions =
    editorMentions && editorMentions.promptId === promptId
      ? editorMentions.mentions
      : promptQuery.data?.mentions ?? []

  const handleSelectFutureTask = (taskId: string) => {
    setFutureTaskId(taskId)
    if (!taskId) {
      return
    }

    const task = futureTasksQuery.data?.find((item) => item.id === taskId)
    if (!task) {
      return
    }

    form.setValue('content', buildSeededPromptContent(task), { shouldDirty: true, shouldValidate: true })
    setEditorMentions({ promptId, mentions: [] })
    if (!form.getValues('title').trim()) {
      form.setValue('title', task.title, { shouldDirty: true, shouldValidate: true })
    }
  }

  const createMutation = useMutation({
    mutationFn: ({ values }: CreatePromptFormPayload) =>
      createPrompt({
        workingDirectoryId,
        futureTaskId: futureTaskId || undefined,
        ...values,
        mentions,
      }),
    onSuccess: async (prompt, payload) => {
      await afterSave(prompt)
      if (futureTaskId) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.futureTasks.all })
      }
      if (payload.copyAfterCreate) {
        try {
          if (!navigator.clipboard?.writeText) {
            throw new Error('Clipboard indisponivel.')
          }

          await navigator.clipboard.writeText(payload.values.content)
          toast.success('Prompt criado e copiado.')
        } catch {
          toast.warning('Prompt criado, mas nao foi possivel copiar.')
        }
      } else {
        toast.success('Prompt criado.')
      }
      if (onCreated) {
        onCreated(prompt)
        return
      }
      if (prompt.taskNumber) {
        await navigate({
          to: '/workspaces/$workspaceId/tasks/$taskNumber',
          params: { workspaceId: workingDirectoryId, taskNumber: prompt.taskNumber },
        })
        return
      }

      await navigate({
        to: '/workspaces/$workspaceId/prompts/$promptId',
        params: { workspaceId: workingDirectoryId, promptId: prompt.id },
      })
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const updateMutation = useMutation({
    mutationFn: (values: PromptFormValues) =>
      updatePrompt(promptId ?? '', {
        ...values,
        rowVersion: promptQuery.data?.rowVersion ?? '',
        mentions,
      }),
    onSuccess: async (prompt) => {
      await afterSave(prompt)
      toast.success('Prompt salvo.')
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const deleteMutation = useMutation({
    mutationFn: () => deletePrompt(promptId ?? ''),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
      await queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
      toast.success('Prompt removido.')
      if (onDeleted) {
        onDeleted()
      } else {
        await navigate({
          to: '/workspaces/$workspaceId',
          params: { workspaceId: workingDirectoryId },
        })
      }
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const afterSave = async (prompt: Prompt) => {
    queryClient.setQueryData(queryKeys.prompts.detail(prompt.id), prompt)
    await queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
    await queryClient.invalidateQueries({ queryKey: queryKeys.prompts.versions(prompt.id) })
    await queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
  }

  const createPromptFromForm = (values: PromptFormValues, copyAfterCreate: boolean) => {
    createMutation.mutate({
      values,
      copyAfterCreate,
    })
  }

  const onSubmit = form.handleSubmit((values) => {
    if (promptId) {
      updateMutation.mutate(values)
      return
    }

    createPromptFromForm(values, false)
  })

  const onSubmitAndCopy = () => {
    void form.handleSubmit((values) => createPromptFromForm(values, true))()
  }

  const isBusy = createMutation.isPending || updateMutation.isPending || deleteMutation.isPending

  if (promptId && promptQuery.isError) {
    return (
      <div className="rounded-lg border border-danger-border bg-danger-soft p-4 text-sm text-danger-soft-foreground">
        {getErrorMessage(promptQuery.error)}
      </div>
    )
  }

  const isWaitingForPromptValues = Boolean(
    promptId && promptQuery.data && !form.formState.isDirty && content !== promptQuery.data.content,
  )

  if (promptId && (promptQuery.isLoading || !promptQuery.data || isWaitingForPromptValues)) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-border bg-card p-4 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        Carregando prompt
      </div>
    )
  }

  return (
    <>
      {showRefineDialog ? (
        <RefineDialog
          content={content}
          workingDirectoryId={workingDirectoryId}
          onApply={(refined) => {
            form.setValue('content', refined, { shouldDirty: true, shouldValidate: true })
            setEditorMentions({ promptId, mentions: [] })
          }}
          onClose={() => setShowRefineDialog(false)}
        />
      ) : null}
      {showAiPanel ? (
        <AiAssistantPanel
          promptId={promptId}
          workingDirectoryId={workingDirectoryId}
          promptContent={content}
          onClose={() => setShowAiPanel(false)}
        />
      ) : null}
      {showTranslateDialog ? (
        <TranslateDialog
          content={content}
          onApply={(translated) => {
            form.setValue('content', translated, { shouldDirty: true, shouldValidate: true })
            setEditorMentions({ promptId, mentions: [] })
          }}
          onClose={() => setShowTranslateDialog(false)}
        />
      ) : null}
    <form
      onSubmit={onSubmit}
      className={showWorkspaceFileTree ? 'grid gap-5 xl:grid-cols-[14rem_minmax(0,1fr)]' : 'grid gap-5'}
    >
      {showWorkspaceFileTree ? (
        <WorkspaceFileTree
          workingDirectoryId={workingDirectoryId}
          onOpenFile={(relativePath) => openFile(workingDirectoryId, relativePath)}
          onShowGitHistory={(relativePath) => openHistory(workingDirectoryId, relativePath)}
          className="hidden min-h-[28rem] xl:grid"
        />
      ) : null}
      <div className="grid content-start gap-4 rounded-lg border border-border bg-card p-4">
        {!promptId && (futureTasksQuery.data?.length ?? 0) > 0 ? (
          <FormField label="Tarefa futura (opcional)" htmlFor="prompt-future-task">
            <Select
              id="prompt-future-task"
              value={futureTaskId}
              onChange={(event) => handleSelectFutureTask(event.target.value)}
            >
              <option value="">Nenhuma</option>
              {futureTasksQuery.data?.map((task) => (
                <option key={task.id} value={task.id}>
                  {task.title}
                </option>
              ))}
            </Select>
          </FormField>
        ) : null}

        <FormField label="Titulo" htmlFor="prompt-title" error={form.formState.errors.title?.message}>
          <Input id="prompt-title" placeholder="Planejar refatoracao do modulo X" {...form.register('title')} />
        </FormField>

        <div className="grid gap-4 sm:grid-cols-3">
          <FormField label="Agente" htmlFor="prompt-agent">
            <Select id="prompt-agent" {...form.register('targetAgent')}>
              {AGENT_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
          </FormField>

          <FormField label="Tipo" htmlFor="prompt-kind">
            <Select id="prompt-kind" {...form.register('kind')}>
              {KIND_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
          </FormField>

          <FormField label="Status" htmlFor="prompt-status">
            <Select id="prompt-status" {...form.register('status')}>
              {STATUS_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
          </FormField>
        </div>

        {form.formState.errors.content?.message ? (
          <p className="text-sm font-medium text-destructive">{form.formState.errors.content.message}</p>
        ) : null}

        <PromptEditor
          workingDirectoryId={workingDirectoryId}
          value={content}
          onOpenMention={(relativePath) => openFile(workingDirectoryId, relativePath)}
          onChange={(value, nextMentions) => {
            form.setValue('content', value, { shouldDirty: true, shouldValidate: true })
            setEditorMentions({ promptId, mentions: nextMentions })
          }}
          exportPdfMeta={{
            title: title?.trim() || 'Prompt',
            subtitle: promptQuery.data?.taskNumber ?? undefined,
          }}
        />

        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="min-h-5 text-sm text-muted-foreground">
            {mentions.length ? `${mentions.length} arquivo(s) mencionado(s)` : 'Nenhum arquivo mencionado'}
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              variant="secondary"
              onClick={() => setShowRefineDialog(true)}
              disabled={isBusy || !content.trim()}
              title="Refinar prompt com Gemini"
            >
              <Sparkles className="h-4 w-4" />
              Refinar
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => setShowTranslateDialog(true)}
              disabled={isBusy || !content.trim()}
              title="Tradução para inglês"
            >
              <Languages className="h-4 w-4" />
              Tradução para inglês
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => setShowAiPanel((v) => !v)}
              title="Assistente IA"
            >
              <Bot className="h-4 w-4" />
              IA
            </Button>
            {promptId ? (
              <Button
                type="button"
                variant="destructive"
                onClick={() => deleteMutation.mutate()}
                disabled={isBusy}
              >
                <Trash2 className="h-4 w-4" />
                Remover
              </Button>
            ) : null}
            {!promptId ? (
              <Button
                type="button"
                variant="secondary"
                onClick={onSubmitAndCopy}
                disabled={isBusy}
                title="Salvar e copiar para area de transferencia"
              >
                {createMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Copy className="h-4 w-4" />}
                Salvar e copiar
              </Button>
            ) : null}
            <Button type="submit" disabled={isBusy}>
              {isBusy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
              Salvar
            </Button>
          </div>
        </div>
      </div>

    </form>
    </>
  )
}
