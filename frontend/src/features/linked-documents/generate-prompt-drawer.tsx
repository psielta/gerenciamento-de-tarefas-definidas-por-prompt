import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, Copy, Loader2, Save, Sparkles, X } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { setLinkedDocumentPullRequest } from '@/api/linked-documents'
import { createPrompt } from '@/api/prompts'
import { renderPromptDraft } from '@/api/prompt-templates'
import { queryKeys } from '@/api/query-keys'
import type { FileMention, Prompt, PromptTemplate } from '@/api/schemas'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import {
  AGENT_OPTIONS,
  KIND_OPTIONS,
  promptFormSchema,
  type PromptFormValues,
} from '@/features/prompts/constants'
import { PromptEditor } from '@/features/prompts/prompt-editor'

type GeneratePromptDrawerProps = {
  linkedDocumentId: string
  template: PromptTemplate
  onClose: () => void
  initialPullRequestReference?: string | null
}

type CreateGeneratedPromptPayload = {
  values: PromptFormValues
  copyAfterCreate: boolean
}

export function GeneratePromptDrawer({
  linkedDocumentId,
  template,
  onClose,
  initialPullRequestReference,
}: GeneratePromptDrawerProps) {
  const queryClient = useQueryClient()
  const titleInputRef = useRef<HTMLInputElement>(null)
  const [contentOverride, setContentOverride] = useState<string | null>(null)
  const [editorMentions, setEditorMentions] = useState<FileMention[] | null>(null)
  const templateInputs = template.inputs?.length ? template.inputs : template.input ? [template.input] : []
  const pullRequestInput = templateInputs.find((input) => input.key === 'pullRequest')
  const requiresPullRequest = Boolean(pullRequestInput?.required)
  const storedPullRequest = (initialPullRequestReference ?? '').trim()
  const showPullRequestAlert = requiresPullRequest && !storedPullRequest
  // Pre-preenche a PR puxada do plano vinculado; auto-confirma o rascunho quando todos os
  // inputs obrigatorios ja vem satisfeitos (ex.: Revisar PR com PR salva no plano).
  const initialTemplateInputs: Record<string, string> =
    pullRequestInput && storedPullRequest ? { pullRequest: storedPullRequest } : {}
  const allRequiredSatisfiedInitially = templateInputs.every(
    (input) => !input.required || Boolean((initialTemplateInputs[input.key] ?? '').trim()),
  )
  const [templateInputValues, setTemplateInputValues] = useState<Record<string, string>>(initialTemplateInputs)
  const [confirmedTemplateInputs, setConfirmedTemplateInputs] = useState<Record<string, string> | null>(
    templateInputs.length === 0 || allRequiredSatisfiedInitially ? initialTemplateInputs : null,
  )
  const form = useForm<PromptFormValues>({
    resolver: zodResolver(promptFormSchema),
    defaultValues: {
      title: '',
      targetAgent: template.defaultTargetAgent,
      kind: template.defaultKind,
      status: 'Draft',
      content: '',
    },
  })

  const normalizedTemplateInputs = templateInputs.reduce<Record<string, string>>((values, input) => {
    values[input.key] = (templateInputValues[input.key] ?? '').trim()
    return values
  }, {})
  const activeTemplateInputs = templateInputs.length > 0 ? confirmedTemplateInputs ?? undefined : undefined
  const canRenderDraft = templateInputs.length === 0 || confirmedTemplateInputs !== null
  const hasChangedTemplateInput = Boolean(
    templateInputs.length > 0 &&
      confirmedTemplateInputs !== null &&
      templateInputs.some((input) => normalizedTemplateInputs[input.key] !== (confirmedTemplateInputs[input.key] ?? '')),
  )
  const canSubmitTemplateInputs = templateInputs.every(
    (input) => !input.required || Boolean(normalizedTemplateInputs[input.key]),
  )
  const templateInputLabel = templateInputs.map((input) => input.label).join(' e ')
  const draftQuery = useQuery({
    queryKey: queryKeys.promptTemplates.draft(linkedDocumentId, template.key, activeTemplateInputs),
    queryFn: () =>
      renderPromptDraft(linkedDocumentId, template.key, {
        pullRequest: activeTemplateInputs?.pullRequest,
        inputs: activeTemplateInputs,
      }),
    enabled: canRenderDraft,
    retry: false,
  })
  const activeDraft = hasChangedTemplateInput ? undefined : draftQuery.data
  const editorContent = contentOverride ?? activeDraft?.content ?? ''

  useEffect(() => {
    if (!activeDraft) {
      return
    }

    form.reset({
      title: activeDraft.title,
      targetAgent: activeDraft.targetAgent,
      kind: activeDraft.kind,
      status: 'Draft',
      content: activeDraft.content,
    })

    window.setTimeout(() => titleInputRef.current?.focus(), 0)
  }, [activeDraft, form])

  const afterSave = async (prompt: Prompt) => {
    queryClient.setQueryData(queryKeys.prompts.detail(prompt.id), prompt)
    await queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
    await queryClient.invalidateQueries({ queryKey: queryKeys.prompts.versions(prompt.id) })
    // O pai pode avancar de etapa ao gerar o filho; atualiza o quadro/timeline na hora.
    await queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
  }

  const persistPullRequestIfChanged = async () => {
    if (!requiresPullRequest) {
      return
    }

    const enteredPullRequest = (confirmedTemplateInputs?.pullRequest ?? '').trim()
    if (!enteredPullRequest || enteredPullRequest === storedPullRequest) {
      return
    }

    try {
      const updated = await setLinkedDocumentPullRequest(linkedDocumentId, enteredPullRequest)
      queryClient.setQueryData(queryKeys.linkedDocuments.detail(linkedDocumentId), updated)
      await queryClient.invalidateQueries({ queryKey: queryKeys.linkedDocuments.forPrompt(updated.promptId) })
    } catch {
      // Falha ao salvar a PR no plano nao deve bloquear a criacao do filho.
    }
  }

  const createMutation = useMutation({
    mutationFn: ({ values }: CreateGeneratedPromptPayload) => {
      if (!activeDraft) {
        throw new Error('Rascunho ainda nao foi gerado.')
      }

      return createPrompt({
        workingDirectoryId: activeDraft.workingDirectoryId,
        parentPromptId: activeDraft.parentPromptId,
        title: values.title,
        content: values.content,
        targetAgent: values.targetAgent,
        kind: values.kind,
        status: 'Draft',
        sourceTemplateKey: activeDraft.templateKey,
        mentions: editorMentions ?? [],
      })
    },
    onSuccess: async (prompt, payload) => {
      await afterSave(prompt)
      await persistPullRequestIfChanged()
      if (payload.copyAfterCreate) {
        try {
          await navigator.clipboard.writeText(payload.values.content)
          toast.success('Prompt filho criado e copiado.')
        } catch {
          toast.warning('Prompt filho criado, mas nao foi possivel copiar.')
        }
      } else {
        toast.success('Prompt filho criado.')
      }
      onClose()
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const isBusy = createMutation.isPending
  const isDirty = form.formState.isDirty

  const requestClose = useCallback(() => {
    if (isBusy) {
      return
    }

    if (isDirty && !window.confirm('Descartar o prompt gerado?')) {
      return
    }

    onClose()
  }, [isBusy, isDirty, onClose])

  const submitTemplateInput = () => {
    if (templateInputs.length === 0) {
      return
    }

    const missingInput = templateInputs.find((input) => input.required && !normalizedTemplateInputs[input.key])
    if (missingInput) {
      toast.error(`Informe ${missingInput.label}.`)
      return
    }

    setContentOverride(null)
    setEditorMentions(null)
    setConfirmedTemplateInputs({ ...normalizedTemplateInputs })
  }

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        requestClose()
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [requestClose])

  const submit = (copyAfterCreate: boolean) =>
    form.handleSubmit((values) =>
      createMutation.mutate({
        values: { ...values, content: editorContent },
        copyAfterCreate,
      }),
    )()
  const titleField = form.register('title')

  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-black/50 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="generate-prompt-title"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          requestClose()
        }
      }}
    >
      <div className="grid h-full w-full max-w-xl grid-rows-[auto_minmax(0,1fr)_auto] border-l-4 border-l-warning-solid bg-card shadow-2xl">
        <div className="flex min-w-0 items-start justify-between gap-3 border-b border-warning-solid/20 bg-warning-soft p-4">
          <div className="flex min-w-0 items-start gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-card ring-2 ring-warning-solid/25">
              <Sparkles className="h-4 w-4 text-warning-solid" />
            </div>
            <div className="min-w-0">
              <h2 id="generate-prompt-title" className="text-base font-semibold text-warning-foreground">
                Gerar prompt filho
              </h2>
              <p className="mt-1 truncate text-sm text-muted-foreground" title={template.description}>
                {template.displayName}
              </p>
            </div>
          </div>
          <Button type="button" variant="ghost" size="icon" onClick={requestClose} disabled={isBusy} aria-label="Fechar">
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div data-testid="generate-prompt-drawer-body" className="min-h-0 overflow-y-auto p-4">
          {templateInputs.length > 0 ? (
            <div className="mb-4 grid gap-2 rounded-md border border-border bg-background p-3">
              {showPullRequestAlert ? (
                <div className="flex items-start gap-2 rounded-md border border-warning-solid/40 bg-warning-soft p-2.5 text-xs text-warning-foreground">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  <span>
                    Nenhuma PR definida neste plano vinculado. Informe o numero abaixo — ele sera salvo no plano ao gerar.
                  </span>
                </div>
              ) : null}
              {templateInputs.map((templateInput) => {
                const inputId = `generated-prompt-template-input-${templateInput.key}`

                return (
                  <FormField key={templateInput.key} label={templateInput.label} htmlFor={inputId}>
                    {templateInput.multiline ? (
                      <Textarea
                        id={inputId}
                        value={templateInputValues[templateInput.key] ?? ''}
                        onChange={(event) =>
                          setTemplateInputValues((current) => ({
                            ...current,
                            [templateInput.key]: event.target.value,
                          }))
                        }
                        placeholder={templateInput.placeholder}
                        disabled={isBusy}
                        rows={5}
                      />
                    ) : (
                      <Input
                        id={inputId}
                        value={templateInputValues[templateInput.key] ?? ''}
                        onChange={(event) =>
                          setTemplateInputValues((current) => ({
                            ...current,
                            [templateInput.key]: event.target.value,
                          }))
                        }
                        onKeyDown={(event) => {
                          if (event.key === 'Enter') {
                            event.preventDefault()
                            submitTemplateInput()
                          }
                        }}
                        placeholder={templateInput.placeholder}
                        disabled={isBusy}
                      />
                    )}
                    <p className="text-xs text-muted-foreground">{templateInput.helpText}</p>
                  </FormField>
                )
              })}
              <div className="flex justify-end">
                <Button type="button" variant="secondary" onClick={submitTemplateInput} disabled={isBusy || !canSubmitTemplateInputs}>
                  Gerar
                </Button>
              </div>
            </div>
          ) : null}

          {draftQuery.isLoading && canRenderDraft ? (
            <div className="flex items-center gap-2 rounded-md border border-border bg-background p-3 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Gerando prompt
            </div>
          ) : null}

          {draftQuery.error ? (
            <div className="grid gap-3 rounded-md border border-danger-border bg-danger-soft p-3 text-sm text-danger-soft-foreground">
              <div className="flex items-start gap-2">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                <span>{getErrorMessage(draftQuery.error)}</span>
              </div>
              <Button type="button" variant="secondary" size="sm" onClick={() => draftQuery.refetch()}>
                Tentar novamente
              </Button>
            </div>
          ) : null}

          {templateInputs.length > 0 && !activeDraft && !draftQuery.isLoading && !draftQuery.error ? (
            <div className="rounded-md border border-dashed border-border p-3 text-sm text-muted-foreground">
              Informe {templateInputLabel} para gerar a previa do prompt filho.
            </div>
          ) : null}

          {activeDraft ? (
            <form className="grid h-full min-h-0 grid-rows-[auto_auto_minmax(0,1fr)] gap-4">
              <input type="hidden" {...form.register('content')} value={editorContent} />

              <FormField label="Titulo" htmlFor="generated-prompt-title" error={form.formState.errors.title?.message}>
                <Input
                  id="generated-prompt-title"
                  {...titleField}
                  ref={(element) => {
                    titleField.ref(element)
                    titleInputRef.current = element
                  }}
                />
              </FormField>

              <div className="grid gap-4 sm:grid-cols-2">
                <FormField label="Agente" htmlFor="generated-prompt-agent">
                  <Select id="generated-prompt-agent" {...form.register('targetAgent')}>
                    {AGENT_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </Select>
                </FormField>

                <FormField label="Tipo" htmlFor="generated-prompt-kind">
                  <Select id="generated-prompt-kind" {...form.register('kind')}>
                    {KIND_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </Select>
                </FormField>
              </div>

              <div className="grid min-h-0 grid-rows-[minmax(0,1fr)_auto] gap-1.5">
                <PromptEditor
                  workingDirectoryId={activeDraft.workingDirectoryId}
                  value={editorContent}
                  onChange={(value, nextMentions) => {
                    setContentOverride(value)
                    form.setValue('content', value, { shouldDirty: true, shouldValidate: true })
                    setEditorMentions(nextMentions)
                  }}
                  className="grid h-full min-h-0 grid-rows-[auto_minmax(0,1fr)]"
                  contentClassName="min-h-0 overflow-auto"
                  editorClassName="min-h-full"
                />
                {form.formState.errors.content?.message ? (
                  <p className="text-xs font-medium text-destructive">
                    {form.formState.errors.content.message}
                  </p>
                ) : null}
              </div>
            </form>
          ) : null}
        </div>

        <div className="flex flex-wrap justify-end gap-2 border-t border-border p-4">
          <Button type="button" variant="ghost" onClick={requestClose} disabled={isBusy}>
            Cancelar
          </Button>
          <Button type="button" variant="secondary" onClick={() => submit(true)} disabled={!activeDraft || isBusy}>
            {createMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Copy className="h-4 w-4" />}
            Criar e copiar
          </Button>
          <Button type="button" onClick={() => submit(false)} disabled={!activeDraft || isBusy}>
            {createMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
            Criar filho
          </Button>
        </div>
      </div>
    </div>
  )
}
