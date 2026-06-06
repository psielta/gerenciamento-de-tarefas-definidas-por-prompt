import { useEffect } from 'react'
import { usePromptHub } from '@/realtime/prompt-hub'

export function useFileSubscription(workingDirectoryId: string | undefined, relativePath: string | undefined) {
  const { joinFile, leaveFile } = usePromptHub()

  useEffect(() => {
    if (!workingDirectoryId || !relativePath) {
      return
    }

    joinFile(workingDirectoryId, relativePath)
    return () => leaveFile(workingDirectoryId, relativePath)
  }, [joinFile, leaveFile, relativePath, workingDirectoryId])
}