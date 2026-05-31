import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  FileText,
  GitBranch,
  Loader2,
  PlayCircle,
  RotateCcw,
  StickyNote,
} from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { queryKeys } from '@/api/query-keys'
import type { Workflow, WorkflowActor } from '@/api/schemas'
import {
  addWorkflowNote,
  advancePhase,
  changeActor,
  completeWorkflow,
  getWorkflow,
  getWorkflowTemplate,
  reopenWorkflow,
  setPhase,
  startWorkflow,
  updateTaskPhases,
  type WorkflowPhaseInput,
} from '@/api/workflow'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { ActorBadge, PhaseBadge } from './badges'
import { ACTOR_OPTIONS, WORKFLOW_STATUS_LABELS, formatRelativeTime } from './constants'
import { PhaseEditor } from './phase-editor'
import { WorkflowTimeline } from './workflow-timeline'

type WorkflowPanelProps = {
  promptId: string
  onNavigateTab: (tab: 'prompt' | 'linked-plan' | 'children') => void
}

export function WorkflowPanel({ promptId, onNavigateTab }: WorkflowPanelProps) {
  const queryClient = useQueryClient()
  const [note, setNote] = useState('')
  const [editingPhases, setEditingPhases] = useState(false)
  const [initialPhaseIndex, setInitialPhaseIndex] = useState(0)

  const workflowQuery = useQuery({
    queryKey: queryKeys.workflow.detail(promptId),
    queryFn: () => getWorkflow(promptId),
  })
  const templateQuery = useQuery({
    queryKey: queryKeys.workflow.template(),
    queryFn: getWorkflowTemplate,
  })

  const applyWorkflow = (updated: Workflow) => {
    queryClient.setQueryData(queryKeys.workflow.detail(promptId), updated)
    void queryClient.invalidateQueries({ queryKey: queryKeys.workflow.all })
    void queryClient.invalidateQueries({ queryKey: queryKeys.prompts.all })
  }

  const onError = (error: unknown) => toast.error(getErrorMessage(error))

  const action = useMutation({
    mutationFn: (run: () => Promise<Workflow>) => run(),
    onSuccess: applyWorkflow,
    onError,
  })
  const noteMutation = useMutation({
    mutationFn: (value: string) => addWorkflowNote(promptId, value),
    onSuccess: (updated) => {
      applyWorkflow(updated)
      setNote('')
    },
    onError,
  })
  const phasesMutation = useMutation({
    mutationFn: (payload: { phases: WorkflowPhaseInput[]; rowVersion: string }) =>
      updateTaskPhases(promptId, payload.phases, payload.rowVersion),
    onSuccess: (updated) => {
      applyWorkflow(updated)
      setEditingPhases(false)
      toast.success('Fases atualizadas.')
    },
    onError,
  })

  if (workflowQuery.isLoading) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-[#d9dfd5] bg-white p-4 text-sm text-[#66746b]">
        <Loader2 className="h-4 w-4 animate-spin" />
        Carregando fluxo
      </div>
    )
  }

  const workflow = workflowQuery.data ?? null

  if (!workflow) {
    const templatePhases = templateQuery.data?.phases ?? []
    return (
      <div className="grid gap-3 rounded-lg border border-[#d9dfd5] bg-white p-6">
        <h2 className="text-base font-semibold text-[#172126]">Fluxo não iniciado</h2>
        <p className="text-sm text-[#66746b]">Inicie o fluxo para acompanhar fases e a timeline desta tarefa.</p>
        <div className="flex flex-wrap items-end gap-2">
          <label className="grid gap-1.5 text-sm font-medium text-[#253035]">
            Começar na fase
            <Select
              value={String(initialPhaseIndex)}
              onChange={(event) => setInitialPhaseIndex(Number(event.target.value))}
              className="w-56"
            >
              {templatePhases.map((phase, index) => (
                <option key={phase.id} value={index}>
                  {phase.name}
                </option>
              ))}
            </Select>
          </label>
          <Button
            type="button"
            onClick={() => action.mutate(() => startWorkflow(promptId, initialPhaseIndex))}
            disabled={action.isPending}
          >
            {action.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <PlayCircle className="h-4 w-4" />}
            Iniciar fluxo
          </Button>
        </div>
      </div>
    )
  }

  const phases = [...workflow.phases].sort((a, b) => a.orderIndex - b.orderIndex)
  const currentIndex = phases.findIndex((phase) => phase.id === workflow.currentPhaseId)
  const previousPhase = currentIndex > 0 ? phases[currentIndex - 1] : null
  const nextPhase = currentIndex >= 0 && currentIndex < phases.length - 1 ? phases[currentIndex + 1] : null
  const isActive = workflow.status === 'Active'
  const busy = action.isPending || noteMutation.isPending || phasesMutation.isPending
  const rowVersion = workflow.rowVersion

  const advanceOrComplete = async () => {
    const latest = await getWorkflow(promptId)
    if (!latest || latest.status !== 'Active') {
      throw new Error('Recarregue o fluxo antes de avançar.')
    }

    const latestPhases = [...latest.phases].sort((a, b) => a.orderIndex - b.orderIndex)
    const latestCurrentIndex = latestPhases.findIndex((phase) => phase.id === latest.currentPhaseId)
    const hasNextPhase = latestCurrentIndex >= 0 && latestCurrentIndex < latestPhases.length - 1
    if (!hasNextPhase) {
      return completeWorkflow(promptId, latest.rowVersion)
    }

    return advancePhase(promptId, latest.rowVersion)
  }

  return (
    <div className="grid gap-4">
      <div className="grid gap-3 rounded-lg border border-[#d9dfd5] bg-white p-4">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm text-[#66746b]">Agora:</span>
          {workflow.currentPhaseName ? (
            <PhaseBadge name={workflow.currentPhaseName} color={workflow.currentPhaseColor} />
          ) : null}
          {workflow.currentActor ? <ActorBadge actor={workflow.currentActor} highlight /> : null}
          <span className="rounded-md bg-[#eef2eb] px-2 py-1 text-xs font-medium text-[#425048]">
            {WORKFLOW_STATUS_LABELS[workflow.status]}
          </span>
          {workflow.enteredCurrentPhaseAtUtc ? (
            <span className="text-xs text-[#8a958c]">nesta fase {formatRelativeTime(workflow.enteredCurrentPhaseAtUtc)}</span>
          ) : null}
        </div>

        {isActive ? (
          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              variant="secondary"
              size="sm"
              disabled={busy || !previousPhase}
              onClick={() => previousPhase && action.mutate(() => setPhase(promptId, previousPhase.id, rowVersion))}
            >
              <ArrowLeft className="h-4 w-4" />
              Voltar
            </Button>
            <Button
              type="button"
              size="sm"
              disabled={busy}
              onClick={() => action.mutate(advanceOrComplete)}
            >
              {nextPhase ? <ArrowRight className="h-4 w-4" /> : <CheckCircle2 className="h-4 w-4" />}
              {nextPhase ? 'Avançar' : 'Concluir'}
            </Button>
            <label className="flex items-center gap-1.5 text-xs text-[#66746b]">
              Mudar fase
              <Select
                value={workflow.currentPhaseId ?? ''}
                onChange={(event) => action.mutate(() => setPhase(promptId, event.target.value, rowVersion))}
                disabled={busy}
                className="w-44"
              >
                {phases.map((phase) => (
                  <option key={phase.id} value={phase.id}>
                    {phase.name}
                  </option>
                ))}
              </Select>
            </label>
            <label className="flex items-center gap-1.5 text-xs text-[#66746b]">
              Responsável
              <Select
                value={workflow.currentActor ?? ''}
                onChange={(event) => action.mutate(() => changeActor(promptId, event.target.value as WorkflowActor, rowVersion))}
                disabled={busy}
                className="w-32"
              >
                {ACTOR_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </Select>
            </label>
            <Button
              type="button"
              variant="secondary"
              size="sm"
              disabled={busy}
              onClick={() => action.mutate(() => completeWorkflow(promptId, rowVersion))}
            >
              <CheckCircle2 className="h-4 w-4" />
              Concluir
            </Button>
          </div>
        ) : (
          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              size="sm"
              disabled={busy}
              onClick={() => action.mutate(() => reopenWorkflow(promptId, rowVersion))}
            >
              <RotateCcw className="h-4 w-4" />
              Reabrir
            </Button>
          </div>
        )}

        <div className="flex flex-wrap items-center gap-2 border-t border-[#eef2eb] pt-3">
          <span className="text-xs text-[#8a958c]">Atalhos:</span>
          <Button type="button" variant="ghost" size="sm" onClick={() => onNavigateTab('children')}>
            <GitBranch className="h-4 w-4" />
            Prompts filhos
          </Button>
          <Button type="button" variant="ghost" size="sm" onClick={() => onNavigateTab('linked-plan')}>
            <FileText className="h-4 w-4" />
            Plano vinculado
          </Button>
          <div className="flex-1" />
          <Button type="button" variant="ghost" size="sm" onClick={() => setEditingPhases((value) => !value)}>
            {editingPhases ? 'Fechar edição' : 'Editar fases'}
          </Button>
        </div>

        {editingPhases ? (
          <PhaseEditor
            initialPhases={phases.map((phase) => ({
              id: phase.id,
              name: phase.name,
              defaultActor: phase.defaultActor,
              color: phase.color,
            }))}
            saving={phasesMutation.isPending}
            onCancel={() => setEditingPhases(false)}
            onSave={(updatedPhases) => phasesMutation.mutate({ phases: updatedPhases, rowVersion })}
          />
        ) : null}
      </div>

      <div className="grid gap-2 rounded-lg border border-[#d9dfd5] bg-white p-4">
        <label className="text-sm font-medium text-[#253035]" htmlFor="workflow-note">
          Adicionar nota
        </label>
        <Textarea
          id="workflow-note"
          value={note}
          onChange={(event) => setNote(event.target.value)}
          placeholder="Ex.: cole aqui o feedback do Codex"
          rows={3}
        />
        <div className="flex justify-end">
          <Button
            type="button"
            size="sm"
            disabled={note.trim().length === 0 || noteMutation.isPending}
            onClick={() => noteMutation.mutate(note.trim())}
          >
            <StickyNote className="h-4 w-4" />
            Salvar nota
          </Button>
        </div>
      </div>

      <div className="grid gap-3 rounded-lg border border-[#d9dfd5] bg-white p-4">
        <h2 className="text-base font-semibold text-[#172126]">Timeline</h2>
        <WorkflowTimeline events={workflow.events} />
      </div>
    </div>
  )
}
