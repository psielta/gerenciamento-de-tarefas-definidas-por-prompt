import { useQuery } from '@tanstack/react-query'
import { Clock, FileText, GitBranch, MessageSquareText } from 'lucide-react'
import { getPrompt } from '@/api/prompts'
import { queryKeys } from '@/api/query-keys'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { LinkedDocumentsPanel } from '@/features/linked-documents/linked-documents-panel'
import { WorkflowPanel } from '@/features/workflow/workflow-panel'
import type { DetailTab } from './prompt-detail-search'
import { PromptChildrenPanel } from './prompt-children-panel'
import { PromptForm } from './prompt-form'
import { PromptVersions } from './prompt-versions'

type PromptDetailViewProps = {
  workspaceId: string
  promptId: string
  activeTab: DetailTab
  onTabChange: (tab: DetailTab) => void
}

export function PromptDetailView({ workspaceId, promptId, activeTab, onTabChange }: PromptDetailViewProps) {
  const promptQuery = useQuery({
    queryKey: queryKeys.prompts.detail(promptId),
    queryFn: () => getPrompt(promptId),
  })

  return (
    <div className="grid gap-4">
      {promptQuery.data?.taskNumber ? (
        <div className="flex items-center gap-2">
          <Badge variant="blue">{promptQuery.data.taskNumber}</Badge>
          <span className="truncate text-sm text-muted-foreground">{promptQuery.data.title}</span>
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
      </div>

      {activeTab === 'prompt' ? (
        <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_18rem]">
          <PromptForm workingDirectoryId={workspaceId} promptId={promptId} />
          <PromptVersions promptId={promptId} />
        </div>
      ) : null}

      {activeTab === 'timeline' ? <WorkflowPanel promptId={promptId} onNavigateTab={onTabChange} /> : null}

      {activeTab === 'linked-plan' ? <LinkedDocumentsPanel promptId={promptId} /> : null}

      {activeTab === 'children' ? (
        <PromptChildrenPanel workingDirectoryId={workspaceId} parentPromptId={promptId} />
      ) : null}
    </div>
  )
}
