import { AlertTriangle, Loader2 } from 'lucide-react'
import type { GitCommit } from '@/api/schemas'
import { getErrorMessage } from '@/api/client'
import { cn } from '@/lib/utils'

export type GitCompareMode = 'parent' | 'working' | 'two'

type GitHistoryPanelProps = {
  commits: GitCommit[]
  isRepository: boolean
  isLoading: boolean
  error: unknown
  selectedHash: string | null
  compareMode: GitCompareMode
  baseHash: string | null
  targetHash: string | null
  onSelectCommit: (hash: string) => void
  onChangeMode: (mode: GitCompareMode) => void
  onPickBase: (hash: string) => void
  onPickTarget: (hash: string) => void
  className?: string
}

export function GitHistoryPanel({
  commits,
  isRepository,
  isLoading,
  error,
  selectedHash,
  compareMode,
  baseHash,
  targetHash,
  onSelectCommit,
  onChangeMode,
  onPickBase,
  onPickTarget,
  className,
}: GitHistoryPanelProps) {
  return (
    <section
      className={cn(
        'grid min-h-0 grid-rows-[auto_minmax(0,1fr)] overflow-hidden rounded-lg border border-border bg-card',
        className,
      )}
    >
      <div className="border-b border-border px-3 py-2">
        <h3 className="text-sm font-semibold text-foreground">Historico de commits</h3>
        <div className="mt-2 flex flex-wrap gap-1">
          <CompareModeButton active={compareMode === 'parent'} onClick={() => onChangeMode('parent')}>
            vs parent
          </CompareModeButton>
          <CompareModeButton active={compareMode === 'working'} onClick={() => onChangeMode('working')}>
            vs working tree
          </CompareModeButton>
          <CompareModeButton active={compareMode === 'two'} onClick={() => onChangeMode('two')}>
            compare two
          </CompareModeButton>
        </div>
      </div>

      <div className="min-h-0 overflow-auto px-1 py-1">
        {isLoading ? (
          <div className="flex items-center gap-2 px-2 py-2 text-xs text-muted-foreground">
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
            Carregando
          </div>
        ) : null}

        {error ? (
          <div className="flex items-start gap-2 px-2 py-2 text-xs text-destructive">
            <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
            <span>{getErrorMessage(error)}</span>
          </div>
        ) : null}

        {!isLoading && !error && !isRepository ? (
          <div className="px-2 py-2 text-xs text-muted-foreground">Nao e um repositorio git.</div>
        ) : null}

        {!isLoading && !error && isRepository && !commits.length ? (
          <div className="px-2 py-2 text-xs text-muted-foreground">Sem historico para este arquivo.</div>
        ) : null}

        <ul className="grid gap-0.5">
          {commits.map((commit) => {
            const isSelected = selectedHash === commit.hash
            const isBase = compareMode === 'two' && baseHash === commit.hash
            const isTarget = compareMode === 'two' && targetHash === commit.hash

            return (
              <li key={commit.hash}>
                <button
                  type="button"
                  onClick={() => onSelectCommit(commit.hash)}
                  className={cn(
                    'flex w-full min-w-0 flex-col gap-0.5 rounded-md px-2 py-1.5 text-left text-xs transition-colors hover:bg-muted',
                    isSelected && 'bg-accent text-foreground',
                  )}
                >
                  <span className="flex min-w-0 items-center gap-2">
                    <span className="shrink-0 font-mono text-[0.68rem] text-primary">{commit.shortHash}</span>
                    <span className="truncate font-medium text-foreground">{commit.message || '(sem assunto)'}</span>
                  </span>
                  <span className="truncate text-[0.68rem] text-muted-foreground">
                    {commit.author} · {formatCommitDate(commit.date)}
                  </span>
                  {compareMode === 'two' ? (
                    <span className="flex flex-wrap gap-1 pt-0.5">
                      <button
                        type="button"
                        className={cn(
                          'rounded border px-1.5 py-0.5 font-mono text-[0.62rem]',
                          isBase ? 'border-primary bg-primary/10 text-primary' : 'border-border text-muted-foreground',
                        )}
                        onClick={(event) => {
                          event.stopPropagation()
                          onPickBase(commit.hash)
                        }}
                      >
                        base
                      </button>
                      <button
                        type="button"
                        className={cn(
                          'rounded border px-1.5 py-0.5 font-mono text-[0.62rem]',
                          isTarget ? 'border-primary bg-primary/10 text-primary' : 'border-border text-muted-foreground',
                        )}
                        onClick={(event) => {
                          event.stopPropagation()
                          onPickTarget(commit.hash)
                        }}
                      >
                        target
                      </button>
                    </span>
                  ) : null}
                </button>
              </li>
            )
          })}
        </ul>
      </div>
    </section>
  )
}

function CompareModeButton({
  active,
  onClick,
  children,
}: {
  active: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'rounded border px-2 py-0.5 text-[0.68rem] transition-colors',
        active
          ? 'border-primary bg-primary/10 text-primary'
          : 'border-border text-muted-foreground hover:bg-muted hover:text-foreground',
      )}
    >
      {children}
    </button>
  )
}

function formatCommitDate(value: string) {
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  return parsed.toLocaleString()
}