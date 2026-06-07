import { useQuery } from '@tanstack/react-query'
import { FileText, Loader2, Radio } from 'lucide-react'
import type { ComponentProps } from 'react'
import { useMemo, useState } from 'react'
import { listLinkedDocuments } from '@/api/linked-documents'
import { queryKeys } from '@/api/query-keys'
import type { LinkedDocument } from '@/api/schemas'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { usePromptHub } from '@/realtime/prompt-hub'
import { cn } from '@/lib/utils'
import { LinkDocumentForm } from './link-document-form'
import { LinkedDocumentViewer } from './linked-document-viewer'

type LinkedDocumentsPanelProps = {
  promptId: string
}

const statusLabels: Record<LinkedDocument['status'], string> = {
  Draft: 'Rascunho',
  Tracking: 'Monitorando',
  Paused: 'Pausado',
  Error: 'Erro',
  Missing: 'Nao encontrado',
}

const statusVariants: Record<LinkedDocument['status'], ComponentProps<typeof Badge>['variant']> = {
  Draft: 'neutral',
  Tracking: 'green',
  Paused: 'amber',
  Error: 'red',
  Missing: 'red',
}

export function LinkedDocumentsPanel({ promptId }: LinkedDocumentsPanelProps) {
  const hub = usePromptHub()
  const [selectedDocumentId, setSelectedDocumentId] = useState<string | null>(null)

  const documentsQuery = useQuery({
    queryKey: queryKeys.linkedDocuments.forPrompt(promptId),
    queryFn: () => listLinkedDocuments(promptId),
  })

  const documents = useMemo(() => documentsQuery.data ?? [], [documentsQuery.data])
  const effectiveSelectedDocumentId =
    selectedDocumentId && documents.some((document) => document.id === selectedDocumentId)
      ? selectedDocumentId
      : documents[0]?.id
  const selectedDocument = documents.find((document) => document.id === effectiveSelectedDocumentId)

  return (
    <div className="grid min-w-0 gap-4 xl:grid-cols-[22rem_minmax(0,1fr)]">
      <aside className="grid content-start gap-4 rounded-lg border border-border bg-card p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h2 className="text-base font-semibold text-foreground">Planos vinculados</h2>
            <p className="mt-1 text-sm text-muted-foreground">Markdown externo com historico e tempo real.</p>
          </div>
          <div className="flex shrink-0 items-center gap-1.5 rounded-md border border-border px-2 py-1 text-xs text-muted-foreground">
            <Radio className={hub.connected ? 'h-3.5 w-3.5 text-success-foreground' : 'h-3.5 w-3.5 text-destructive'} />
            {hub.connected ? 'Online' : 'Offline'}
          </div>
        </div>

        {documents.length > 0 ? (
          <p className="rounded-md border border-dashed border-input p-3 text-xs text-muted-foreground">
            Cada prompt permite um plano vinculado. Remova o atual para vincular outro.
          </p>
        ) : (
          <LinkDocumentForm promptId={promptId} onLinked={(document) => setSelectedDocumentId(document.id)} />
        )}

        <div className="grid gap-2">
          <div className="flex items-center justify-between gap-2 text-sm font-semibold text-foreground">
            <span>Markdowns</span>
            {documentsQuery.isFetching ? <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" /> : null}
          </div>

          {!documentsQuery.isLoading && !documents.length ? (
            <div className="rounded-md border border-dashed border-input p-3 text-sm text-muted-foreground">
              Nenhum markdown vinculado a este prompt.
            </div>
          ) : null}

          {documents.map((document) => (
            <Button
              key={document.id}
              type="button"
              variant="ghost"
              className={cn(
                'h-auto min-w-0 justify-start border px-3 py-2 text-left',
                effectiveSelectedDocumentId === document.id
                  ? 'border-primary bg-muted'
                  : 'border-border bg-card hover:bg-background',
              )}
              onClick={() => setSelectedDocumentId(document.id)}
            >
              <FileText className="h-4 w-4 shrink-0 text-ring" />
              <span className="grid min-w-0 flex-1 gap-1">
                <span className="truncate text-sm font-semibold text-foreground">{document.displayName}</span>
                <span className="truncate text-xs font-normal text-muted-foreground" title={document.absolutePath}>
                  {document.absolutePath}
                </span>
              </span>
              <Badge className="shrink-0" variant={statusVariants[document.status]}>
                {statusLabels[document.status]}
              </Badge>
            </Button>
          ))}
        </div>
      </aside>

      {effectiveSelectedDocumentId ? (
        <LinkedDocumentViewer
          documentId={effectiveSelectedDocumentId}
          initialDocument={selectedDocument}
          onRemoved={() => setSelectedDocumentId(null)}
        />
      ) : (
        <div className="flex min-h-[28rem] items-center justify-center rounded-lg border border-border bg-card p-4 text-center text-sm text-muted-foreground">
          Vincule um markdown para renderizar o plano e acompanhar novas versoes.
        </div>
      )}
    </div>
  )
}
