import { FileCode2, Minimize2, PanelLeftClose, PanelLeftOpen, Search } from 'lucide-react'
import { useEffect, useState } from 'react'
import type { WorkingDirectory } from '@/api/schemas'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { useLocalStorage } from '@/hooks/use-local-storage'
import { cn } from '@/lib/utils'
import { FileSearchPalette } from './file-search-palette'
import { FileViewerPanel } from './file-viewer-panel'
import { WorkspaceFileTree } from './workspace-file-tree'

const TREE_WIDTH_STORAGE_KEY = 'prompt-tasks:files:tree-width'
const TREE_MIN_WIDTH = 200
const TREE_MAX_WIDTH = 560
const TREE_DEFAULT_WIDTH = 288

function clampTreeWidth(width: number) {
  if (Number.isNaN(width)) {
    return TREE_DEFAULT_WIDTH
  }

  return Math.min(Math.max(width, TREE_MIN_WIDTH), TREE_MAX_WIDTH)
}

type ExpandedFileExplorerProps = {
  workingDirectoryId: string
  workspaces: WorkingDirectory[]
  onChangeWorkspace: (workingDirectoryId: string) => void
  selectedPath: string | null
  onSelectFile: (relativePath: string) => void
  onExit: () => void
  /** Permite ajustar o overlay (ex.: z-index acima do drawer de visualizacao). */
  className?: string
}

/**
 * Modo expandido da navegacao de arquivos: ocupa a janela inteira priorizando
 * arvore + viewer, com arvore recolhivel/redimensionavel e busca rapida via
 * paleta (Ctrl+K). Esc fecha a paleta e, em seguida, sai do modo expandido.
 */
export function ExpandedFileExplorer({
  workingDirectoryId,
  workspaces,
  onChangeWorkspace,
  selectedPath,
  onSelectFile,
  onExit,
  className,
}: ExpandedFileExplorerProps) {
  const [paletteOpen, setPaletteOpen] = useState(false)
  const [treeVisible, setTreeVisible] = useState(true)
  const [storedTreeWidth, setStoredTreeWidth] = useLocalStorage(
    TREE_WIDTH_STORAGE_KEY,
    String(TREE_DEFAULT_WIDTH),
  )
  const [treeWidth, setTreeWidth] = useState(() => clampTreeWidth(Number.parseInt(storedTreeWidth, 10)))

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        setPaletteOpen(true)
        return
      }

      if (event.key === 'Escape') {
        if (paletteOpen) {
          setPaletteOpen(false)
          return
        }

        onExit()
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onExit, paletteOpen])

  // Trava o scroll da pagina atras do overlay enquanto o modo expandido durar.
  useEffect(() => {
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
    }
  }, [])

  const handleResizeStart = (event: React.PointerEvent<HTMLDivElement>) => {
    event.preventDefault()
    const startX = event.clientX
    const startWidth = treeWidth
    let nextWidth = startWidth

    const handleMove = (moveEvent: PointerEvent) => {
      nextWidth = clampTreeWidth(startWidth + (moveEvent.clientX - startX))
      setTreeWidth(nextWidth)
    }

    const handleUp = () => {
      setStoredTreeWidth(String(nextWidth))
      window.removeEventListener('pointermove', handleMove)
      window.removeEventListener('pointerup', handleUp)
    }

    window.addEventListener('pointermove', handleMove)
    window.addEventListener('pointerup', handleUp)
  }

  return (
    <div
      className={cn('fixed inset-0 z-50 grid grid-rows-[auto_minmax(0,1fr)] bg-background', className)}
      role="dialog"
      aria-modal="true"
      aria-label="Navegacao expandida de arquivos"
    >
      <header className="flex flex-wrap items-center justify-between gap-2 border-b border-border bg-card px-3 py-2">
        <div className="flex min-w-0 items-center gap-2">
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-muted-foreground"
            onClick={() => setTreeVisible((current) => !current)}
            title={treeVisible ? 'Recolher arvore' : 'Mostrar arvore'}
            aria-label={treeVisible ? 'Recolher arvore de arquivos' : 'Mostrar arvore de arquivos'}
          >
            {treeVisible ? <PanelLeftClose className="h-4 w-4" /> : <PanelLeftOpen className="h-4 w-4" />}
          </Button>
          <div className="w-56 max-w-[40vw]">
            <Select
              value={workingDirectoryId}
              onChange={(event) => onChangeWorkspace(event.target.value)}
              aria-label="Selecionar diretorio de trabalho"
            >
              {workspaces.map((workspace) => (
                <option key={workspace.id} value={workspace.id}>
                  {workspace.name}
                </option>
              ))}
            </Select>
          </div>
        </div>

        <div className="flex items-center gap-1.5">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => setPaletteOpen(true)}
            title="Buscar arquivos (Ctrl+K)"
          >
            <Search className="h-4 w-4" />
            Buscar arquivos
            <kbd className="rounded border border-border bg-muted px-1.5 py-0.5 font-mono text-[0.65rem] text-muted-foreground">
              Ctrl K
            </kbd>
          </Button>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={onExit}
            title="Sair do modo expandido (Esc)"
            aria-label="Sair do modo expandido"
          >
            <Minimize2 className="h-4 w-4" />
            Sair
          </Button>
        </div>
      </header>

      <div className="flex min-h-0 min-w-0 gap-1.5 p-2">
        {treeVisible ? (
          <>
            <WorkspaceFileTree
              key={workingDirectoryId}
              workingDirectoryId={workingDirectoryId}
              selectedPath={selectedPath}
              onSelectFile={onSelectFile}
              className="h-full shrink-0"
              style={{ width: treeWidth }}
            />
            <div
              role="separator"
              aria-orientation="vertical"
              aria-label="Redimensionar arvore de arquivos"
              onPointerDown={handleResizeStart}
              className="w-1.5 shrink-0 cursor-col-resize rounded-full bg-border transition-colors hover:bg-ring/60"
            />
          </>
        ) : null}

        {selectedPath ? (
          <FileViewerPanel
            workingDirectoryId={workingDirectoryId}
            relativePath={selectedPath}
            className="min-w-0 flex-1"
          />
        ) : (
          <div className="flex min-w-0 flex-1 flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-input bg-card p-6 text-center text-sm text-muted-foreground">
            <FileCode2 className="h-6 w-6" />
            <p>Nenhum arquivo aberto.</p>
            <p className="text-xs">
              Selecione um arquivo na arvore ou busque com{' '}
              <kbd className="rounded border border-border bg-muted px-1.5 py-0.5 font-mono text-[0.65rem]">Ctrl K</kbd>.
            </p>
          </div>
        )}
      </div>

      {paletteOpen ? (
        <FileSearchPalette
          workingDirectoryId={workingDirectoryId}
          onSelectFile={onSelectFile}
          onClose={() => setPaletteOpen(false)}
        />
      ) : null}
    </div>
  )
}
