import { useQueryClient } from '@tanstack/react-query'
import { AlertCircle, Check, Loader2 } from 'lucide-react'
import type * as React from 'react'
import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { updateDiagram } from '@/api/diagrams'
import { queryKeys } from '@/api/query-keys'
import type { Diagram } from '@/api/schemas'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

const ExcalidrawEditor = lazy(() => import('./excalidraw-editor'))
const MermaidEditor = lazy(() => import('./mermaid-editor'))

type SaveStatus = 'idle' | 'saving' | 'error'

const dateTimeFormatter = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

/**
 * Title + description + type-specific editor for a single diagram. Persists with
 * a debounced autosave and an explicit "Salvar" button, surfacing the current
 * save state. Must be mounted with `key={diagram.id}` so each diagram gets fresh
 * local state.
 */
export function DiagramEditor({ diagram }: { diagram: Diagram }) {
  const queryClient = useQueryClient()
  const [title, setTitle] = useState(diagram.title)
  const [description, setDescription] = useState(diagram.description ?? '')
  const [content, setContent] = useState(diagram.content)
  const [saved, setSaved] = useState({
    title: diagram.title,
    description: diagram.description ?? '',
    content: diagram.content,
  })
  const [status, setStatus] = useState<SaveStatus>('idle')
  const [lastSavedAt, setLastSavedAt] = useState(diagram.updatedAtUtc)

  const trimmedTitle = title.trim()
  const dirty = title !== saved.title || description !== saved.description || content !== saved.content

  const flushRef = useRef({ title, description, content, saved })
  useEffect(() => {
    flushRef.current = { title, description, content, saved }
  }, [content, description, saved, title])

  const invalidate = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.diagrams.all })
  }, [queryClient])

  const performSave = useCallback(async () => {
    const current = flushRef.current
    const nextTitle = current.title.trim()
    if (!nextTitle) {
      return
    }
    if (
      nextTitle === current.saved.title &&
      current.description === current.saved.description &&
      current.content === current.saved.content
    ) {
      return
    }

    setStatus('saving')
    try {
      const updated = await updateDiagram(diagram.id, {
        title: nextTitle,
        content: current.content,
        description: current.description.trim() ? current.description.trim() : null,
      })
      setSaved({ title: updated.title, description: updated.description ?? '', content: updated.content })
      setLastSavedAt(updated.updatedAtUtc)
      setStatus('idle')
      invalidate()
    } catch (error) {
      setStatus('error')
      toast.error(getErrorMessage(error))
    }
  }, [diagram.id, invalidate])

  // Debounced autosave while editing. Re-runs on every change to reset the timer.
  useEffect(() => {
    if (!dirty || !trimmedTitle) {
      return
    }
    const handle = window.setTimeout(() => void performSave(), 800)
    return () => window.clearTimeout(handle)
  }, [content, description, dirty, performSave, title, trimmedTitle])

  // Flush pending edits when switching diagrams / unmounting so nothing is lost.
  useEffect(() => {
    return () => {
      const { title: pendingTitle, description: pendingDescription, content: pendingContent, saved: lastSaved } =
        flushRef.current
      const nextTitle = pendingTitle.trim()
      if (!nextTitle) {
        return
      }
      if (
        nextTitle === lastSaved.title &&
        pendingDescription === lastSaved.description &&
        pendingContent === lastSaved.content
      ) {
        return
      }

      void updateDiagram(diagram.id, {
        title: nextTitle,
        content: pendingContent,
        description: pendingDescription.trim() ? pendingDescription.trim() : null,
      })
        .then(() => invalidate())
        .catch(() => undefined)
    }
  }, [diagram.id, invalidate])

  const handleKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 's') {
      event.preventDefault()
      void performSave()
    }
  }

  return (
    <div
      className="flex min-h-0 flex-1 flex-col gap-3 rounded-lg border border-border bg-card p-4"
      onKeyDown={handleKeyDown}
    >
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <Input
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            placeholder="Titulo do diagrama"
            className="h-10 text-base font-semibold"
            aria-label="Titulo do diagrama"
          />
          <span className="shrink-0 rounded bg-muted px-2 py-1 text-xs font-medium text-muted-foreground">
            {diagram.type}
          </span>
        </div>
        <Input
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          placeholder="Descricao (opcional)"
          className="h-9 text-sm"
          aria-label="Descricao do diagrama"
        />
        <div className="flex items-center justify-between gap-3">
          <SaveIndicator status={status} dirty={dirty} hasTitle={Boolean(trimmedTitle)} lastSavedAt={lastSavedAt} />
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => void performSave()}
            disabled={!dirty || status === 'saving' || !trimmedTitle}
          >
            Salvar
          </Button>
        </div>
      </div>

      <Suspense fallback={<EditorFallback />}>
        {diagram.type === 'Excalidraw' ? (
          <ExcalidrawEditor value={content} onChange={setContent} />
        ) : (
          <MermaidEditor value={content} onChange={setContent} />
        )}
      </Suspense>
    </div>
  )
}

function EditorFallback() {
  return (
    <div className="flex min-h-[20rem] flex-1 items-center justify-center rounded-md border border-border bg-card text-sm text-muted-foreground">
      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
      Carregando editor
    </div>
  )
}

function SaveIndicator({
  status,
  dirty,
  hasTitle,
  lastSavedAt,
}: {
  status: SaveStatus
  dirty: boolean
  hasTitle: boolean
  lastSavedAt: string
}) {
  if (!hasTitle) {
    return (
      <span className="inline-flex items-center gap-1.5 text-xs text-warning-foreground">
        <AlertCircle className="h-3.5 w-3.5" />
        Informe um titulo para salvar
      </span>
    )
  }

  if (status === 'saving') {
    return (
      <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
        <Loader2 className="h-3.5 w-3.5 animate-spin" />
        Salvando
      </span>
    )
  }

  if (status === 'error') {
    return (
      <span className="inline-flex items-center gap-1.5 text-xs text-destructive">
        <AlertCircle className="h-3.5 w-3.5" />
        Erro ao salvar
      </span>
    )
  }

  if (dirty) {
    return <span className="text-xs text-muted-foreground">Alteracoes nao salvas</span>
  }

  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
      <Check className="h-3.5 w-3.5 text-primary" />
      Salvo · {formatTimestamp(lastSavedAt)}
    </span>
  )
}

function formatTimestamp(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : dateTimeFormatter.format(date)
}
