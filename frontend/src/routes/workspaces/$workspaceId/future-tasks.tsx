import { createFileRoute } from '@tanstack/react-router'
import { FutureTaskList } from '@/features/future-tasks/future-task-list'

export const Route = createFileRoute('/workspaces/$workspaceId/future-tasks')({
  component: WorkspaceFutureTasksPage,
})

function WorkspaceFutureTasksPage() {
  const { workspaceId } = Route.useParams()

  return <FutureTaskList key={workspaceId} workspaceId={workspaceId} />
}
