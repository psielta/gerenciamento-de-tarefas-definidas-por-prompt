import { useQuery } from '@tanstack/react-query'
import { browseDirectory, getFileContent, searchFiles } from '@/api/files'
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

const FILE_SEARCH_LIMIT = 30

export function useFileSearch(workingDirectoryId: string, query: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.files.search(workingDirectoryId, query, FILE_SEARCH_LIMIT),
    queryFn: () => searchFiles(workingDirectoryId, query, FILE_SEARCH_LIMIT),
    enabled: Boolean(workingDirectoryId) && query.length >= 2 && enabled,
    staleTime: 10_000,
  })
}