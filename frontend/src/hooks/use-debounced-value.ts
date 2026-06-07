import { useEffect, useState } from 'react'

/** Returns a copy of `value` that only updates after `delayMs` of no changes. */
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebounced(value), delayMs)
    return () => window.clearTimeout(timeout)
  }, [value, delayMs])

  return debounced
}
