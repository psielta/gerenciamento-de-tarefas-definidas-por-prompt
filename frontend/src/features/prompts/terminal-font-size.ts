export const TERMINAL_FONT_SIZE_STORAGE_KEY = 'prompt-tasks:terminals:font-size'
export const TERMINAL_FONT_SIZE_DEFAULT = 14
export const TERMINAL_FONT_SIZE_MIN = 10
export const TERMINAL_FONT_SIZE_MAX = 24

export function clampTerminalFontSize(size: number) {
  if (Number.isNaN(size)) {
    return TERMINAL_FONT_SIZE_DEFAULT
  }

  return Math.min(Math.max(size, TERMINAL_FONT_SIZE_MIN), TERMINAL_FONT_SIZE_MAX)
}