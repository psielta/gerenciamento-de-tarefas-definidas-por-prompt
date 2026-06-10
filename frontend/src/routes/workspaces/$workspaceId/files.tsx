import { createFileRoute } from '@tanstack/react-router'
import { Maximize2 } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { ExpandedFileOverlay } from '@/features/files/expanded-file-overlay'
import { FileExplorer } from '@/features/files/file-explorer'
import { readLastOpenedFile, writeLastOpenedFile } from '@/features/files/last-opened-file'

export const Route = createFileRoute('/workspaces/$workspaceId/files')({
  component: WorkspaceFilesPage,
})

function WorkspaceFilesPage() {
  const { workspaceId } = Route.useParams()

  return <WorkspaceFilesContent key={workspaceId} workspaceId={workspaceId} />
}

function WorkspaceFilesContent({ workspaceId }: { workspaceId: string }) {
  const [expanded, setExpanded] = useState(false)
  // Selecao controlada para o arquivo aberto sobreviver a entrada/saida do
  // modo expandido; inicia restaurando o ultimo aberto do workspace.
  const [selectedPath, setSelectedPath] = useState<string | null>(() => readLastOpenedFile(workspaceId))

  const handleSelectFile = (relativePath: string) => {
    setSelectedPath(relativePath)
    writeLastOpenedFile(workspaceId, relativePath)
  }

  return (
    <div className="grid gap-3">
      <div className="flex justify-end">
        <Button
          type="button"
          variant="secondary"
          size="sm"
          onClick={() => setExpanded(true)}
          title="Modo expandido"
          aria-label="Entrar no modo expandido"
        >
          <Maximize2 className="h-4 w-4" />
          Modo expandido
        </Button>
      </div>

      <FileExplorer
        workingDirectoryId={workspaceId}
        selectedPath={selectedPath}
        onSelectFile={handleSelectFile}
        className="min-h-[24rem] lg:h-[calc(100svh-27rem)]"
      />

      {expanded ? (
        <ExpandedFileOverlay
          workingDirectoryId={workspaceId}
          initialPath={selectedPath}
          onSelectFile={(targetWorkspaceId, relativePath) => {
            if (targetWorkspaceId === workspaceId) {
              setSelectedPath(relativePath)
            }
          }}
          onExit={() => setExpanded(false)}
        />
      ) : null}
    </div>
  )
}
