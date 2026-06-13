import { FitAddon } from '@xterm/addon-fit'
import { Terminal } from '@xterm/xterm'
import '@xterm/xterm/css/xterm.css'
import './terminal-view.css'
import { useEffect, useRef } from 'react'
import { base64ToBytes, bytesToBase64 } from '@/lib/base64'
import { cn } from '@/lib/utils'
import { usePromptHub } from '@/realtime/prompt-hub'

const TERMINAL_FONT_FAMILY =
  '"JetBrains Mono Variable", ui-monospace, "Cascadia Code", "SF Mono", Consolas, monospace'

type TerminalViewProps = {
  sessionId: string
  active: boolean
  fontSize: number
  onZoom?: (delta: number) => void
  onSessionExit?: (sessionId: string, exitCode: number) => void
}

function scheduleTerminalFit(
  fitAddon: FitAddon,
  term: Terminal,
  onSized?: (cols: number, rows: number) => void,
) {
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      try {
        fitAddon.fit()
        onSized?.(term.cols, term.rows)
      } catch {
        // FitAddon throws when the container has no measurable size yet.
      }
    })
  })
}

export function TerminalView({ sessionId, active, fontSize, onZoom, onSessionExit }: TerminalViewProps) {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const terminalRef = useRef<{ term: Terminal; fitAddon: FitAddon } | null>(null)
  const onZoomRef = useRef(onZoom)
  const activeRef = useRef(active)
  onZoomRef.current = onZoom
  activeRef.current = active
  const {
    joinTerminal,
    leaveTerminal,
    sendTerminalInput,
    resizeTerminal,
    subscribeTerminalOutput,
    subscribeTerminalExit,
  } = usePromptHub()

  useEffect(() => {
    const container = containerRef.current
    if (!container) {
      return
    }

    const term = new Terminal({
      cursorBlink: true,
      convertEol: true,
      scrollback: 10_000,
      smoothScrollDuration: 0,
      fontFamily: TERMINAL_FONT_FAMILY,
      fontSize,
      lineHeight: 1.3,
      theme: {
        background: '#0f1117',
        foreground: '#e6edf3',
        cursor: '#e6edf3',
        selectionBackground: '#264f78',
      },
    })
    const fitAddon = new FitAddon()
    term.loadAddon(fitAddon)
    term.open(container)
    terminalRef.current = { term, fitAddon }

    scheduleTerminalFit(fitAddon, term, (cols, rows) => {
      resizeTerminal(sessionId, cols, rows)
    })

    joinTerminal(sessionId)

    const unsubscribeOutput = subscribeTerminalOutput(sessionId, (dataBase64) => {
      const bytes = base64ToBytes(dataBase64)
      term.write(bytes)
    })

    const unsubscribeExit = subscribeTerminalExit(sessionId, (exitCode) => {
      term.writeln(`\r\n[Process exited with code ${exitCode}]`)
      onSessionExit?.(sessionId, exitCode)
    })

    const dataDisposable = term.onData((data) => {
      const bytes = new TextEncoder().encode(data)
      sendTerminalInput(sessionId, bytesToBase64(bytes))
    })

    const resizeDisposable = term.onResize(({ cols, rows }) => {
      resizeTerminal(sessionId, cols, rows)
    })

    const onWheel = (event: WheelEvent) => {
      event.stopPropagation()
      if ((event.ctrlKey || event.metaKey) && activeRef.current && onZoomRef.current) {
        event.preventDefault()
        onZoomRef.current(event.deltaY < 0 ? 1 : -1)
      }
    }
    container.addEventListener('wheel', onWheel, { passive: false })

    return () => {
      container.removeEventListener('wheel', onWheel)
      dataDisposable.dispose()
      resizeDisposable.dispose()
      unsubscribeOutput()
      unsubscribeExit()
      leaveTerminal(sessionId)
      term.dispose()
      terminalRef.current = null
    }
  }, [
    joinTerminal,
    leaveTerminal,
    onSessionExit,
    resizeTerminal,
    sendTerminalInput,
    sessionId,
    subscribeTerminalExit,
    subscribeTerminalOutput,
  ])

  useEffect(() => {
    const terminal = terminalRef.current
    if (!terminal) {
      return
    }

    terminal.term.options.fontSize = fontSize
    scheduleTerminalFit(terminal.fitAddon, terminal.term, (cols, rows) => {
      resizeTerminal(sessionId, cols, rows)
    })
  }, [fontSize, resizeTerminal, sessionId])

  useEffect(() => {
    const container = containerRef.current
    const terminal = terminalRef.current
    if (!container || !terminal) {
      return
    }

    const fit = () => {
      if (!active) {
        return
      }

      scheduleTerminalFit(terminal.fitAddon, terminal.term, (cols, rows) => {
        resizeTerminal(sessionId, cols, rows)
      })
      terminal.term.focus()
    }

    const resizeObserver = new ResizeObserver(() => fit())
    resizeObserver.observe(container)

    if (active) {
      fit()
    }

    return () => resizeObserver.disconnect()
  }, [active, resizeTerminal, sessionId])

  return (
    <div
      ref={containerRef}
      className={cn(
        'thoth-terminal absolute inset-0 overflow-hidden',
        !active && 'pointer-events-none invisible',
      )}
      aria-hidden={!active}
    />
  )
}