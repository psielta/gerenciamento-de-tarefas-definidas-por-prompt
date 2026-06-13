import { describe, expect, it } from 'vitest'
import {
  TERMINAL_FONT_SIZE_DEFAULT,
  TERMINAL_FONT_SIZE_MAX,
  TERMINAL_FONT_SIZE_MIN,
  clampTerminalFontSize,
} from './terminal-font-size'

describe('clampTerminalFontSize', () => {
  it('returns default for invalid values', () => {
    expect(clampTerminalFontSize(Number.NaN)).toBe(TERMINAL_FONT_SIZE_DEFAULT)
  })

  it('clamps to configured bounds', () => {
    expect(clampTerminalFontSize(TERMINAL_FONT_SIZE_MIN - 1)).toBe(TERMINAL_FONT_SIZE_MIN)
    expect(clampTerminalFontSize(TERMINAL_FONT_SIZE_MAX + 1)).toBe(TERMINAL_FONT_SIZE_MAX)
    expect(clampTerminalFontSize(16)).toBe(16)
  })
})