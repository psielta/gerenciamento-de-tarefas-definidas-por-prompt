import { useCallback, useMemo, useState } from 'react'
import {
  pruneTerminalTabPreferences,
  readTerminalTabPreferences,
  type TerminalTabPreference,
  type TerminalTabPreferencesMap,
  writeTerminalTabPreferences,
} from './terminal-tab-preferences'

function loadPreferences(promptId: string, sessionIds: string[]) {
  return pruneTerminalTabPreferences(readTerminalTabPreferences(promptId), sessionIds)
}

export function useTerminalTabPreferences(promptId: string, sessionIds: string[]) {
  const [storedPromptId, setStoredPromptId] = useState(promptId)
  const [preferences, setPreferences] = useState<TerminalTabPreferencesMap>(() =>
    loadPreferences(promptId, sessionIds),
  )

  if (storedPromptId !== promptId) {
    setStoredPromptId(promptId)
    setPreferences(loadPreferences(promptId, sessionIds))
  }

  const visiblePreferences = useMemo(
    () => pruneTerminalTabPreferences(preferences, sessionIds),
    [preferences, sessionIds],
  )

  const setSessionPreference = useCallback(
    (sessionId: string, patch: TerminalTabPreference) => {
      setPreferences((current) => {
        const merged = {
          ...current,
          [sessionId]: {
            ...current[sessionId],
            ...patch,
          },
        }
        const next = pruneTerminalTabPreferences(
          merged,
          sessionIds.includes(sessionId) ? sessionIds : [...sessionIds, sessionId],
        )
        writeTerminalTabPreferences(promptId, next)
        return next
      })
    },
    [promptId, sessionIds],
  )

  const removeSessionPreference = useCallback(
    (sessionId: string) => {
      setPreferences((current) => {
        if (!current[sessionId]) {
          return current
        }

        const next = { ...current }
        delete next[sessionId]
        writeTerminalTabPreferences(promptId, next)
        return next
      })
    },
    [promptId],
  )

  return {
    preferences: visiblePreferences,
    setSessionPreference,
    removeSessionPreference,
  }
}