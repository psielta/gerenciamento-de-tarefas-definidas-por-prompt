import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useTerminalSwitcher } from './use-terminal-switcher'

describe('useTerminalSwitcher', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('opens switcher on Ctrl+PageDown and selects on Control release', () => {
    const onSelectSession = vi.fn()
    const { result } = renderHook(() =>
      useTerminalSwitcher({
        enabled: true,
        sessionIds: ['a', 'b', 'c'],
        activeSessionId: 'a',
        onSelectSession,
      }),
    )

    act(() => {
      document.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'PageDown', ctrlKey: true, bubbles: true }),
      )
    })

    expect(result.current.switcherOpen).toBe(true)
    expect(result.current.highlightedSessionId).toBe('b')

    act(() => {
      document.dispatchEvent(new KeyboardEvent('keyup', { key: 'Control', bubbles: true }))
    })

    expect(onSelectSession).toHaveBeenCalledWith('b')
    expect(result.current.switcherOpen).toBe(false)
  })

  it('cycles with Tab while switcher is open', () => {
    const onSelectSession = vi.fn()
    const { result } = renderHook(() =>
      useTerminalSwitcher({
        enabled: true,
        sessionIds: ['a', 'b', 'c'],
        activeSessionId: 'a',
        onSelectSession,
      }),
    )

    act(() => {
      document.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'PageDown', ctrlKey: true, bubbles: true }),
      )
    })

    act(() => {
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }))
    })

    expect(result.current.highlightedSessionId).toBe('c')

    act(() => {
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
    })

    expect(onSelectSession).toHaveBeenCalledWith('c')
    expect(result.current.switcherOpen).toBe(false)
  })
})