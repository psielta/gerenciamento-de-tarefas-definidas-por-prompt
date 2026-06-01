import { useQuery } from '@tanstack/react-query'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { Loader2 } from 'lucide-react'
import { getErrorMessage } from '@/api/client'
import { getPromptByTaskNumber } from '@/api/prompts'
import { queryKeys } from '@/api/query-keys'
import { PromptDetailView } from '@/features/prompts/prompt-detail'
import { type DetailTab, validatePromptDetailSearch } from '@/features/prompts/prompt-detail-search'

export const Route = createFileRoute('/workspaces/$workspaceId/tasks/$taskNumber')({
  validateSearch: validatePromptDetailSearch,
  component: PromptTaskNumberPage,
})

function PromptTaskNumberPage() {
  const { workspaceId, taskNumber } = Route.useParams()
  const { tab } = Route.useSearch()
  const navigate = useNavigate()
  const activeTab = tab ?? 'prompt'
  const promptQuery = useQuery({
    queryKey: queryKeys.prompts.byTaskNumber(workspaceId, taskNumber),
    queryFn: () => getPromptByTaskNumber(workspaceId, taskNumber),
  })

  const setActiveTab = (nextTab: DetailTab) => {
    void navigate({
      to: '/workspaces/$workspaceId/tasks/$taskNumber',
      params: { workspaceId, taskNumber },
      search: nextTab === 'prompt' ? {} : { tab: nextTab },
      replace: true,
    })
  }

  if (promptQuery.isLoading) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-border bg-card p-4 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        Carregando tarefa
      </div>
    )
  }

  if (promptQuery.isError || !promptQuery.data) {
    return (
      <div className="rounded-lg border border-danger-border bg-danger-soft p-4 text-sm text-danger-soft-foreground">
        {promptQuery.error ? getErrorMessage(promptQuery.error) : 'Tarefa nao encontrada.'}
      </div>
    )
  }

  return (
    <PromptDetailView
      workspaceId={workspaceId}
      promptId={promptQuery.data.id}
      activeTab={activeTab}
      onTabChange={setActiveTab}
    />
  )
}
