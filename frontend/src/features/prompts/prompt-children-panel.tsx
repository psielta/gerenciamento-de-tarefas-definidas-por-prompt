import { useQuery } from '@tanstack/react-query'
import { Copy, Loader2, MessageSquareText, X } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { toast } from 'sonner'
import { listPrompts } from '@/api/prompts'
import { queryKeys } from '@/api/query-keys'
import type { Prompt, PromptStatus } from '@/api/schemas'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useFileViewer } from '@/features/files/use-file-viewer'
import { PromptEditor } from './prompt-editor'
import {
  AGENT_LABELS,
  KIND_LABELS,
  STATUS_BADGE_VARIANTS,
  STATUS_LABELS,
} from './constants'

type PromptChildrenPanelProps = {
  workingDirectoryId: string
  parentPromptId: string
}

const dateFormatter = new Intl.DateTimeFormat('pt-BR', {
  dateStyle: 'short',
  timeStyle: 'short',
})

export function PromptChildrenPanel({ workingDirectoryId, parentPromptId }: PromptChildrenPanelProps) {
  const [selectedPrompt, setSelectedPrompt] = useState<Prompt | null>(null)
  const filters = useMemo(
    () => ({ workingDirectoryId, parentPromptId }),
    [parentPromptId, workingDirectoryId],
  )
  const childrenQuery = useQuery({
    queryKey: queryKeys.prompts.list(filters),
    queryFn: () => listPrompts(filters),
  })

  return (
    <section className="grid gap-3 rounded-lg border border-border bg-card p-4">
      <div className="flex items-center gap-2 text-sm font-semibold text-foreground">
        <MessageSquareText className="h-4 w-4 text-ring" />
        Prompts filhos
      </div>

      {childrenQuery.isLoading ? (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Carregando
        </div>
      ) : null}

      {!childrenQuery.isLoading && !childrenQuery.data?.length ? (
        <div className="rounded-md border border-dashed border-input bg-card p-3 text-sm text-muted-foreground">
          Nenhum prompt filho.
        </div>
      ) : null}

      <div className="grid gap-2">
        {childrenQuery.data?.map((prompt) => (
          <button
            key={prompt.id}
            type="button"
            className="grid min-w-0 gap-2 rounded-md border border-border p-3 text-left transition-colors hover:border-ring hover:bg-card focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            onClick={() => setSelectedPrompt(prompt)}
          >
            <div className="flex min-w-0 flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="truncate text-sm font-medium text-foreground">{prompt.title}</div>
                <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{prompt.content}</p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-1.5">
                <StatusBadge status={prompt.status} />
                <Badge variant="blue">{AGENT_LABELS[prompt.targetAgent]}</Badge>
                <Badge>{KIND_LABELS[prompt.kind]}</Badge>
              </div>
            </div>
          </button>
        ))}
      </div>

      {selectedPrompt ? (
        <ChildPromptDrawer prompt={selectedPrompt} onClose={() => setSelectedPrompt(null)} />
      ) : null}
    </section>
  )
}

function StatusBadge({ status }: { status: PromptStatus }) {
  return <Badge variant={STATUS_BADGE_VARIANTS[status]}>{STATUS_LABELS[status]}</Badge>
}

function ChildPromptDrawer({ prompt, onClose }: { prompt: Prompt; onClose: () => void }) {
  const { openFile } = useFileViewer()

  const copyContent = async () => {
    await navigator.clipboard.writeText(prompt.content)
    toast.success('Prompt filho copiado.')
  }

  const requestClose = useCallback(() => onClose(), [onClose])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        requestClose()
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [requestClose])

  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-black/50 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="child-prompt-title"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          requestClose()
        }
      }}
    >
      <div className="grid h-full w-full max-w-2xl grid-rows-[auto_minmax(0,1fr)_auto] border-l border-border bg-card shadow-2xl">
        <div className="flex min-w-0 items-start justify-between gap-3 border-b border-border p-4">
          <div className="min-w-0">
            <div className="flex min-w-0 flex-wrap items-center gap-2">
              <h2 id="child-prompt-title" className="truncate text-base font-semibold text-foreground">
                {prompt.title}
              </h2>
              <StatusBadge status={prompt.status} />
              <Badge variant="blue">{AGENT_LABELS[prompt.targetAgent]}</Badge>
              <Badge>{KIND_LABELS[prompt.kind]}</Badge>
            </div>
            <p className="mt-1 text-xs text-muted-foreground">
              Criado em {dateFormatter.format(new Date(prompt.createdAtUtc))}
            </p>
          </div>

          <Button type="button" variant="ghost" size="icon" onClick={requestClose} aria-label="Fechar">
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div className="min-h-0 overflow-hidden p-4">
          <PromptEditor
            workingDirectoryId={prompt.workingDirectoryId}
            value={prompt.content}
            onOpenMention={(relativePath) => openFile(prompt.workingDirectoryId, relativePath)}
            onChange={() => undefined}
            editable={false}
            className="grid h-full min-h-0 grid-rows-[auto_minmax(0,1fr)]"
            contentClassName="min-h-0 overflow-auto"
            editorClassName="min-h-full"
          />
        </div>

        <div className="flex flex-wrap justify-end gap-2 border-t border-border p-4">
          <Button type="button" variant="ghost" onClick={requestClose}>
            Fechar
          </Button>
          <Button type="button" onClick={() => copyContent().catch(() => toast.error('Nao foi possivel copiar.'))}>
            <Copy className="h-4 w-4" />
            Copiar prompt
          </Button>
        </div>
      </div>
    </div>
  )
}
