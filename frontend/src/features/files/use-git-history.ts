import { useContext } from 'react'
import { GitHistoryContext } from './git-history-context'

export function useGitHistory() {
  const context = useContext(GitHistoryContext)
  if (!context) {
    throw new Error('useGitHistory must be used within GitHistoryProvider')
  }

  return context
}