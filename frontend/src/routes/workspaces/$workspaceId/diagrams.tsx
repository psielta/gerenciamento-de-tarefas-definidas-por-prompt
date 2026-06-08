import { createFileRoute } from '@tanstack/react-router'
import { DiagramsWorkspace } from '@/features/diagrams/diagrams-workspace'

export const Route = createFileRoute('/workspaces/$workspaceId/diagrams')({
  component: WorkspaceDiagramsPage,
})

function WorkspaceDiagramsPage() {
  const { workspaceId } = Route.useParams()

  return <DiagramsWorkspace key={workspaceId} workspaceId={workspaceId} />
}
