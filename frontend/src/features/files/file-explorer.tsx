import { useState } from 'react'
import { cn } from '@/lib/utils'
import { FileViewerPanel } from './file-viewer-panel'
import { WorkspaceFileTree } from './workspace-file-tree'

type FileExplorerProps = {
  workingDirectoryId: string
  className?: string
}

/**
 * File tree + viewer pair. Height-agnostic: the parent passes a bounded height
 * (via `className`) so the Monaco editor scrolls internally instead of growing.
 * Remount with a `key` to reset the selected file when switching workspaces.
 */
export function FileExplorer({ workingDirectoryId, className }: FileExplorerProps) {
  const [selectedPath, setSelectedPath] = useState<string | null>(null)

  return (
    <div className={cn('grid gap-4 lg:grid-cols-[16rem_minmax(0,1fr)] lg:grid-rows-1', className)}>
      <WorkspaceFileTree
        workingDirectoryId={workingDirectoryId}
        selectedPath={selectedPath}
        onSelectFile={setSelectedPath}
        className="min-h-[24rem] lg:min-h-0"
      />

      {selectedPath ? (
        <FileViewerPanel
          workingDirectoryId={workingDirectoryId}
          relativePath={selectedPath}
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
