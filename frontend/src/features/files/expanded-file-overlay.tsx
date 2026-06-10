import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { getErrorMessage } from '@/api/client'
import { queryKeys } from '@/api/query-keys'
import { listWorkingDirectories } from '@/api/working-directories'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { ExpandedFileExplorer } from './expanded-file-explorer'
import { readLastOpenedFile, writeLastOpenedFile } from './last-opened-file'

type ExpandedFileOverlayProps = {
  workingDirectoryId: string
  /** Arquivo aberto na superficie de origem; sem ele, restaura o ultimo aberto. */
  initialPath?: string | null
  /** Sincroniza a superficie de origem a cada arquivo selecionado no modo expandido. */
  onSelectFile?: (workingDirectoryId: string, relativePath: string) => void
  onExit: () => void
  /** Repassado ao overlay (ex.: z-index acima do drawer de visualizacao). */
  className?: string
}

/**
 * Versao autocontida do modo expandido: carrega a lista de workspaces e
 * gerencia workspace ativo + arquivo selecionado internamente, permitindo
 * abrir o modo expandido de qualquer superficie com acesso a arquivos (rota
 * de arquivos do workspace, drawer de visualizacao) sem o estado controlado
 * da pagina global /files.
 */
export function ExpandedFileOverlay({
  workingDirectoryId,
  initialPath = null,
  onSelectFile,
  onExit,
  className,
}: ExpandedFileOverlayProps) {
  const workspacesQuery = useQuery({
    queryKey: queryKeys.workingDirectories.all,
    queryFn: listWorkingDirectories,
  })
  const [activeWorkspaceId, setActiveWorkspaceId] = useState(workingDirectoryId)
  // Mesmo modelo da pagina /files: quando o par nao corresponde ao workspace
  // ativo, cai no ultimo arquivo aberto persistido daquele workspace.
  const [selection, setSelection] = useState<{ workspaceId: string; path: string | null } | null>(
    initialPath ? { workspaceId: workingDirectoryId, path: initialPath } : null,
  )

  const selectedPath =
    selection && selection.workspaceId === activeWorkspaceId
      ? selection.path
      : readLastOpenedFile(activeWorkspaceId)

  const handleSelectFile = (relativePath: string) => {
    setSelection({ workspaceId: activeWorkspaceId, path: relativePath })
    writeLastOpenedFile(activeWorkspaceId, relativePath)
    onSelectFile?.(activeWorkspaceId, relativePath)
  }

  if (!workspacesQuery.data) {
    return (
      <div
        className={cn('fixed inset-0 z-50 flex flex-col items-center justify-center gap-3 bg-background', className)}
        role="dialog"
        aria-modal="true"
        aria-label="Navegacao expandida de arquivos"
      >
        {workspacesQuery.isError ? (
          <p className="px-4 text-center text-sm text-destructive">{getErrorMessage(workspacesQuery.error)}</p>
        ) : (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Carregando diretorios...
          </div>
        )}
        <Button type="button" variant="secondary" size="sm" onClick={onExit}>
          Sair
        </Button>
      </div>
    )
  }

  return (
    <ExpandedFileExplorer
      workingDirectoryId={activeWorkspaceId}
      workspaces={workspacesQuery.data}
      onChangeWorkspace={setActiveWorkspaceId}
      selectedPath={selectedPath}
      onSelectFile={handleSelectFile}
      onExit={onExit}
      className={className}
    />
  )
}
