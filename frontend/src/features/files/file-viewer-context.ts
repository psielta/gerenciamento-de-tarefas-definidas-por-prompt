import { createContext } from 'react'

export type FileViewerTarget = {
  workingDirectoryId: string
  relativePath: string
}

export type FileViewerContextValue = {
  openFile: (workingDirectoryId: string, relativePath: string) => void
  closeFile: () => void
  target: FileViewerTarget | null
}

export const FileViewerContext = createContext<FileViewerContextValue | null>(null)