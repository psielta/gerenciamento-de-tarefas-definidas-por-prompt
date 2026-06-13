import { useQuery } from '@tanstack/react-query'
import { Clock, FileText, GitBranch, MessageSquarePlus, MessageSquareText, Terminal } from 'lucide-react'
import { lazy, Suspense, useState } from 'react'
import { getPrompt } from '@/api/prompts'
import { queryKeys } from '@/api/query-keys'
import { getWorkflow } from '@/api/workflow'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { LinkedDocumentsPanel } from '@/features/linked-documents/linked-documents-panel'
import { currentPhaseRole, isReviewPhaseRole } from '@/features/workflow/constants'
import { ReviewVerdictDialog } from '@/features/workflow/review-verdict-dialog'
import { WorkflowPanel } from '@/features/workflow/workflow-panel'
import type { DetailTab } from './prompt-detail-search'
import { getTerminalCapabilities } from '@/api/terminals'
import { PromptChildrenPanel } from './prompt-children-panel'
const TerminalsPanel = lazy(() =>
  import('./terminals-panel').then((module) => ({ default: module.TerminalsPanel })),
)
import { PromptForm } from './prompt-form'
import { PromptVersions } from './prompt-versions'

type PromptDetailViewProps = {
  workspaceId: string
  promptId: string
  activeTab: DetailTab
  onTabChange: (tab: DetailTab) => void
  onDeleted?: () => void
}

export function PromptDetailView({ workspaceId, promptId, activeTab, onTabChange, onDeleted }: PromptDetailViewProps) {
  const promptQuery = useQuery({
    queryKey: queryKeys.prompts.detail(promptId),
    queryFn: () => getPrompt(promptId),
  })

  const workflowQuery = useQuery({
    queryKey: queryKeys.workflow.detail(promptId),
    queryFn: () => getWorkflow(promptId),
  })

  const terminalCapabilitiesQuery = useQuery({
    queryKey: queryKeys.terminals.capabilities(),
    queryFn: getTerminalCapabilities,
    staleTime: 60_000,
  })
  const terminalsEnabled = terminalCapabilitiesQuery.data?.enabled ?? false
  const workflow = workflowQuery.data ?? null
  const reviewRole = workflow ? currentPhaseRole(workflow.phases, workflow.currentPhaseId) : null
  const canAddVerdict = workflow?.status === 'Active' && isReviewPhaseRole(reviewRole)

  const [showVerdict, setShowVerdict] = useState(false)

  return (
    <div className="grid gap-4">
      {promptQuery.data?.taskNumber || canAddVerdict || workflow?.reviewVerdictSourcePhaseName ? (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex min-w-0 items-center gap-2">
            {promptQuery.data?.taskNumber ? (
              <>
                <Badge variant="blue">{promptQuery.data.taskNumber}</Badge>
                <span className="truncate text-sm text-muted-foreground">{promptQuery.data.title}</span>
              </>
            ) : null}
            {workflow?.reviewVerdictSourcePhaseName ? (
              <Badge variant="amber" title={`Trabalhando no veredito de ${workflow.reviewVerdictSourcePhaseName}`}>
                ⮌ {workflow.reviewVerdictSourcePhaseName}
              </Badge>
            ) : null}
          </div>
          {canAddVerdict ? (
            <Button type="button" size="sm" onClick={() => setShowVerdict(true)}>
              <MessageSquarePlus className="h-4 w-4" />
              Adicionar nota de revisão
            </Button>
          ) : null}
        </div>
      ) : null}

      <div className="flex flex-wrap gap-2 rounded-lg border border-border bg-card p-2">
        <Button
          type="button"
          variant={activeTab === 'prompt' ? 'default' : 'ghost'}
          size="sm"
          aria-pressed={activeTab === 'prompt'}
          onClick={() => onTabChange('prompt')}
        >
          <MessageSquareText className="h-4 w-4" />
          Prompt
        </Button>
        <Button
          type="button"
          variant={activeTab === 'timeline' ? 'default' : 'ghost'}
          size="sm"
          aria-pressed={activeTab === 'timeline'}
          onClick={() => onTabChange('timeline')}
        >
          <Clock className="h-4 w-4" />
          Timeline
        </Button>
        <Button
          type="button"
          variant={activeTab === 'linked-plan' ? 'default' : 'ghost'}
          size="sm"
          aria-pressed={activeTab === 'linked-plan'}
          onClick={() => onTabChange('linked-plan')}
        >
          <FileText className="h-4 w-4" />
          Plano vinculado
        </Button>
        <Button
          type="button"
          variant={activeTab === 'children' ? 'default' : 'ghost'}
          size="sm"
          aria-pressed={activeTab === 'children'}
          onClick={() => onTabChange('children')}
        >
          <GitBranch className="h-4 w-4" />
          Prompts filhos
        </Button>
        {terminalsEnabled ? (
          <Button
            type="button"
            variant={activeTab === 'terminals' ? 'default' : 'ghost'}
            size="sm"
            aria-pressed={activeTab === 'terminals'}
            onClick={() => onTabChange('terminals')}
          >
            <Terminal className="h-4 w-4" />
            Terminais
          </Button>
        ) : null}
      </div>

      {activeTab === 'prompt' ? (
        <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_18rem]">
          <PromptForm workingDirectoryId={workspaceId} promptId={promptId} onDeleted={onDeleted} />
          <PromptVersions promptId={promptId} />
        </div>
      ) : null}

      {activeTab === 'timeline' ? <WorkflowPanel promptId={promptId} onNavigateTab={onTabChange} /> : null}

      {activeTab === 'linked-plan' ? <LinkedDocumentsPanel promptId={promptId} /> : null}

      {activeTab === 'children' ? (
        <PromptChildrenPanel workingDirectoryId={workspaceId} parentPromptId={promptId} />
      ) : null}

      {terminalsEnabled ? (
        <Suspense fallback={<div className="text-sm text-muted-foreground">Carregando terminais...</div>}>
          <div className={activeTab === 'terminals' ? undefined : 'hidden'}>
            <TerminalsPanel promptId={promptId} />
          </div>
        </Suspense>
      ) : null}

      {showVerdict ? (
        <ReviewVerdictDialog promptId={promptId} onClose={() => setShowVerdict(false)} />
      ) : null}
    </div>
  )
}
