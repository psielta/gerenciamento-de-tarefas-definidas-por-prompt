import { useState } from 'react'
import { cn } from '@/lib/utils'
import { FileViewerPanel } from './file-viewer-panel'
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
}

/**
 * File tree + viewer pair. Height-agnostic: the parent passes a bounded height
 * (via `className`) so the Monaco editor scrolls internally instead of growing.
 * Remount with a `key` to reset the selected file when switching workspaces.
 */
export function FileExplorer({ workingDirectoryId, className, selectedPath, onSelectFile }: FileExplorerProps) {
  const [internalPath, setInternalPath] = useState<string | null>(null)
  const isControlled = selectedPath !== undefined
  const activePath = isControlled ? selectedPath : internalPath

  const handleSelectFile = (relativePath: string) => {
    if (!isControlled) {
      setInternalPath(relativePath)
    }

    onSelectFile?.(relativePath)
  }

  return (
    <div className={cn('grid gap-4 lg:grid-cols-[16rem_minmax(0,1fr)] lg:grid-rows-1', className)}>
      <WorkspaceFileTree
        workingDirectoryId={workingDirectoryId}
        selectedPath={activePath}
        onSelectFile={handleSelectFile}
        className="min-h-[24rem] lg:min-h-0"
      />

      {activePath ? (
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
