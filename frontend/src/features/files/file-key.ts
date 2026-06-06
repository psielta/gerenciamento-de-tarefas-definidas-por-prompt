export function fileKey(workingDirectoryId: string, relativePath: string) {
  const normalized = relativePath.trim().replace(/\\/g, '/').toLocaleLowerCase()
  return `${workingDirectoryId}:${normalized}`
}

export function parentDirectoryPath(relativePath: string) {
  const normalized = relativePath.trim().replace(/\\/g, '/')
  if (!normalized) {
    return ''
  }

  const lastSlash = normalized.lastIndexOf('/')
  return lastSlash === -1 ? '' : normalized.slice(0, lastSlash)
}