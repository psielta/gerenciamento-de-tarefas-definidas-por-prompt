import { useQuery } from '@tanstack/react-query'
import { browseDirectory, getFileContent } from '@/api/files'
import { queryKeys } from '@/api/query-keys'

export function useDirectoryChildren(workingDirectoryId: string, relativePath: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.files.tree(workingDirectoryId, relativePath),
    queryFn: () => browseDirectory(workingDirectoryId, relativePath),
    enabled: Boolean(workingDirectoryId) && enabled,
    staleTime: 30_000,
  })
}

export function useFileContent(workingDirectoryId: string, relativePath: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.files.content(workingDirectoryId, relativePath),
    queryFn: () => getFileContent(workingDirectoryId, relativePath),
    enabled: Boolean(workingDirectoryId) && Boolean(relativePath) && enabled,
    staleTime: 0,
  })
}