import type { TerminalAgentLaunch } from '@/api/schemas'

export type TerminalTabPreference = {
  name?: string
  color?: string | null
}

export type TerminalTabPreferencesMap = Record<string, TerminalTabPreference>

export const TERMINAL_TAB_PREFERENCES_STORAGE_PREFIX = 'prompt-tasks:terminals:tabs:'

export const TERMINAL_TAB_NAME_MAX_LENGTH = 32

export const TERMINAL_TAB_COLORS = [
  { id: 'default', label: 'Padrao', value: null },
  { id: 'crimson', label: 'Vermelho', value: '#e74856' },
  { id: 'orange', label: 'Laranja', value: '#ff8c00' },
  { id: 'gold', label: 'Amarelo', value: '#c19c00' },
  { id: 'green', label: 'Verde', value: '#16c60c' },
  { id: 'teal', label: 'Azul claro', value: '#3a96dd' },
  { id: 'blue', label: 'Azul', value: '#0078d4' },
  { id: 'purple', label: 'Roxo', value: '#8761b9' },
  { id: 'pink', label: 'Rosa', value: '#ff99a4' },
  { id: 'grey', label: 'Cinza', value: '#767676' },
] as const

const AGENT_TAB_DEFAULTS: Record<TerminalAgentLaunch, TerminalTabPreference> = {
  Claude: { name: 'Claude', color: '#8761b9' },
  Codex: { name: 'Codex', color: '#16c60c' },
  Grok: { name: 'Grok', color: '#ff8c00' },
}

export function terminalTabPreferencesStorageKey(promptId: string) {
  return `${TERMINAL_TAB_PREFERENCES_STORAGE_PREFIX}${promptId}`
}

export function readTerminalTabPreferences(promptId: string): TerminalTabPreferencesMap {
  if (typeof window === 'undefined') {
    return {}
  }

  try {
    const raw = window.localStorage.getItem(terminalTabPreferencesStorageKey(promptId))
    if (!raw) {
      return {}
    }

    const parsed = JSON.parse(raw) as TerminalTabPreferencesMap
    return parsed && typeof parsed === 'object' ? parsed : {}
  } catch {
    return {}
  }
}

export function writeTerminalTabPreferences(promptId: string, preferences: TerminalTabPreferencesMap) {
  window.localStorage.setItem(terminalTabPreferencesStorageKey(promptId), JSON.stringify(preferences))
}

export function sanitizeTerminalTabName(value: string) {
  return value.trim().slice(0, TERMINAL_TAB_NAME_MAX_LENGTH)
}

export function resolveTerminalTabLabel(preference: TerminalTabPreference | undefined, fallbackIndex: number) {
  const name = preference?.name?.trim()
  return name && name.length > 0 ? name : `Terminal ${fallbackIndex + 1}`
}

export function defaultPreferenceForAgent(agent: TerminalAgentLaunch): TerminalTabPreference {
  return { ...AGENT_TAB_DEFAULTS[agent] }
}

export function pruneTerminalTabPreferences(
  preferences: TerminalTabPreferencesMap,
  activeSessionIds: string[],
): TerminalTabPreferencesMap {
  const active = new Set(activeSessionIds)
  const next: TerminalTabPreferencesMap = {}

  for (const sessionId of active) {
    if (preferences[sessionId]) {
      next[sessionId] = preferences[sessionId]
    }
  }

  return next
}