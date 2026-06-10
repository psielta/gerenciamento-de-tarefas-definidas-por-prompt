import { diffLines } from 'diff'

export type GitLineChangeKind = 'added' | 'modified' | 'deleted'

export type GitLineChange = {
  kind: GitLineChangeKind
  startLineNumber: number
  endLineNumber: number
}

function normalizeLineEndings(text: string) {
  return text.replace(/\r\n/g, '\n').replace(/\r/g, '\n')
}

function countLines(value: string) {
  if (value === '') {
    return 0
  }

  const lines = value.split('\n')
  return lines[lines.length - 1] === '' ? lines.length - 1 : lines.length
}

function appendRange(changes: GitLineChange[], kind: GitLineChangeKind, startLineNumber: number, lineCount: number) {
  if (lineCount <= 0) {
    return
  }

  const start = Math.max(1, startLineNumber)
  const end = Math.max(start, start + lineCount - 1)
  const previous = changes[changes.length - 1]

  if (previous?.kind === kind && previous.endLineNumber + 1 >= start) {
    previous.endLineNumber = Math.max(previous.endLineNumber, end)
    return
  }

  changes.push({ kind, startLineNumber: start, endLineNumber: end })
}

function appendDeletionMarker(changes: GitLineChange[], currentLineNumber: number, currentLineCount: number) {
  const markerLine = Math.min(Math.max(1, currentLineNumber), currentLineCount)
  appendRange(changes, 'deleted', markerLine, 1)
}

export function getGitLineChanges(originalContent: string, currentContent: string): GitLineChange[] {
  const original = normalizeLineEndings(originalContent)
  const current = normalizeLineEndings(currentContent)
  const parts = diffLines(original, current)
  const changes: GitLineChange[] = []
  const currentLineCount = Math.max(1, countLines(current))
  let currentLineNumber = 1

  let index = 0
  while (index < parts.length) {
    const part = parts[index]
    const lineCount = countLines(part.value)

    if (part.removed && parts[index + 1]?.added) {
      const addedLineCount = countLines(parts[index + 1].value)
      const pairedLineCount = Math.min(lineCount, addedLineCount)

      appendRange(changes, 'modified', currentLineNumber, pairedLineCount)
      currentLineNumber += pairedLineCount

      appendRange(changes, 'added', currentLineNumber, addedLineCount - pairedLineCount)
      currentLineNumber += Math.max(0, addedLineCount - pairedLineCount)

      if (lineCount > pairedLineCount) {
        appendDeletionMarker(changes, currentLineNumber, currentLineCount)
      }

      index += 2
      continue
    }

    if (part.added) {
      appendRange(changes, 'added', currentLineNumber, lineCount)
      currentLineNumber += lineCount
      index++
      continue
    }

    if (part.removed) {
      appendDeletionMarker(changes, currentLineNumber, currentLineCount)
      index++
      continue
    }

    currentLineNumber += lineCount
    index++
  }

  return changes
}
