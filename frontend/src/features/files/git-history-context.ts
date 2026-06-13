import { createContext } from 'react'

export type GitHistoryTarget = {
  workingDirectoryId: string
  relativePath: string
}

export type GitHistoryContextValue = {
  openHistory: (workingDirectoryId: string, relativePath: string) => void
  closeHistory: () => void
  target: GitHistoryTarget | null
}

export const GitHistoryContext = createContext<GitHistoryContextValue | null>(null)