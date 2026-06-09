import { AlertTriangle, Copy, FileCode2, Loader2 } from 'lucide-react'
import { lazy, Suspense, useMemo } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { useTheme } from '@/components/theme/theme-provider'
import { cn } from '@/lib/utils'
import { extensionToLanguage } from './extension-to-language'
import { useFileContent } from './use-file-queries'
import { useFileSubscription } from './use-file-subscription'

const MonacoEditor = lazy(async () => {
  await import('./monaco-setup')
  return import('@monaco-editor/react')
})

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
  const { resolvedTheme } = useTheme()

  const language = useMemo(() => {
    const extension = relativePath.includes('.') ? relativePath.slice(relativePath.lastIndexOf('.')) : null
    return extensionToLanguage(extension)
  }, [relativePath])

  const fileName = relativePath.split('/').pop() || relativePath

  const copyRelativePath = async () => {
    try {
      if (!navigator.clipboard?.writeText) {
        throw new Error('Área de transferência indisponível neste navegador.')
      }

      await navigator.clipboard.writeText(relativePath)
      toast.success('Caminho relativo copiado.')
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

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
        <div className="flex shrink-0 items-center gap-1.5">
          {contentQuery.data ? (
            <span className="text-xs text-muted-foreground">
              {byteFormatter.format(contentQuery.data.sizeBytes)} bytes
            </span>
          ) : null}
          <button
            type="button"
            onClick={() => void copyRelativePath()}
            title="Copiar caminho relativo"
            aria-label="Copiar caminho relativo"
            className="rounded p-1 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
          >
            <Copy className="h-3.5 w-3.5" />
          </button>
        </div>
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

        {contentQuery.data && !contentQuery.data.isBinary ? (
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
              theme={resolvedTheme === 'dark' ? 'vs-dark' : 'vs'}
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

        {contentQuery.data?.truncated ? (
          <div className="border-t border-border bg-warning-soft px-3 py-2 text-xs text-warning-foreground">
            Arquivo truncado para visualizacao. Abra no editor local para ver o conteudo completo.
          </div>
        ) : null}
      </div>
    </section>
  )
}