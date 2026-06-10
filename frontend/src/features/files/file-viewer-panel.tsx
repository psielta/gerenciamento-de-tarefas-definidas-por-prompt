import {
  AlertTriangle,
  BookOpen,
  Code2,
  Copy,
  FileCode2,
  Loader2,
  Map as MapIcon,
  TableOfContents,
  WrapText,
  ZoomIn,
  ZoomOut,
} from 'lucide-react'
import type { editor } from 'monaco-editor'
import { lazy, Suspense, useCallback, useEffect, useMemo, useRef } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { useTheme } from '@/components/theme/theme-provider'
import { useLocalStorage } from '@/hooks/use-local-storage'
import { cn } from '@/lib/utils'
import { createFileKey } from './file-key'
import { extensionToLanguage } from './extension-to-language'
import { getGitLineChanges, type GitLineChange, type GitLineChangeKind } from './git-line-changes'
import { MarkdownFilePreview } from './markdown-file-preview'
import { resolveMonacoTheme } from './monaco-setup'
import { useFileContent } from './use-file-queries'
import { useFileSubscription } from './use-file-subscription'
import { useGitOriginalFile, useGitStatus } from './use-git-queries'

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

type MonacoNamespace = typeof import('monaco-editor')
type MonacoEditorInstance = editor.IStandaloneCodeEditor
type MonacoDecorationCollection = ReturnType<MonacoEditorInstance['createDecorationsCollection']>

const byteFormatter = new Intl.NumberFormat('pt-BR')

// Preferencias do viewer compartilhadas entre todas as superficies (explorer
// inline, modo expandido e drawer), persistidas no mesmo padrao das demais
// chaves de arquivos.
const FONT_SIZE_STORAGE_KEY = 'prompt-tasks:files:editor-font-size'
const MINIMAP_STORAGE_KEY = 'prompt-tasks:files:editor-minimap'
const WORD_WRAP_STORAGE_KEY = 'prompt-tasks:files:editor-word-wrap'
const MARKDOWN_VIEW_STORAGE_KEY = 'prompt-tasks:files:markdown-view'
const MARKDOWN_OUTLINE_STORAGE_KEY = 'prompt-tasks:files:markdown-outline'
const FONT_SIZE_DEFAULT = 13
const FONT_SIZE_MIN = 10
const FONT_SIZE_MAX = 28
const GIT_DECORATION_COLORS: Record<GitLineChangeKind, string> = {
  added: '#2ea043',
  modified: '#d29922',
  deleted: '#f85149',
}
const GIT_DECORATION_LABELS: Record<GitLineChangeKind, string> = {
  added: 'Linha adicionada no Git',
  modified: 'Linha modificada no Git',
  deleted: 'Linha removida no Git',
}

function clampFontSize(size: number) {
  if (Number.isNaN(size)) {
    return FONT_SIZE_DEFAULT
  }

  return Math.min(Math.max(size, FONT_SIZE_MIN), FONT_SIZE_MAX)
}

type ToolbarIconButtonProps = {
  onClick: () => void
  title: string
  ariaLabel: string
  active?: boolean
  children: React.ReactNode
}

function ToolbarIconButton({ onClick, title, ariaLabel, active, children }: ToolbarIconButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={title}
      aria-label={ariaLabel}
      aria-pressed={active}
      className={cn(
        'rounded p-1 transition-colors hover:bg-secondary hover:text-foreground',
        active ? 'bg-secondary text-foreground' : 'text-muted-foreground',
      )}
    >
      {children}
    </button>
  )
}

function toMonacoGitDecoration(change: GitLineChange, monaco: MonacoNamespace): editor.IModelDeltaDecoration {
  const startLineNumber = Math.max(1, change.startLineNumber)
  const endLineNumber = Math.max(startLineNumber, change.endLineNumber)
  const color = GIT_DECORATION_COLORS[change.kind]

  return {
    range: new monaco.Range(startLineNumber, 1, endLineNumber, 1),
    options: {
      isWholeLine: true,
      className: `git-line-change-background git-line-change-background-${change.kind}`,
      linesDecorationsClassName: `git-line-change-gutter git-line-change-gutter-${change.kind}`,
      glyphMarginClassName: `git-line-change-glyph git-line-change-glyph-${change.kind}`,
      hoverMessage: { value: GIT_DECORATION_LABELS[change.kind] },
      overviewRuler: {
        color,
        position: monaco.editor.OverviewRulerLane.Left,
      },
      minimap: {
        color,
        position: monaco.editor.MinimapPosition.Gutter,
      },
    },
  }
}

