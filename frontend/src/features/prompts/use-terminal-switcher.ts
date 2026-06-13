import { useCallback, useEffect, useRef, useState } from 'react'

type UseTerminalSwitcherOptions = {
  enabled: boolean
  sessionIds: string[]
  activeSessionId: string | null
  onSelectSession: (sessionId: string) => void
}

function moveHighlight(sessionIds: string[], currentId: string | null, direction: 1 | -1) {
  if (sessionIds.length === 0) {
    return null
  }

  const currentIndex = currentId ? sessionIds.indexOf(currentId) : -1
  const nextIndex =
    currentIndex === -1
      ? direction === 1
        ? 0
        : sessionIds.length - 1
      : (currentIndex + direction + sessionIds.length) % sessionIds.length

  return sessionIds[nextIndex] ?? null
}

function directionFromCycleKey(key: string, shiftKey: boolean): 1 | -1 | null {
  if (key === 'PageUp' || key === 'ArrowLeft' || key === 'ArrowUp') {
    return -1
  }

  if (key === 'PageDown' || key === 'ArrowRight' || key === 'ArrowDown') {
    return 1
  }

  if (key === 'Tab') {
    return shiftKey ? -1 : 1
  }

  return null
}

function isCtrlCycleKey(key: string) {
  return key === 'PageDown' || key === 'PageUp' || key === 'ArrowRight' || key === 'ArrowLeft'
}

export function useTerminalSwitcher({
  enabled,
  sessionIds,
  activeSessionId,
  onSelectSession,
}: UseTerminalSwitcherOptions) {
  const [open, setOpen] = useState(false)
  const [highlightedSessionId, setHighlightedSessionId] = useState<string | null>(null)
  const ctrlPressedRef = useRef(false)
  const pendingSelectionRef = useRef<string | null>(null)

  const highlightNext = useCallback(
    (direction: 1 | -1) => {
      const baseId = open ? highlightedSessionId ?? activeSessionId : activeSessionId
      const nextId = moveHighlight(sessionIds, baseId, direction)

      if (!nextId) {
        return
      }

      pendingSelectionRef.current = nextId
      setHighlightedSessionId(nextId)
      setOpen(true)
    },
    [activeSessionId, highlightedSessionId, open, sessionIds],
  )

  const closeSwitcher = useCallback((applySelection: boolean) => {
    if (applySelection && pendingSelectionRef.current) {
      onSelectSession(pendingSelectionRef.current)
    }

    pendingSelectionRef.current = null
    setOpen(false)
    setHighlightedSessionId(null)
  }, [onSelectSession])

  useEffect(() => {
    if (!enabled || sessionIds.length < 2) {
      return
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Control') {
        ctrlPressedRef.current = true
        return
      }

      if (open) {
        if (event.key === 'Enter') {
          event.preventDefault()
          event.stopPropagation()
          closeSwitcher(true)
          return
        }

        if (event.key === 'Escape') {
          event.preventDefault()
          event.stopPropagation()
          closeSwitcher(false)
          return
        }

        const overlayDirection = directionFromCycleKey(event.key, event.shiftKey)
        if (overlayDirection !== null && !event.ctrlKey && !event.altKey && !event.metaKey) {
          event.preventDefault()
          event.stopPropagation()
          highlightNext(overlayDirection)
          return
        }
      }

      if (!event.ctrlKey || event.altKey || event.metaKey || !isCtrlCycleKey(event.key)) {
        return
      }

      event.preventDefault()
      event.stopPropagation()

      const direction = directionFromCycleKey(event.key, event.shiftKey) ?? 1
      highlightNext(direction)
    }

    const onKeyUp = (event: KeyboardEvent) => {
      if (event.key !== 'Control') {
        return
      }

      ctrlPressedRef.current = false
      if (open) {
        closeSwitcher(true)
      }
    }

    const onBlur = () => {
      if (open && !ctrlPressedRef.current) {
        closeSwitcher(true)
      }
    }

    document.addEventListener('keydown', onKeyDown, true)
    document.addEventListener('keyup', onKeyUp, true)
    window.addEventListener('blur', onBlur)

    return () => {
      document.removeEventListener('keydown', onKeyDown, true)
      document.removeEventListener('keyup', onKeyUp, true)
      window.removeEventListener('blur', onBlur)
    }
  }, [closeSwitcher, enabled, highlightNext, open, sessionIds.length])

  return {
    switcherOpen: open,
    highlightedSessionId: highlightedSessionId ?? activeSessionId ?? sessionIds[0] ?? null,
  }
}