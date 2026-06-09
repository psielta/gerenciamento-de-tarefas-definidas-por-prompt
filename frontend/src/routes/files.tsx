import { Link, createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { Maximize2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { queryKeys } from '@/api/query-keys'
import { listWorkingDirectories } from '@/api/working-directories'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { ExpandedFileExplorer } from '@/features/files/expanded-file-explorer'
import { FileExplorer } from '@/features/files/file-explorer'
import { readLastOpenedFile, writeLastOpenedFile } from '@/features/files/last-opened-file'
import { useLocalStorage } from '@/hooks/use-local-storage'

const SELECTED_WORKSPACE_KEY = 'prompt-tasks-files-workspace'

export const Route = createFileRoute('/files')({
  component: FilesPage,
})

function FilesPage() {
  const workspacesQuery = useQuery({
    queryKey: queryKeys.workingDirectories.all,
    queryFn: listWorkingDirectories,
  })
  const workspaces = workspacesQuery.data ?? []
  const [storedId, setStoredId] = useLocalStorage(SELECTED_WORKSPACE_KEY, '')
  const [expanded, setExpanded] = useState(false)
  // Selecao por workspace: quando o par nao corresponde ao workspace atual,
  // cai no ultimo arquivo aberto persistido, sem precisar de efeito.
  const [selection, setSelection] = useState<{ workspaceId: string; path: string | null } | null>(null)

  // Use the stored workspace if it still exists, otherwise fall back to the first.
  const selectedId = workspaces.some((workspace) => workspace.id === storedId)
    ? storedId
    : (workspaces[0]?.id ?? '')

  // Persist the resolved selection (first visit, or previously stored one removed).
  useEffect(() => {
    if (selectedId && selectedId !== storedId) {
      setStoredId(selectedId)
    }
  }, [selectedId, storedId, setStoredId])

  const selectedPath =
    selection && selection.workspaceId === selectedId
      ? selection.path
      : selectedId
        ? readLastOpenedFile(selectedId)
        : null

  const handleSelectFile = (relativePath: string) => {
    if (!selectedId) {
      return
    }

    setSelection({ workspaceId: selectedId, path: relativePath })
    writeLastOpenedFile(selectedId, relativePath)
  }

  const selectedWorkspace = workspaces.find((workspace) => workspace.id === selectedId)

  return (
    <div className="flex min-h-[28rem] flex-col gap-4 lg:h-[calc(100svh-7rem)]">
      <header className="flex shrink-0 flex-col gap-3 rounded-lg border border-border bg-card p-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold text-foreground">Arquivos</h1>
          <p className="mt-1 truncate text-sm text-muted-foreground">
            {selectedWorkspace?.absolutePath ?? 'Escolha um diretorio de trabalho para navegar nos arquivos.'}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <div className="w-full sm:w-72">
            <Select
              value={selectedId}
              onChange={(event) => setStoredId(event.target.value)}
              disabled={!workspaces.length}
              aria-label="Selecionar diretorio de trabalho"
            >
              {workspaces.length ? (
                workspaces.map((workspace) => (
                  <option key={workspace.id} value={workspace.id}>
                    {workspace.name}
                  </option>
                ))
              ) : (
                <option value="">Nenhum diretorio</option>
              )}
            </Select>
          </div>
          <Button
            type="button"
            variant="secondary"
            size="icon"
            onClick={() => setExpanded(true)}
            disabled={!selectedId}
            title="Modo expandido"
            aria-label="Entrar no modo expandido"
          >
            <Maximize2 className="h-4 w-4" />
          </Button>
        </div>
      </header>

      {workspacesQuery.isLoading ? (
        <div className="flex flex-1 items-center justify-center rounded-lg border border-dashed border-input bg-card p-6 text-sm text-muted-foreground">
          Carregando diretorios...
        </div>
      ) : null}

      {!workspacesQuery.isLoading && !workspaces.length ? (
        <div className="flex flex-1 flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-input bg-card p-6 text-center text-sm text-muted-foreground">
          <p>Nenhum diretorio de trabalho cadastrado.</p>
          <Link to="/workspaces" className="font-medium text-primary hover:underline">
            Cadastrar diretorio
          </Link>
        </div>
      ) : null}

      {selectedId && !expanded ? (
        <FileExplorer
          key={selectedId}
          workingDirectoryId={selectedId}
          selectedPath={selectedPath}
          onSelectFile={handleSelectFile}
          className="min-h-0 flex-1"
        />
      ) : null}

      {selectedId && expanded ? (
        <ExpandedFileExplorer
          workingDirectoryId={selectedId}
          workspaces={workspaces}
          onChangeWorkspace={setStoredId}
          selectedPath={selectedPath}
          onSelectFile={handleSelectFile}
          onExit={() => setExpanded(false)}
        />
      ) : null}
    </div>
  )
}
