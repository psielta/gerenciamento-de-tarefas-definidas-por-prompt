import { AlertTriangle, FileCode2, Loader2 } from 'lucide-react'
import { lazy, Suspense, useMemo } from 'react'
import { getErrorMessage } from '@/api/client'
import { cn } from '@/lib/utils'
import { extensionToLanguage } from './extension-to-language'
import { useFileContent } from './use-file-queries'
import { useFileSubscription } from './use-file-subscription'

const MonacoEditor = lazy(() => import('@monaco-editor/react'))

type FileViewerPanelProps = {
  workingDirectoryId: string
  relativePath: string
  className?: string
  inline?: boolean
}

const byteFormatter = new Intl.NumberFormat('pt-BR')

export function FileViewerPanel({ workingDirectoryId, relativePath, className, inline = false }: FileViewerPanelProps) {
  const contentQuery = useFileContent(workingDirectoryId, relativePath)
  useFileSubscription(workingDirectoryId, relativePath)

  const language = useMemo(() => {
    if (contentQuery.data?.language) {
      return contentQuery.data.language
    }

    const extension = relativePath.includes('.') ? relativePath.slice(relativePath.lastIndexOf('.')) : null
    return extensionToLanguage(extension)
  }, [contentQuery.data?.language, relativePath])

  const fileName = relativePath.split('/').pop() || relativePath

  return (
    <section
      className={cn(
        'grid min-h-0 grid-rows-[auto_minmax(0,1fr)] overflow-hidden rounded-lg border border-border bg-card',
        className,
      )}
    >
      <div className="flex min-w-0 items-center justify-between gap-2 border-b border-border px-3 py-2">
        <div className="flex min-w-0 items-center gap-2">
          <FileCode2 className="h-4 w-4 shrink-0 text-primary" />
          <div className="min-w-0">
            <p className="truncate font-mono text-sm font-medium text-foreground" title={relativePath}>
              {fileName}
            </p>
            {!inline ? <p className="truncate text-xs text-muted-foreground">{relativePath}</p> : null}
          </div>
        </div>
        {contentQuery.data ? (
          <span className="shrink-0 text-xs text-muted-foreground">
            {byteFormatter.format(contentQuery.data.sizeBytes)} bytes
          </span>
        ) : null}
      </div>

      <div className="min-h-0 overflow-hidden">
        {contentQuery.isLoading ? (
          <div className="flex h-full min-h-48 items-center justify-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Carregando arquivo
          </div>
        ) : null}

        {contentQuery.isError ? (
          <div className="flex h-full min-h-48 items-center justify-center px-4 text-sm text-destructive">
            {getErrorMessage(contentQuery.error)}
          </div>
        ) : null}

        {contentQuery.data?.isBinary ? (
          <div className="flex h-full min-h-48 flex-col items-center justify-center gap-2 px-4 text-center text-sm text-muted-foreground">
            <AlertTriangle className="h-5 w-5 text-warning-solid" />
            <p>Arquivo binario. Visualizacao de texto indisponivel.</p>
          </div>
        ) : null}

        {contentQuery.data && !contentQuery.data.isBinary && contentQuery.data.content !== null ? (
          <Suspense
            fallback={
              <div className="flex h-full min-h-48 items-center justify-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Preparando editor
              </div>
            }
          >
            <MonacoEditor
              value={contentQuery.data.content}
              language={language}
              theme="vs-dark"
              options={{
                readOnly: true,
                minimap: { enabled: false },
                scrollBeyondLastLine: false,
                fontFamily: 'JetBrains Mono Variable, ui-monospace, monospace',
                fontSize: 13,
                lineNumbers: 'on',
                wordWrap: 'on',
                automaticLayout: true,
              }}
              height="100%"
            />
          </Suspense>
        ) : null}

        {contentQuery.data?.isTruncated ? (
          <div className="border-t border-border bg-warning-soft px-3 py-2 text-xs text-warning-foreground">
            Arquivo truncado para visualizacao. Abra no editor local para ver o conteudo completo.
          </div>
        ) : null}
      </div>
    </section>
  )
}