import { describe, expect, it } from 'vitest'
import { getGitLineChanges } from './git-line-changes'

describe('getGitLineChanges', () => {
  it('returns no changes for identical content', () => {
    expect(getGitLineChanges('const total = 1\n', 'const total = 1\n')).toEqual([])
  })

  it('ignores CRLF versus LF line ending noise', () => {
    expect(getGitLineChanges('line one\r\nline two\r\n', 'line one\nline two\n')).toEqual([])
  })

  it('marks inserted lines as added', () => {
    expect(getGitLineChanges('one\nthree', 'one\ntwo\nthree')).toEqual([
      { kind: 'added', startLineNumber: 2, endLineNumber: 2 },
    ])
  })

  it('marks changed lines as modified', () => {
    expect(getGitLineChanges('one\ntwo\nthree', 'one\nTWO\nthree')).toEqual([
      { kind: 'modified', startLineNumber: 2, endLineNumber: 2 },
    ])
  })

  it('marks deleted lines at the adjacent current line', () => {
    expect(getGitLineChanges('one\ntwo\nthree', 'one\nthree')).toEqual([
      { kind: 'deleted', startLineNumber: 2, endLineNumber: 2 },
    ])
  })

  it('separates modified lines from extra added lines in a replacement block', () => {
    expect(getGitLineChanges('one\nold\nthree', 'one\nnew\nextra\nthree')).toEqual([
      { kind: 'modified', startLineNumber: 2, endLineNumber: 2 },
      { kind: 'added', startLineNumber: 3, endLineNumber: 3 },
    ])
  })
})
