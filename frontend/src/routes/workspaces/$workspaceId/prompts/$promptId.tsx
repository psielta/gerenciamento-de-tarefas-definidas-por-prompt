import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { PromptDetailView } from '@/features/prompts/prompt-detail'
import { type DetailTab, validatePromptDetailSearch } from '@/features/prompts/prompt-detail-search'

export const Route = createFileRoute('/workspaces/$workspaceId/prompts/$promptId')({
  validateSearch: validatePromptDetailSearch,
  component: PromptDetailPage,
})

function PromptDetailPage() {
  const { workspaceId, promptId } = Route.useParams()
  const { tab } = Route.useSearch()
  const navigate = useNavigate()
  const activeTab = tab ?? 'prompt'

  const setActiveTab = (nextTab: DetailTab) => {
    void navigate({
      to: '/workspaces/$workspaceId/prompts/$promptId',
      params: { workspaceId, promptId },
      search: nextTab === 'prompt' ? {} : { tab: nextTab },
      replace: true,
    })
  }

  return <PromptDetailView workspaceId={workspaceId} promptId={promptId} activeTab={activeTab} onTabChange={setActiveTab} />
}
