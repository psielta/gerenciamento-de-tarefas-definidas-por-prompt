import { X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import type { GitDiffSource } from './git-commit-diff-viewer'
import { GitCommitDiffViewer } from './git-commit-diff-viewer'
import { GitHistoryPanel, type GitCompareMode } from './git-history-panel'
import { useFileGitHistory } from './use-git-queries'

type GitHistoryDrawerProps = {
  workingDirectoryId: string
  relativePath: string
  onClose: () => void
}

export function GitHistoryDrawer({ workingDirectoryId, relativePath, onClose }: GitHistoryDrawerProps) {
  return (
    <div
      className="fixed inset-0 z-[80] flex justify-end bg-black/50 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="git-history-drawer-title"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
    >
      <div className="grid h-full w-full max-w-[min(96vw,72rem)] grid-rows-[auto_minmax(0,1fr)] border-l border-border bg-card shadow-2xl">
        <div className="flex min-w-0 items-center justify-between gap-2 border-b border-border px-4 py-2.5">
          <div className="min-w-0">
            <h2 id="git-history-drawer-title" className="truncate text-base font-semibold text-foreground">
              Historico do git
            </h2>
            <p className="truncate font-mono text-xs text-muted-foreground" title={relativePath}>
              {relativePath}
            </p>
          </div>

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

        <GitHistoryDrawerBody
          key={`${workingDirectoryId}:${relativePath}`}
          workingDirectoryId={workingDirectoryId}
          relativePath={relativePath}
        />
      </div>
    </div>
  )
}

function GitHistoryDrawerBody({
  workingDirectoryId,
  relativePath,
}: {
  workingDirectoryId: string
  relativePath: string
}) {
  const historyQuery = useFileGitHistory(workingDirectoryId, relativePath)
  const commits = useMemo(() => historyQuery.data?.commits ?? [], [historyQuery.data?.commits])
  const isRepository = historyQuery.data?.isRepository ?? true

  const [selectedHash, setSelectedHash] = useState<string | null>(null)
  const [compareMode, setCompareMode] = useState<GitCompareMode>('parent')
  const [baseHash, setBaseHash] = useState<string | null>(null)
  const [targetHash, setTargetHash] = useState<string | null>(null)

  const effectiveSelectedHash = selectedHash ?? commits[0]?.hash ?? null

  const selectedCommit = useMemo(
    () => commits.find((commit) => commit.hash === effectiveSelectedHash) ?? null,
    [commits, effectiveSelectedHash],
  )

  const { original, modified } = useMemo(() => {
    if (compareMode === 'two') {
      return {
        original: toHashSource(baseHash),
        modified: toHashSource(targetHash),
      }
    }

    if (!selectedCommit) {
      return { original: { kind: 'empty' } as GitDiffSource, modified: { kind: 'empty' } as GitDiffSource }
    }

    if (compareMode === 'working') {
      return {
        original: toHashSource(selectedCommit.hash),
        modified: { kind: 'working' } as GitDiffSource,
      }
    }

    return {
      original: selectedCommit.parentHash ? toHashSource(selectedCommit.parentHash) : ({ kind: 'empty' } as GitDiffSource),
      modified: toHashSource(selectedCommit.hash),
    }
  }, [baseHash, compareMode, selectedCommit, targetHash])

  const handleSelectCommit = (hash: string) => {
    setSelectedHash(hash)
    if (compareMode === 'two') {
      if (!baseHash) {
        setBaseHash(hash)
        return
      }

      if (!targetHash) {
        setTargetHash(hash)
      }
    }
  }

  return (
    <div className="grid min-h-0 gap-3 overflow-hidden p-3 lg:grid-cols-[18rem_minmax(0,1fr)]">
      <GitHistoryPanel
        commits={commits}
        isRepository={isRepository}
        isLoading={historyQuery.isLoading}
        error={historyQuery.error}
        selectedHash={effectiveSelectedHash}
        compareMode={compareMode}
        baseHash={baseHash}
        targetHash={targetHash}
        onSelectCommit={handleSelectCommit}
        onChangeMode={setCompareMode}
        onPickBase={setBaseHash}
        onPickTarget={setTargetHash}
        className="min-h-0"
      />
      <GitCommitDiffViewer
        workingDirectoryId={workingDirectoryId}
        path={relativePath}
        original={original}
        modified={modified}
        className="min-h-0"
      />
    </div>
  )
}

function toHashSource(hash: string | null): GitDiffSource {
  return hash ? { kind: 'hash', hash } : { kind: 'empty' }
}