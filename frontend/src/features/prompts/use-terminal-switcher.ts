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

      if (!event.ctrlKey || event.key !== 'Tab' || event.altKey) {
        return
      }

      event.preventDefault()
      event.stopPropagation()

      const direction: 1 | -1 = event.shiftKey ? -1 : 1
      const baseId = open ? highlightedSessionId ?? activeSessionId : activeSessionId
      const nextId = moveHighlight(sessionIds, baseId, direction)

      if (!nextId) {
        return
      }

      pendingSelectionRef.current = nextId
      setHighlightedSessionId(nextId)
      setOpen(true)
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
  }, [
    activeSessionId,
    closeSwitcher,
    enabled,
    highlightedSessionId,
    open,
    sessionIds,
  ])

  return {
    switcherOpen: open,
    highlightedSessionId: highlightedSessionId ?? activeSessionId ?? sessionIds[0] ?? null,
  }
}