import { X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { TerminalsPanel } from '@/features/prompts/terminals-panel'

type AgentTerminalDrawerProps = {
  promptId: string
  title: string
  onClose: () => void
}

/**
 * Right-side drawer que hospeda o terminal do prompt filho com o agente em
 * execucao. Reaproveita o TerminalsPanel escopado ao prompt. Fecha pelo botao
 * do cabecalho ou clique no backdrop; Escape nao e vinculado de proposito para
 * poder ser usado dentro do terminal (vim, etc.).
 */
export function AgentTerminalDrawer({ promptId, title, onClose }: AgentTerminalDrawerProps) {
  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-black/50 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="agent-terminal-drawer-title"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
    >
      <div className="grid h-full w-full max-w-[min(96vw,72rem)] grid-rows-[auto_minmax(0,1fr)] border-l border-border bg-card shadow-2xl">
        <div className="flex min-w-0 items-center justify-between gap-2 border-b border-border px-4 py-2.5">
          <h2
            id="agent-terminal-drawer-title"
            className="min-w-0 truncate text-base font-semibold text-foreground"
            title={title}
          >
            Terminal do agente — {title}
          </h2>
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

        <div className="min-h-0 overflow-auto px-4 py-3">
          <TerminalsPanel promptId={promptId} />
        </div>
      </div>
    </div>
  )
}
