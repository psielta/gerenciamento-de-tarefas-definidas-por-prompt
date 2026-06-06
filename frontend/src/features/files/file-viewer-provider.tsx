import { useCallback, useMemo, useState } from 'react'
import { FileViewerContext, type FileViewerTarget } from './file-viewer-context'
import { FileViewerDrawer } from './file-viewer-drawer'

export function FileViewerProvider({ children }: { children: React.ReactNode }) {
  const [target, setTarget] = useState<FileViewerTarget | null>(null)

  const openFile = useCallback((workingDirectoryId: string, relativePath: string) => {
    const normalizedPath = relativePath.trim().replace(/\\/g, '/')
    if (!normalizedPath) {
      return
    }

    setTarget({ workingDirectoryId, relativePath: normalizedPath })
  }, [])

  const closeFile = useCallback(() => {
    setTarget(null)
  }, [])

  const value = useMemo(
    () => ({
      openFile,
      closeFile,
      target,
    }),
    [closeFile, openFile, target],
  )

  return (
    <FileViewerContext.Provider value={value}>
      {children}
      {target ? (
        <FileViewerDrawer
          workingDirectoryId={target.workingDirectoryId}
          relativePath={target.relativePath}
          onClose={closeFile}
        />
      ) : null}
    </FileViewerContext.Provider>
  )
}