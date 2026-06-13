import { useCallback, useMemo, useState } from 'react'
import { GitHistoryContext, type GitHistoryTarget } from './git-history-context'
import { GitHistoryDrawer } from './git-history-drawer'

export function GitHistoryProvider({ children }: { children: React.ReactNode }) {
  const [target, setTarget] = useState<GitHistoryTarget | null>(null)

  const openHistory = useCallback((workingDirectoryId: string, relativePath: string) => {
    const normalizedPath = relativePath.trim().replace(/\\/g, '/')
    if (!normalizedPath) {
      return
    }

    setTarget({ workingDirectoryId, relativePath: normalizedPath })
  }, [])

  const closeHistory = useCallback(() => {
    setTarget(null)
  }, [])

  const value = useMemo(
    () => ({
      openHistory,
      closeHistory,
      target,
    }),
    [closeHistory, openHistory, target],
  )

  return (
    <GitHistoryContext.Provider value={value}>
      {children}
      {target ? (
        <GitHistoryDrawer
          workingDirectoryId={target.workingDirectoryId}
          relativePath={target.relativePath}
          onClose={closeHistory}
        />
      ) : null}
    </GitHistoryContext.Provider>
  )
}