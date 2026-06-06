export function createFileKey(relativePath: string) {
  return relativePath.trim().replace(/^@/, '').replace(/\\/g, '/').toLowerCase()
}

export function fileSubscriptionKey(workingDirectoryId: string, relativePath: string) {
  return `${workingDirectoryId}::${createFileKey(relativePath)}`
}

export function parentDirectoryPath(relativePath: string) {
  const normalized = relativePath.trim().replace(/\\/g, '/')
  if (!normalized) {
    return ''
  }

  const lastSlash = normalized.lastIndexOf('/')
  return lastSlash === -1 ? '' : normalized.slice(0, lastSlash)
}