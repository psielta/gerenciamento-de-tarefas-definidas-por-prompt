import { AlertTriangle, FileCode2, Loader2 } from 'lucide-react'
import { lazy, Suspense, useMemo } from 'react'
import { getErrorMessage } from '@/api/client'
import { useTheme } from '@/components/theme/theme-provider'
import { cn } from '@/lib/utils'
import { extensionToLanguage } from './extension-to-language'
import { useFileContent } from './use-file-queries'
import { useGitCommitContent } from './use-git-queries'
import { resolveMonacoTheme } from './monaco-setup'

const MonacoDiffEditor = lazy(async () => {
  await import('./monaco-setup')
  const { DiffEditor } = await import('@monaco-editor/react')
  return { default: DiffEditor }
})

export type GitDiffSource =
  | { kind: 'hash'; hash: string }
  | { kind: 'working' }
  | { kind: 'empty' }

type GitCommitDiffViewerProps = {
  workingDirectoryId: string
  path: string
  original: GitDiffSource
  modified: GitDiffSource
  className?: string
}

function useDiffSide(
  workingDirectoryId: string,
  path: string,
  source: GitDiffSource,
) {
  const hash = source.kind === 'hash' ? source.hash : undefined
  const commitQuery = useGitCommitContent(workingDirectoryId, path, hash, source.kind === 'hash')
  const workingQuery = useFileContent(workingDirectoryId, path, source.kind === 'working')

  if (source.kind === 'empty') {
    return { content: '', isLoading: false, error: null, isBinary: false, truncated: false }
  }

  if (source.kind === 'hash') {
    return {
      content: commitQuery.data?.exists ? (commitQuery.data.content ?? '') : '',
      isLoading: commitQuery.isLoading,
      error: commitQuery.error,
      isBinary: commitQuery.data?.isBinary ?? false,
      truncated: commitQuery.data?.truncated ?? false,
    }
  }

  return {
    content: workingQuery.data?.content ?? '',
    isLoading: workingQuery.isLoading,
    error: workingQuery.error,
    isBinary: workingQuery.data?.isBinary ?? false,
    truncated: workingQuery.data?.truncated ?? false,
  }
}

export function GitCommitDiffViewer({
  workingDirectoryId,
  path,
  original,
  modified,
  className,
}: GitCommitDiffViewerProps) {
  const originalSide = useDiffSide(workingDirectoryId, path, original)
  const modifiedSide = useDiffSide(workingDirectoryId, path, modified)
  const { resolvedTheme } = useTheme()

  const language = useMemo(() => {
    const extension = path.includes('.') ? path.slice(path.lastIndexOf('.')) : null
    return extensionToLanguage(extension)
  }, [path])

  const fileName = path.split('/').pop() || path
  const isLoading = originalSide.isLoading || modifiedSide.isLoading
  const error = originalSide.error ?? modifiedSide.error
  const isBinary = originalSide.isBinary || modifiedSide.isBinary
  const truncated = originalSide.truncated || modifiedSide.truncated

  return (
    <section
      className={cn(
        'grid min-h-0 grid-rows-[auto_minmax(0,1fr)_auto] overflow-hidden rounded-lg border border-border bg-card',
        className,
      )}
    >
      <div className="flex min-w-0 items-center gap-2 border-b border-border px-3 py-2">
        <FileCode2 className="h-4 w-4 shrink-0 text-primary" />
        <div className="min-w-0">
          <p className="truncate font-mono text-sm font-medium text-foreground" title={path}>
            {fileName}
          </p>
          <p className="truncate text-xs text-muted-foreground">{path}</p>
        </div>
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

        {!isLoading && !error && !isBinary && truncated ? (
          <div className="flex h-full min-h-48 flex-col items-center justify-center gap-2 px-4 text-center text-sm text-muted-foreground">
            <AlertTriangle className="h-5 w-5 text-warning-solid" />
            <p>Arquivo truncado para visualizacao. Abra no editor local para ver o conteudo completo.</p>
          </div>
        ) : null}

        {!isLoading && !error && !isBinary && !truncated ? (
          <Suspense
            fallback={
              <div className="flex h-full min-h-48 items-center justify-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Preparando editor
              </div>
            }
          >
            <MonacoDiffEditor
              original={originalSide.content}
              modified={modifiedSide.content}
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