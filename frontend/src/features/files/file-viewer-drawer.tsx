import { Maximize2, X } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { ExpandedFileOverlay } from './expanded-file-overlay'
import { FileViewerPanel } from './file-viewer-panel'

type FileViewerDrawerProps = {
  workingDirectoryId: string
  relativePath: string
  onClose: () => void
  /** Atualiza o arquivo exibido no drawer conforme a navegacao no modo expandido. */
  onSelectFile?: (workingDirectoryId: string, relativePath: string) => void
}

export function FileViewerDrawer({ workingDirectoryId, relativePath, onClose, onSelectFile }: FileViewerDrawerProps) {
  const [expanded, setExpanded] = useState(false)

  return (
    <div
      className="fixed inset-0 z-[60] flex justify-end bg-black/50 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="file-viewer-drawer-title"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
    >
      <div className="grid h-full w-full max-w-[min(96vw,64rem)] grid-rows-[auto_minmax(0,1fr)] border-l border-border bg-card shadow-2xl">
        <div className="flex min-w-0 items-center justify-between gap-2 border-b border-border px-4 py-2.5">
          <h2 id="file-viewer-drawer-title" className="min-w-0 truncate text-base font-semibold text-foreground">
            Visualizar arquivo
          </h2>

          <div className="flex shrink-0 items-center gap-1">
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-8 w-8 shrink-0 text-muted-foreground"
              onClick={() => setExpanded(true)}
              aria-label="Abrir no modo expandido"
              title="Modo expandido"
            >
              <Maximize2 className="h-4 w-4" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-8 w-8 shrink-0 text-muted-foreground"
              onClick={onClose}
              aria-label="Fechar"
              title="Fechar"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
        </div>

        <div className="min-h-0 overflow-hidden p-3">
          <FileViewerPanel
            workingDirectoryId={workingDirectoryId}
            relativePath={relativePath}
            className="h-full min-h-0"
          />
        </div>
      </div>

      {expanded ? (
        <ExpandedFileOverlay
          workingDirectoryId={workingDirectoryId}
          initialPath={relativePath}
          onSelectFile={onSelectFile}
          onExit={() => setExpanded(false)}
          className="z-[70]"
        />
      ) : null}
    </div>
  )
}
