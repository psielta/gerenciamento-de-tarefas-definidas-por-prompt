import { Link, createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useEffect } from 'react'
import { queryKeys } from '@/api/query-keys'
import { listWorkingDirectories } from '@/api/working-directories'
import { Select } from '@/components/ui/select'
import { FileExplorer } from '@/features/files/file-explorer'
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
        <div className="sm:w-72">
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

      {selectedId ? (
        <FileExplorer key={selectedId} workingDirectoryId={selectedId} className="min-h-0 flex-1" />
      ) : null}
    </div>
  )
}
