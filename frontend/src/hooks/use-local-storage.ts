import { useCallback, useState } from 'react'

/**
 * Persist a string value in localStorage. Mirrors the try/catch pattern used by
 * the theme provider so it degrades gracefully when storage is unavailable
 * (private mode / SSR).
 */
export function useLocalStorage(
  key: string,
  initialValue: string,
): [string, (value: string) => void] {
  const [value, setValue] = useState<string>(() => {
    try {
      const stored = localStorage.getItem(key)
      if (stored !== null) {
        return stored
      }
    } catch {
      // localStorage may be unavailable (private mode / SSR)
    }
    return initialValue
  })

  const setStoredValue = useCallback(
    (next: string) => {
      setValue(next)
      try {
        localStorage.setItem(key, next)
      } catch {
        // ignore persistence failures
      }
    },
    [key],
  )

  return [value, setStoredValue]
}
