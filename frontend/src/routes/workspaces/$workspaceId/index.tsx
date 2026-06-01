import { createFileRoute } from '@tanstack/react-router'
import { PromptList } from '@/features/prompts/prompt-list'
import { WorkspaceTaskNumberSettings } from '@/features/workspaces/workspace-task-number-settings'

export const Route = createFileRoute('/workspaces/$workspaceId/')({
  component: WorkspaceIndexPage,
})

function WorkspaceIndexPage() {
  const { workspaceId } = Route.useParams()

  return (
    <div className="grid gap-4">
      <WorkspaceTaskNumberSettings workspaceId={workspaceId} />
      <PromptList workingDirectoryId={workspaceId} />
    </div>
  )
}
