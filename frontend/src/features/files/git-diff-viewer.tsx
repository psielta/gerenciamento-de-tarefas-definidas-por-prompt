import { AlertTriangle, FileCode2, Loader2 } from 'lucide-react'
import { lazy, Suspense, useMemo } from 'react'
import { getErrorMessage } from '@/api/client'
import type { GitFileStatusValue } from '@/api/schemas'
import { useTheme } from '@/components/theme/theme-provider'
import { cn } from '@/lib/utils'
import { extensionToLanguage } from './extension-to-language'
import { getGitStatusMeta } from './git-status-meta'
import { useFileContent } from './use-file-queries'
import { useFileSubscription } from './use-file-subscription'
import { useGitOriginalFile } from './use-git-queries'

import { resolveMonacoTheme } from './monaco-setup'

const MonacoDiffEditor = lazy(async () => {
  await import('./monaco-setup')
  const { DiffEditor } = await import('@monaco-editor/react')
  return { default: DiffEditor }
})

type GitDiffViewerProps = {
  workingDirectoryId: string
  path: string
  originalPath?: string | null
  status?: GitFileStatusValue
  className?: string
}

export function GitDiffViewer({ workingDirectoryId, path, originalPath, status, className }: GitDiffViewerProps) {
  const skipOriginal = status === 'Untracked' || status === 'Added'
  const skipCurrent = status === 'Deleted'
  const originalQuery = useGitOriginalFile(workingDirectoryId, originalPath ?? path, !skipOriginal)
  const currentQuery = useFileContent(workingDirectoryId, path, !skipCurrent)
  useFileSubscription(workingDirectoryId, skipCurrent ? undefined : path)
  const { resolvedTheme } = useTheme()

  const language = useMemo(() => {
    const extension = path.includes('.') ? path.slice(path.lastIndexOf('.')) : null
    return extensionToLanguage(extension)
  }, [path])

  const fileName = path.split('/').pop() || path
  const meta = status ? getGitStatusMeta(status) : null
  const originalContent = skipOriginal ? '' : (originalQuery.data?.content ?? '')
  const currentContent = skipCurrent ? '' : (currentQuery.data?.content ?? '')
  const isLoading = (!skipOriginal && originalQuery.isLoading) || (!skipCurrent && currentQuery.isLoading)
  const error = originalQuery.error ?? currentQuery.error
  const isBinary = !skipCurrent && currentQuery.data?.isBinary

  return (
    <section
      className={cn(
        'grid min-h-0 grid-rows-[auto_minmax(0,1fr)_auto] overflow-hidden rounded-lg border border-border bg-card',
        className,
      )}
    >
      <div className="flex min-w-0 items-center justify-between gap-2 border-b border-border px-3 py-2">
        <div className="flex min-w-0 items-center gap-2">
          <FileCode2 className="h-4 w-4 shrink-0 text-primary" />
          <div className="min-w-0">
            <p className="truncate font-mono text-sm font-medium text-foreground" title={path}>
              {fileName}
            </p>
            <p className="truncate text-xs text-muted-foreground">
              {status === 'Renamed' && originalPath ? `${originalPath} -> ${path}` : path}
            </p>
          </div>
        </div>
        {meta ? (
          <span
            className={cn('shrink-0 rounded px-1.5 py-0.5 font-mono text-[0.68rem] font-semibold', meta.badgeClass)}
            title={meta.label}
          >
            {meta.letter}
          </span>
        ) : null}
      </div>

      <div className="min-h-0 overflow-hidden">
        {isLoading ? <LoadingSkeleton /> : null}

        {error ? (
          <div className="m-4 flex items-start gap-2 rounded-md border border-danger-border bg-danger-soft p-3 text-sm text-danger-soft-foreground">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>{getErrorMessage(error)}</span>
          </div>
        ) : null}

        {isBinary ? (
          <div className="flex h-full min-h-48 flex-col items-center justify-center gap-2 px-4 text-center text-sm text-muted-foreground">
            <AlertTriangle className="h-5 w-5 text-warning-solid" />
            <p>Arquivo binario. Visualizacao de diff indisponivel.</p>
          </div>
        ) : null}

        {!isLoading && !error && !isBinary ? (
          <Suspense
            fallback={
              <div className="flex h-full min-h-48 items-center justify-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Preparando editor
              </div>
            }
          >
            <MonacoDiffEditor
              original={originalContent}
              modified={currentContent}
              language={language}
              theme={resolveMonacoTheme(resolvedTheme)}
              height="100%"
              options={{
                readOnly: true,
                renderSideBySide: true,
                automaticLayout: true,
                scrollBeyondLastLine: false,
                fontFamily: 'JetBrains Mono Variable, ui-monospace, monospace',
                minimap: { enabled: false },
                hideUnchangedRegions: { enabled: true },
              }}
            />
          </Suspense>
        ) : null}
      </div>

      {!skipCurrent && currentQuery.data?.truncated ? (
        <div className="border-t border-border bg-warning-soft px-3 py-2 text-xs text-warning-foreground">
          Arquivo truncado para visualizacao. Abra no editor local para ver o conteudo completo.
        </div>
      ) : null}
    </section>
  )
}

function LoadingSkeleton() {
  return (
    <div className="grid gap-2 p-4">
      {[70, 90, 55, 80, 65, 75, 50, 85].map((width, index) => (
        <div key={index} className="h-4 animate-pulse rounded bg-muted" style={{ width: `${width}%` }} />
      ))}
    </div>
  )
}
