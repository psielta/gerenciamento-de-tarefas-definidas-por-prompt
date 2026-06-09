import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Archive, ArrowRight, CheckCircle2, Copy, FastForward, FolderGit2, Link2, Loader2, MessageSquarePlus, PlayCircle, RefreshCw, X } from 'lucide-react'
import { useState, type DragEvent } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { getPrompt, updatePromptStatus } from '@/api/prompts'
import { listPromptTemplates } from '@/api/prompt-templates'
import { queryKeys } from '@/api/query-keys'
import type { PromptTemplate, TaskSummary, WorkflowPhaseRole } from '@/api/schemas'
import { advancePhase, completeWorkflow, getWorkflow, setPhase, startWorkflow } from '@/api/workflow'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { GeneratePromptMenu } from '@/features/linked-documents/generate-prompt-menu'
import { ActorBadge, PhaseBadge } from './badges'
import {
  APPROVE_ADVANCE_BY_ROLE,
  IMPLEMENTATION_TEMPLATE_KEYS,
  IMPLEMENTATION_REVIEW_ACTION,
  PLANNING_REVIEW_ACTION,
  PLAN_REVIEW_TEMPLATE_KEYS,
  PLAN_REVIEW_IMPLEMENTATION_ACTION,
  findPhaseByRole,
  formatRelativeTime,
  isReviewPhaseRole,
  RE_REVIEW_TEMPLATE_BY_ROLE,
} from './constants'
import { ReviewVerdictDialog } from './review-verdict-dialog'

type TaskCardProps = {
  task: TaskSummary
  dragging?: boolean
  moveDisabled?: boolean
  onDragStart?: (task: TaskSummary, event: DragEvent<HTMLDivElement>) => void
  onDragEnd?: () => void
  onOpen?: (task: TaskSummary) => void
  onGenerate?: (task: TaskSummary, template: PromptTemplate) => void
  onLinkPlan?: (task: TaskSummary) => void
}

const WORKSPACE_NAME_MAX_LENGTH = 36

function formatWorkspaceName(name: string) {
  const normalizedName = name.trim()
  if (normalizedName.length <= WORKSPACE_NAME_MAX_LENGTH) {
    return normalizedName
  }

  return `${normalizedName.slice(0, WORKSPACE_NAME_MAX_LENGTH - 3).trimEnd()}...`
}

