import { ChevronRight, FileText, Folder, Loader2 } from 'lucide-react'
import type { DragEvent } from 'react'
import { useMemo, useState } from 'react'
import type { FileTreeNode } from '@/api/schemas'
import { cn } from '@/lib/utils'
import { useDirectoryChildren } from './use-file-queries'

export const WORKSPACE_FILE_MIME = 'application/x-workspace-file'

type WorkspaceFileTreeProps = {
  workingDirectoryId: string
  selectedPath?: string | null
  onSelectFile?: (relativePath: string) => void
  onOpenFile?: (relativePath: string) => void
  className?: string
}

export function WorkspaceFileTree({
  workingDirectoryId,
  selectedPath,
  onSelectFile,
  onOpenFile,
  className,
}: WorkspaceFileTreeProps) {
  const rootQuery = useDirectoryChildren(workingDirectoryId, '')
  const [expandedPaths, setExpandedPaths] = useState<Set<string>>(() => new Set())

  const toggleExpanded = (relativePath: string) => {
    setExpandedPaths((current) => {
      const next = new Set(current)
      if (next.has(relativePath)) {
        next.delete(relativePath)
      } else {
        next.add(relativePath)
      }
      return next
    })
  }

  const nodes = rootQuery.data ?? []

  return (
    <section
      className={cn(
        'grid min-h-0 grid-rows-[auto_minmax(0,1fr)] overflow-hidden rounded-lg border border-border bg-card',
        className,
      )}
    >
      <div className="border-b border-border px-3 py-2 text-xs font-semibold uppercase tracking-normal text-muted-foreground">
        Arquivos do workspace
      </div>

      <div className="min-h-0 overflow-auto p-1">
        {rootQuery.isLoading ? (
          <div className="flex items-center gap-2 px-2 py-2 text-xs text-muted-foreground">
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
            Carregando arvore
          </div>
        ) : null}

        {rootQuery.isError ? (
          <div className="px-2 py-2 text-xs text-destructive">Nao foi possivel carregar os arquivos.</div>
        ) : null}

        {!rootQuery.isLoading && !nodes.length ? (
          <div className="px-2 py-2 text-xs text-muted-foreground">Diretorio vazio.</div>
        ) : null}

        <ul className="grid gap-0.5">
          {nodes.map((node) => (
            <TreeNode
              key={node.relativePath}
              workingDirectoryId={workingDirectoryId}
              node={node}
              depth={0}
              expandedPaths={expandedPaths}
              selectedPath={selectedPath}
              onToggleExpanded={toggleExpanded}
              onSelectFile={onSelectFile}
              onOpenFile={onOpenFile}
            />
          ))}
        </ul>
      </div>
    </section>
  )
}

type TreeNodeProps = {
  workingDirectoryId: string
  node: FileTreeNode
  depth: number
  expandedPaths: Set<string>
  selectedPath?: string | null
  onToggleExpanded: (relativePath: string) => void
  onSelectFile?: (relativePath: string) => void
  onOpenFile?: (relativePath: string) => void
}

function TreeNode({
  workingDirectoryId,
  node,
  depth,
  expandedPaths,
  selectedPath,
  onToggleExpanded,
  onSelectFile,
  onOpenFile,
}: TreeNodeProps) {
  const isExpanded = expandedPaths.has(node.relativePath)
  const childrenQuery = useDirectoryChildren(workingDirectoryId, node.relativePath, node.isDirectory && isExpanded)
  const children = useMemo(() => childrenQuery.data ?? [], [childrenQuery.data])
  const isSelected = !node.isDirectory && selectedPath === node.relativePath

  const handleClick = () => {
    if (node.isDirectory) {
      onToggleExpanded(node.relativePath)
      return
    }

    onSelectFile?.(node.relativePath)
    onOpenFile?.(node.relativePath)
  }

  const handleDragStart = (event: DragEvent<HTMLButtonElement>) => {
    if (node.isDirectory) {
      event.preventDefault()
      return
    }

    event.dataTransfer.effectAllowed = 'copy'
    event.dataTransfer.setData(
      WORKSPACE_FILE_MIME,
      JSON.stringify({
        workingDirectoryId,
        relativePath: node.relativePath,
      }),
    )
    event.dataTransfer.setData('text/plain', `@${node.relativePath}`)
  }

  return (
    <li>
      <button
        type="button"
        draggable={!node.isDirectory}
        onClick={handleClick}
        onDragStart={handleDragStart}
        className={cn(
          'flex w-full min-w-0 items-center gap-1 rounded-md px-1.5 py-1 text-left text-xs transition-colors hover:bg-muted',
          isSelected && 'bg-accent text-foreground',
        )}
        style={{ paddingLeft: `${depth * 0.85 + 0.35}rem` }}
        title={node.relativePath}
      >
        {node.isDirectory ? (
          <ChevronRight
            className={cn('h-3.5 w-3.5 shrink-0 text-muted-foreground transition-transform', isExpanded && 'rotate-90')}
          />
        ) : (
          <span className="inline-block h-3.5 w-3.5 shrink-0" />
        )}
        {node.isDirectory ? (
          <Folder className="h-3.5 w-3.5 shrink-0 text-warning-solid" />
        ) : (
          <FileText className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
        )}
        <span className="truncate font-mono">{node.name}</span>
      </button>

      {node.isDirectory && isExpanded ? (
        <div className="grid gap-0.5">
          {childrenQuery.isLoading ? (
            <div
              className="flex items-center gap-1.5 py-1 text-[0.68rem] text-muted-foreground"
              style={{ paddingLeft: `${(depth + 1) * 0.85 + 0.35}rem` }}
            >
              <Loader2 className="h-3 w-3 animate-spin" />
              Carregando
            </div>
          ) : null}

          <ul className="grid gap-0.5">
            {children.map((child) => (
              <TreeNode
                key={child.relativePath}
                workingDirectoryId={workingDirectoryId}
                node={child}
                depth={depth + 1}
                expandedPaths={expandedPaths}
                selectedPath={selectedPath}
                onToggleExpanded={onToggleExpanded}
                onSelectFile={onSelectFile}
                onOpenFile={onOpenFile}
              />
            ))}
          </ul>
        </div>
      ) : null}
    </li>
  )
}