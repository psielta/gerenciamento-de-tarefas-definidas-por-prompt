import { AlertTriangle } from 'lucide-react'
import { useEffect, useState } from 'react'
import type { GitFileStatus } from '@/api/schemas'
import { getErrorMessage } from '@/api/client'
import { cn } from '@/lib/utils'
import { FileViewerPanel } from './file-viewer-panel'
import { GitDiffViewer } from './git-diff-viewer'
import { useDirectoryChildren } from './use-file-queries'
import { useGitHistory } from './use-git-history'
import { WorkspaceFileTree } from './workspace-file-tree'

type FileExplorerProps = {
  workingDirectoryId: string
  className?: string
  /**
   * Quando informados, a selecao passa a ser controlada pelo componente pai
   * (usado pela rota /files para preservar o arquivo aberto entre os modos
   * normal e expandido). Sem eles, o estado continua interno.
   */
  selectedPath?: string | null
  onSelectFile?: (relativePath: string) => void
  onClearSelection?: () => void
}

/**
 * File tree + viewer pair. Height-agnostic: the parent passes a bounded height
 * (via `className`) so the Monaco editor scrolls internally instead of growing.
 * Remount with a `key` to reset the selected file when switching workspaces.
 */
export function FileExplorer({
  workingDirectoryId,
  className,
  selectedPath,
  onSelectFile,
  onClearSelection,
}: FileExplorerProps) {
  const { openHistory } = useGitHistory()
  const rootQuery = useDirectoryChildren(workingDirectoryId, '')
  const [internalPath, setInternalPath] = useState<string | null>(null)
  const [diffSelection, setDiffSelection] = useState<GitFileStatus | null>(null)
  const isControlled = selectedPath !== undefined
  const rootReady = rootQuery.isSuccess
  const rootUnavailable = rootQuery.isError
  const selectedActivePath = isControlled ? selectedPath : internalPath
  const activePath = rootReady ? selectedActivePath : null
  const activeDiffSelection = rootReady ? diffSelection : null

  useEffect(() => {
    if (!rootUnavailable) {
      return
    }

    if (selectedActivePath) {
      onClearSelection?.()
    }
  }, [onClearSelection, rootUnavailable, selectedActivePath])

  const handleSelectFile = (relativePath: string) => {
    setDiffSelection(null)
    if (!isControlled) {
      setInternalPath(relativePath)
    }

    onSelectFile?.(relativePath)
  }

  const handleSelectChange = (entry: GitFileStatus) => {
    setDiffSelection(entry)
  }

  return (
    <div className={cn('grid gap-4 lg:grid-cols-[16rem_minmax(0,1fr)] lg:grid-rows-1', className)}>
      <WorkspaceFileTree
        workingDirectoryId={workingDirectoryId}
        selectedPath={activePath}
        selectedGitPath={diffSelection?.path}
        onSelectFile={handleSelectFile}
        onSelectGitChange={handleSelectChange}
        onShowGitHistory={(path) => openHistory(workingDirectoryId, path)}
        className="min-h-[24rem] lg:min-h-0"
      />

      {rootUnavailable ? (
        <WorkspaceUnavailablePanel error={rootQuery.error} />
      ) : activeDiffSelection ? (
        <GitDiffViewer
          workingDirectoryId={workingDirectoryId}
          path={activeDiffSelection.path}
          originalPath={activeDiffSelection.originalPath}
          status={activeDiffSelection.status}
          className="min-h-[24rem] lg:min-h-0"
        />
      ) : activePath ? (
        <FileViewerPanel
          workingDirectoryId={workingDirectoryId}
          relativePath={activePath}
          inline
          className="min-h-[24rem] lg:min-h-0"
        />
      ) : (
        <div className="flex min-h-[24rem] items-center justify-center rounded-lg border border-dashed border-input bg-card p-6 text-sm text-muted-foreground lg:min-h-0">
          Selecione um arquivo na arvore para visualizar o conteudo.
        </div>
      )}
    </div>
  )
}

function WorkspaceUnavailablePanel({ error }: { error: unknown }) {
  return (
    <div className="flex min-h-[24rem] flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-input bg-card p-6 text-center text-sm text-muted-foreground lg:min-h-0">
      <AlertTriangle className="h-5 w-5 text-warning-solid" />
      <p className="font-medium text-foreground">Diretorio do workspace nao encontrado.</p>
      <p className="max-w-md">
        A pasta cadastrada para este workspace nao esta mais acessivel. Selecione outro workspace ou ajuste o
        diretorio em Workspaces.
      </p>
      <p className="max-w-md text-xs text-destructive">{getErrorMessage(error)}</p>
    </div>
  )
}