export function TaskCard({ task, dragging, moveDisabled, onDragStart, onDragEnd, onOpen, onGenerate, onLinkPlan }: TaskCardProps) {
  const queryClient = useQueryClient()
  const [showVerdict, setShowVerdict] = useState(false)
  const [showPlanReviewChoice, setShowPlanReviewChoice] = useState(false)
  const [showImplementationChoice, setShowImplementationChoice] = useState(false)

  const currentPhase = task.phases.find((phase) => phase.id === task.currentPhaseId)
  const currentRole = currentPhase?.role ?? null
  const reReviewKey = currentRole ? RE_REVIEW_TEMPLATE_BY_ROLE[currentRole] : undefined
  const approveTarget = currentRole ? APPROVE_ADVANCE_BY_ROLE[currentRole] : undefined
  const planReviewAction = currentRole === 'Planning' ? PLANNING_REVIEW_ACTION : undefined
  const implementationAction = currentRole === 'PlanReview' ? PLAN_REVIEW_IMPLEMENTATION_ACTION : undefined
  const implementationReviewAction = currentRole === 'Implementation' ? IMPLEMENTATION_REVIEW_ACTION : undefined

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    void queryClient.invalidateQueries({ queryKey: queryKeys.workflow.detail(task.promptId) })
    void queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
  }

  // Mesma queryKey do GeneratePromptMenu: o React Query deduplica, sem requisição extra.
  const templatesQuery = useQuery({
    queryKey: queryKeys.promptTemplates.all,
    queryFn: listPromptTemplates,
    enabled: Boolean(task.linkedDocumentId && (reReviewKey || planReviewAction || implementationAction || implementationReviewAction)),
  })
  const reReviewTemplate = reReviewKey ? templatesQuery.data?.find((template) => template.key === reReviewKey) : undefined
  const implementationReviewTemplate = implementationReviewAction
    ? templatesQuery.data?.find((template) => template.key === implementationReviewAction.templateKey)
    : undefined
  const planReviewTemplates = PLAN_REVIEW_TEMPLATE_KEYS.map((key) =>
    templatesQuery.data?.find((template) => template.key === key),
  )
  const [basicPlanReviewTemplate, parentPlanReviewTemplate] = planReviewTemplates
  const implementationTemplates = IMPLEMENTATION_TEMPLATE_KEYS.map((key) =>
    templatesQuery.data?.find((template) => template.key === key),
  )
  const [basicImplementationTemplate, worktreeImplementationTemplate] = implementationTemplates

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

  // O resumo do board nao traz `content`: busca o prompt completo sob demanda,
  // reaproveitando o cache de detalhe do React Query quando ainda fresco.
  const copyPrompt = useMutation({
    mutationFn: async () => {
      const prompt = await queryClient.fetchQuery({
        queryKey: queryKeys.prompts.detail(task.promptId),
        queryFn: () => getPrompt(task.promptId),
        staleTime: 15_000,
      })

      if (!navigator.clipboard?.writeText) {
        throw new Error('Área de transferência indisponível neste navegador.')
      }

      await navigator.clipboard.writeText(prompt.content)
    },
    onSuccess: () => toast.success('Prompt copiado.'),
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  // Salto direto para fases aprovadas que nao dependem da criacao de prompt filho.
  const approveAdvance = useMutation({
    mutationFn: async (targetRole: WorkflowPhaseRole) => {
      const workflow = await getWorkflow(task.promptId)
      if (!workflow || workflow.status !== 'Active') {
        throw new Error('Recarregue o quadro antes de avançar esta tarefa.')
      }

      const target = findPhaseByRole(workflow.phases, targetRole)
      if (!target) {
        throw new Error('Esta tarefa não possui a fase de destino.')
      }

      if (workflow.currentPhaseId === target.id) {
        return workflow
      }

      return setPhase(task.promptId, target.id, workflow.rowVersion)
    },
    onSuccess: invalidate,
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const isHumanTurn = task.currentActor === 'Human'
  const isLastPhase = task.workflowStatus === 'Active' && currentPhase
    ? currentPhase.orderIndex === Math.max(...task.phases.map((phase) => phase.orderIndex))
    : false
  const isBusy = start.isPending || advanceOrComplete.isPending || archive.isPending || approveAdvance.isPending
  const hasPlanReviewTarget = Boolean(
    planReviewAction && findPhaseByRole(task.phases, planReviewAction.targetRole),
  )
  const hasImplementationTarget = Boolean(
    implementationAction && findPhaseByRole(task.phases, implementationAction.targetRole),
  )
  const hasImplementationReviewTarget = Boolean(
    implementationReviewAction && findPhaseByRole(task.phases, implementationReviewAction.targetRole),
  )
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
      <button
        type="button"
        onClick={() => onOpen?.(task)}
        className="grid min-w-0 gap-2 text-left"
        aria-label="Abrir detalhes do prompt"
      >
        {linkContent}
      </button>

      <div className="flex flex-wrap items-center gap-1.5">
        {task.currentPhaseName ? (
          <PhaseBadge name={task.currentPhaseName} color={task.currentPhaseColor} />
        ) : (
          <span className="rounded-md bg-muted px-2 py-1 text-xs font-medium text-muted-foreground">Fluxo não iniciado</span>
        )}
        {task.currentPhaseIteration > 1 ? (
          <Badge variant="blue">re-review #{task.currentPhaseIteration}</Badge>
        ) : null}
        {task.reviewVerdictSourcePhaseName ? (
          <Badge variant="amber" title={`Trabalhando no veredito de ${task.reviewVerdictSourcePhaseName}`}>
            ⮌ {task.reviewVerdictSourcePhaseName}
          </Badge>
        ) : null}
        {task.currentActor ? <ActorBadge actor={task.currentActor} highlight /> : null}
      </div>

      {task.linkedDocumentId ? (
        <div className="flex">
          <GeneratePromptMenu
            linkedDocumentId={task.linkedDocumentId}
            pullRequestReference={task.pullRequestReference}
            onSelectTemplate={(template) => onGenerate?.(task, template)}
          />
        </div>
      ) : (
        <div className="flex">
          <Button type="button" variant="secondary" size="sm" onClick={() => onLinkPlan?.(task)}>
            <Link2 className="h-4 w-4" />
            Vincular plano
          </Button>
        </div>
      )}

      {task.workflowStatus === 'Active' && isReviewPhaseRole(currentRole) ? (
        <div className="flex">
          <Button type="button" size="sm" onClick={() => setShowVerdict(true)} disabled={isBusy}>
            <MessageSquarePlus className="h-4 w-4" />
            Adicionar nota de revisão
          </Button>
        </div>
      ) : null}

      {task.workflowStatus === 'Active' && task.linkedDocumentId && reReviewTemplate ? (
        <div className="flex">
          <Button type="button" size="sm" onClick={() => onGenerate?.(task, reReviewTemplate)} disabled={isBusy}>
            <RefreshCw className="h-4 w-4" />
            {reReviewTemplate.displayName}
          </Button>
        </div>
      ) : null}

      {task.workflowStatus === 'Active' && task.linkedDocumentId && planReviewAction && hasPlanReviewTarget ? (
        <div className="flex">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => {
              if (!basicPlanReviewTemplate || !parentPlanReviewTemplate) {
                toast.error('Templates de revisão indisponíveis.')
                return
              }

              setShowPlanReviewChoice(true)
            }}
            disabled={isBusy || templatesQuery.isLoading}
          >
            <FastForward className="h-4 w-4" />
            {planReviewAction.label}
          </Button>
        </div>
      ) : null}

      {task.workflowStatus === 'Active' && approveTarget && findPhaseByRole(task.phases, approveTarget.targetRole) ? (
        <div className="flex">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => approveAdvance.mutate(approveTarget.targetRole)}
            disabled={isBusy}
          >
            <FastForward className="h-4 w-4" />
            {approveTarget.label}
          </Button>
        </div>
      ) : null}

      {task.workflowStatus === 'Active' && task.linkedDocumentId && implementationAction && hasImplementationTarget ? (
        <div className="flex">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => {
              if (!basicImplementationTemplate || !worktreeImplementationTemplate) {
                toast.error('Templates de implementação indisponíveis.')
                return
              }

              setShowImplementationChoice(true)
            }}
            disabled={isBusy || templatesQuery.isLoading}
          >
            <FastForward className="h-4 w-4" />
            {implementationAction.label}
          </Button>
        </div>
      ) : null}

      {task.workflowStatus === 'Active' && task.linkedDocumentId && implementationReviewAction && hasImplementationReviewTarget ? (
        <div className="flex">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => {
              if (!implementationReviewTemplate) {
                toast.error('Template de revisão de PR indisponível.')
                return
              }

              onGenerate?.(task, implementationReviewTemplate)
            }}
            disabled={isBusy || templatesQuery.isLoading}
          >
            <FastForward className="h-4 w-4" />
            {implementationReviewAction.label}
          </Button>
        </div>
      ) : null}

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
            onClick={() => copyPrompt.mutate()}
            disabled={copyPrompt.isPending}
            title="Copiar prompt"
            aria-label="Copiar prompt"
          >
            {copyPrompt.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Copy className="h-4 w-4" />}
          </Button>
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

      {showVerdict ? (
        <ReviewVerdictDialog promptId={task.promptId} onClose={() => setShowVerdict(false)} />
      ) : null}

      {showPlanReviewChoice && basicPlanReviewTemplate && parentPlanReviewTemplate ? (
        <div
          className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 pt-16"
          role="dialog"
          aria-modal="true"
          aria-labelledby="plan-review-choice-title"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              setShowPlanReviewChoice(false)
            }
          }}
        >
          <div className="grid w-full max-w-lg gap-4 rounded-lg border border-border bg-card p-4 shadow-xl">
            <div className="flex items-start justify-between gap-3">
              <h2 id="plan-review-choice-title" className="text-sm font-semibold text-foreground">
                Escolher revisão
              </h2>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-8 w-8 shrink-0 text-muted-foreground"
                onClick={() => setShowPlanReviewChoice(false)}
                aria-label="Fechar"
              >
                <X className="h-4 w-4" />
              </Button>
            </div>

            <div className="grid gap-2">
              {[basicPlanReviewTemplate, parentPlanReviewTemplate].map((template) => (
                <button
                  key={template.key}
                  type="button"
                  className="grid min-w-0 gap-1 rounded-md border border-border bg-background px-3 py-2 text-left text-sm transition-colors hover:border-ring hover:bg-muted focus:outline-none focus:ring-2 focus:ring-ring"
                  onClick={() => {
                    setShowPlanReviewChoice(false)
                    onGenerate?.(task, template)
                  }}
                >
                  <span className="font-medium text-foreground">{template.displayName}</span>
                  <span className="text-xs text-muted-foreground">{template.description}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      ) : null}

      {showImplementationChoice && basicImplementationTemplate && worktreeImplementationTemplate ? (
        <div
          className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 pt-16"
          role="dialog"
          aria-modal="true"
          aria-labelledby="implementation-choice-title"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              setShowImplementationChoice(false)
            }
          }}
        >
          <div className="grid w-full max-w-lg gap-4 rounded-lg border border-border bg-card p-4 shadow-xl">
            <div className="flex items-start justify-between gap-3">
              <h2 id="implementation-choice-title" className="text-sm font-semibold text-foreground">
                Escolher implementação
              </h2>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-8 w-8 shrink-0 text-muted-foreground"
                onClick={() => setShowImplementationChoice(false)}
                aria-label="Fechar"
              >
                <X className="h-4 w-4" />
              </Button>
            </div>

            <div className="grid gap-2">
              {[basicImplementationTemplate, worktreeImplementationTemplate].map((template) => (
                <button
                  key={template.key}
                  type="button"
                  className="grid min-w-0 gap-1 rounded-md border border-border bg-background px-3 py-2 text-left text-sm transition-colors hover:border-ring hover:bg-muted focus:outline-none focus:ring-2 focus:ring-ring"
                  onClick={() => {
                    setShowImplementationChoice(false)
                    onGenerate?.(task, template)
                  }}
                >
                  <span className="font-medium text-foreground">{template.displayName}</span>
                  <span className="text-xs text-muted-foreground">{template.description}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
