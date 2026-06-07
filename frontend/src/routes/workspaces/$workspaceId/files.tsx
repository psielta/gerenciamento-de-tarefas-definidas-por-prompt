import { createFileRoute } from '@tanstack/react-router'
import { FileExplorer } from '@/features/files/file-explorer'

export const Route = createFileRoute('/workspaces/$workspaceId/files')({
  component: WorkspaceFilesPage,
})

function WorkspaceFilesPage() {
  const { workspaceId } = Route.useParams()

  return (
    <FileExplorer
      key={workspaceId}
      workingDirectoryId={workspaceId}
      className="min-h-[24rem] lg:h-[calc(100svh-24rem)]"
    />
  )
}
