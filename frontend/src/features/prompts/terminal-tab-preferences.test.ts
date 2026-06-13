import { describe, expect, it } from 'vitest'
import {
  defaultPreferenceForAgent,
  pruneTerminalTabPreferences,
  resolveTerminalTabLabel,
  sanitizeTerminalTabName,
} from './terminal-tab-preferences'

describe('terminal-tab-preferences', () => {
  it('resolves custom name or fallback index label', () => {
    expect(resolveTerminalTabLabel({ name: ' Claude ' }, 2)).toBe('Claude')
    expect(resolveTerminalTabLabel(undefined, 0)).toBe('Terminal 1')
  })

  it('sanitizes terminal tab names', () => {
    expect(sanitizeTerminalTabName('  meu terminal  ')).toBe('meu terminal')
    expect(sanitizeTerminalTabName('x'.repeat(40)).length).toBe(32)
  })

  it('returns defaults for agent launches', () => {
    expect(defaultPreferenceForAgent('Codex')).toEqual({ name: 'Codex', color: '#16c60c' })
  })

  it('prunes preferences for closed sessions', () => {
    const pruned = pruneTerminalTabPreferences(
      {
        '11111111-1111-4111-8111-111111111111': { name: 'A' },
        '22222222-2222-4222-8222-222222222222': { name: 'B' },
      },
      ['11111111-1111-4111-8111-111111111111'],
    )

    expect(pruned).toEqual({
      '11111111-1111-4111-8111-111111111111': { name: 'A' },
    })
  })
})