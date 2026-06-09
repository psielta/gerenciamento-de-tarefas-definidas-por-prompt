const LAST_OPENED_FILE_KEY_PREFIX = 'prompt-tasks:files:last-opened:'

/**
 * Persistencia por workspace do ultimo arquivo aberto na pagina global de
 * Arquivos. Segue o mesmo try/catch de use-local-storage para degradar sem
 * erro quando o storage nao esta disponivel.
 */
export function readLastOpenedFile(workingDirectoryId: string): string | null {
  try {
    return localStorage.getItem(`${LAST_OPENED_FILE_KEY_PREFIX}${workingDirectoryId}`)
  } catch {
    return null
  }
}

export function writeLastOpenedFile(workingDirectoryId: string, relativePath: string | null) {
  try {
    if (relativePath === null) {
      localStorage.removeItem(`${LAST_OPENED_FILE_KEY_PREFIX}${workingDirectoryId}`)
      return
    }

    localStorage.setItem(`${LAST_OPENED_FILE_KEY_PREFIX}${workingDirectoryId}`, relativePath)
  } catch {
    // ignore persistence failures
  }
}
