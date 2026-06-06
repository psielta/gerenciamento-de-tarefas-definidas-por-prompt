import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import { FileViewerPanel } from '@/features/files/file-viewer-panel'
import { WorkspaceFileTree } from '@/features/files/workspace-file-tree'

export const Route = createFileRoute('/workspaces/$workspaceId/files')({
  component: WorkspaceFilesPage,
})

function WorkspaceFilesPage() {
  const { workspaceId } = Route.useParams()
  const [selectedPath, setSelectedPath] = useState<string | null>(null)

  return (
    <div className="grid min-h-[32rem] gap-4 lg:grid-cols-[16rem_minmax(0,1fr)]">
      <WorkspaceFileTree
        workingDirectoryId={workspaceId}
        selectedPath={selectedPath}
        onSelectFile={setSelectedPath}
        className="min-h-[32rem]"
      />

      {selectedPath ? (
        <FileViewerPanel
          workingDirectoryId={workspaceId}
          relativePath={selectedPath}
          inline
          className="min-h-[32rem]"
        />
      ) : (
        <div className="flex min-h-[32rem] items-center justify-center rounded-lg border border-dashed border-input bg-card p-6 text-sm text-muted-foreground">
          Selecione um arquivo na arvore para visualizar o conteudo.
        </div>
      )}
    </div>
  )
}