export function FileViewerPanel({ workingDirectoryId, relativePath, className, inline = false }: FileViewerPanelProps) {
  const contentQuery = useFileContent(workingDirectoryId, relativePath)
  useFileSubscription(workingDirectoryId, contentQuery.data?.relativePath ?? relativePath, contentQuery.isSuccess)
  const gitStatusQuery = useGitStatus(workingDirectoryId)
  const { resolvedTheme } = useTheme()
  const monacoRef = useRef<MonacoNamespace | null>(null)
  const decorationsRef = useRef<MonacoDecorationCollection | null>(null)

  const [storedFontSize, setStoredFontSize] = useLocalStorage(FONT_SIZE_STORAGE_KEY, String(FONT_SIZE_DEFAULT))
  const [minimapPref, setMinimapPref] = useLocalStorage(MINIMAP_STORAGE_KEY, 'on')
  const [wordWrapPref, setWordWrapPref] = useLocalStorage(WORD_WRAP_STORAGE_KEY, 'on')
  const [markdownViewPref, setMarkdownViewPref] = useLocalStorage(MARKDOWN_VIEW_STORAGE_KEY, 'code')
  const [outlinePref, setOutlinePref] = useLocalStorage(MARKDOWN_OUTLINE_STORAGE_KEY, 'on')

  const fontSize = clampFontSize(Number.parseInt(storedFontSize, 10))
  const minimapEnabled = minimapPref !== 'off'
  const wordWrapEnabled = wordWrapPref !== 'off'
  const outlineEnabled = outlinePref !== 'off'

  const language = useMemo(() => {
    const extension = relativePath.includes('.') ? relativePath.slice(relativePath.lastIndexOf('.')) : null
    return extensionToLanguage(extension)
  }, [relativePath])

  const fileName = relativePath.split('/').pop() || relativePath
  const isMarkdown = language === 'markdown'
  const hasTextContent = Boolean(contentQuery.data && !contentQuery.data.isBinary)
  const showMarkdownPreview = isMarkdown && hasTextContent && markdownViewPref === 'preview'
  const gitStatus = useMemo(() => {
    const fileKey = createFileKey(relativePath)
    return gitStatusQuery.data?.find((entry) => createFileKey(entry.path) === fileKey)
  }, [gitStatusQuery.data, relativePath])
  const shouldCompareWithOriginal = Boolean(
    gitStatus &&
      gitStatus.status !== 'Added' &&
      gitStatus.status !== 'Deleted' &&
      gitStatus.status !== 'Untracked' &&
      contentQuery.data &&
      !contentQuery.data.isBinary &&
      !contentQuery.data.truncated &&
      !showMarkdownPreview,
  )
  const originalGitFileQuery = useGitOriginalFile(
    workingDirectoryId,
    gitStatus?.originalPath ?? relativePath,
    shouldCompareWithOriginal,
  )
  const gitLineChanges = useMemo(() => {
    if (!gitStatus || !contentQuery.data || contentQuery.data.isBinary || contentQuery.data.truncated) {
      return []
    }

    if (gitStatus.status === 'Added' || gitStatus.status === 'Untracked') {
      return getGitLineChanges('', contentQuery.data.content)
    }

    if (gitStatus.status === 'Deleted' || !originalGitFileQuery.data) {
      return []
    }

    return getGitLineChanges(originalGitFileQuery.data.content, contentQuery.data.content)
  }, [contentQuery.data, gitStatus, originalGitFileQuery.data])
  const applyGitDecorations = useCallback((changes: GitLineChange[]) => {
    const decorations = decorationsRef.current
    const monaco = monacoRef.current

    if (!decorations || !monaco) {
      return
    }

    decorations.set(changes.map((change) => toMonacoGitDecoration(change, monaco)))
  }, [])
  const handleEditorMount = useCallback(
    (editorInstance: MonacoEditorInstance, monaco: MonacoNamespace) => {
      decorationsRef.current?.clear()
      monacoRef.current = monaco
      decorationsRef.current = editorInstance.createDecorationsCollection()
      applyGitDecorations(gitLineChanges)
    },
    [applyGitDecorations, gitLineChanges],
  )

  useEffect(() => {
    applyGitDecorations(gitLineChanges)
  }, [applyGitDecorations, gitLineChanges])

  useEffect(() => {
    return () => {
      decorationsRef.current?.clear()
      decorationsRef.current = null
      monacoRef.current = null
    }
  }, [])

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
        <div className="flex shrink-0 flex-wrap items-center justify-end gap-1.5">
          {isMarkdown && hasTextContent ? (
            <div
              role="group"
              aria-label="Modo de visualizacao do markdown"
              className="flex shrink-0 items-center overflow-hidden rounded-md border border-border"
            >
              <button
                type="button"
                onClick={() => setMarkdownViewPref('code')}
                aria-pressed={!showMarkdownPreview}
                title="Ver codigo-fonte no editor"
                className={cn(
                  'flex items-center gap-1 px-2 py-1 text-xs font-medium transition-colors',
                  showMarkdownPreview
                    ? 'text-muted-foreground hover:bg-secondary hover:text-foreground'
                    : 'bg-secondary text-foreground',
                )}
              >
                <Code2 className="h-3.5 w-3.5" />
                Codigo
              </button>
              <button
                type="button"
                onClick={() => setMarkdownViewPref('preview')}
                aria-pressed={showMarkdownPreview}
                title="Ver markdown renderizado"
                className={cn(
                  'flex items-center gap-1 px-2 py-1 text-xs font-medium transition-colors',
                  showMarkdownPreview
                    ? 'bg-secondary text-foreground'
                    : 'text-muted-foreground hover:bg-secondary hover:text-foreground',
                )}
              >
                <BookOpen className="h-3.5 w-3.5" />
                Visual
              </button>
            </div>
          ) : null}

          {showMarkdownPreview ? (
            <ToolbarIconButton
              onClick={() => setOutlinePref(outlineEnabled ? 'off' : 'on')}
              title={outlineEnabled ? 'Ocultar sumario' : 'Mostrar sumario'}
              ariaLabel="Alternar sumario"
              active={outlineEnabled}
            >
              <TableOfContents className="h-3.5 w-3.5" />
            </ToolbarIconButton>
          ) : null}

          {hasTextContent && !showMarkdownPreview ? (
            <div className="flex items-center gap-0.5">
              <ToolbarIconButton
                onClick={() => setStoredFontSize(String(clampFontSize(fontSize - 1)))}
                title="Diminuir fonte"
                ariaLabel="Diminuir fonte do editor"
              >
                <ZoomOut className="h-3.5 w-3.5" />
              </ToolbarIconButton>
              <button
                type="button"
                onClick={() => setStoredFontSize(String(FONT_SIZE_DEFAULT))}
                title="Restaurar tamanho padrao da fonte"
                aria-label="Restaurar tamanho padrao da fonte"
                className="rounded px-1 py-0.5 font-mono text-[0.65rem] tabular-nums text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
              >
                {fontSize}px
              </button>
              <ToolbarIconButton
                onClick={() => setStoredFontSize(String(clampFontSize(fontSize + 1)))}
                title="Aumentar fonte (Ctrl+scroll no editor tambem aplica zoom)"
                ariaLabel="Aumentar fonte do editor"
              >
                <ZoomIn className="h-3.5 w-3.5" />
              </ToolbarIconButton>
              <ToolbarIconButton
                onClick={() => setMinimapPref(minimapEnabled ? 'off' : 'on')}
                title={minimapEnabled ? 'Ocultar minimapa' : 'Mostrar minimapa'}
                ariaLabel="Alternar minimapa"
                active={minimapEnabled}
              >
                <MapIcon className="h-3.5 w-3.5" />
              </ToolbarIconButton>
              <ToolbarIconButton
                onClick={() => setWordWrapPref(wordWrapEnabled ? 'off' : 'on')}
                title={wordWrapEnabled ? 'Desativar quebra de linha' : 'Ativar quebra de linha'}
                ariaLabel="Alternar quebra de linha"
                active={wordWrapEnabled}
              >
                <WrapText className="h-3.5 w-3.5" />
              </ToolbarIconButton>
            </div>
          ) : null}

          {contentQuery.data ? (
            <span className="hidden text-xs text-muted-foreground sm:inline">
              {byteFormatter.format(contentQuery.data.sizeBytes)} bytes
            </span>
          ) : null}
          <ToolbarIconButton
            onClick={() => void copyRelativePath()}
            title="Copiar caminho relativo"
            ariaLabel="Copiar caminho relativo"
          >
            <Copy className="h-3.5 w-3.5" />
          </ToolbarIconButton>
        </div>
      </div>

      <div className="grid min-h-0 grid-rows-[minmax(0,1fr)_auto]">
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

          {contentQuery.data && !contentQuery.data.isBinary && showMarkdownPreview ? (
            <MarkdownFilePreview content={contentQuery.data.content} showOutline={outlineEnabled} />
          ) : null}

          {contentQuery.data && !contentQuery.data.isBinary && !showMarkdownPreview ? (
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
                theme={resolveMonacoTheme(resolvedTheme)}
                onMount={handleEditorMount}
                options={{
                  readOnly: true,
                  domReadOnly: true,
                  glyphMargin: true,
                  minimap: { enabled: minimapEnabled },
                  scrollBeyondLastLine: false,
                  fontFamily: 'JetBrains Mono Variable, ui-monospace, monospace',
                  fontSize,
                  lineNumbers: 'on',
                  wordWrap: wordWrapEnabled ? 'on' : 'off',
                  automaticLayout: true,
                  mouseWheelZoom: true,
                  stickyScroll: { enabled: true },
                  folding: true,
                  showFoldingControls: 'always',
                  guides: { indentation: true, bracketPairs: 'active' },
                  bracketPairColorization: { enabled: true },
                  renderLineHighlight: 'all',
                  smoothScrolling: true,
                  unicodeHighlight: { ambiguousCharacters: false },
                }}
                height="100%"
              />
            </Suspense>
          ) : null}
        </div>

        {contentQuery.data?.truncated ? (
          <div className="border-t border-border bg-warning-soft px-3 py-2 text-xs text-warning-foreground">
            Arquivo truncado para visualizacao. Abra no editor local para ver o conteudo completo.
          </div>
        ) : null}
      </div>
    </section>
  )
}
