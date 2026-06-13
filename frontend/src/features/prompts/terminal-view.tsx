import { FitAddon } from '@xterm/addon-fit'
import { Terminal } from '@xterm/xterm'
import '@xterm/xterm/css/xterm.css'
import './terminal-view.css'
import { useEffect, useRef } from 'react'
import { base64ToBytes, bytesToBase64 } from '@/lib/base64'
import { cn } from '@/lib/utils'
import { usePromptHub } from '@/realtime/prompt-hub'

const TERMINAL_FONT_FAMILY =
  '"Cascadia Code", "Cascadia Mono", Consolas, "JetBrains Mono", ui-monospace, monospace'

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
  options?: { notifyBackend?: boolean; onSized?: (cols: number, rows: number) => void },
) {
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      try {
        fitAddon.fit()
      } catch {
        return
      }

      if (options?.notifyBackend) {
        options.onSized?.(term.cols, term.rows)
      }

      term.refresh(0, term.rows - 1)
    })
  })
}

export function TerminalView({ sessionId, active, fontSize, onZoom, onSessionExit }: TerminalViewProps) {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const terminalRef = useRef<{ term: Terminal; fitAddon: FitAddon } | null>(null)
  const onZoomRef = useRef(onZoom)
  const activeRef = useRef(active)
  const {
    joinTerminal,
    leaveTerminal,
    sendTerminalInput,
    resizeTerminal,
    subscribeTerminalOutput,
    subscribeTerminalExit,
  } = usePromptHub()

  useEffect(() => {
    onZoomRef.current = onZoom
    activeRef.current = active
  }, [active, onZoom])

  useEffect(() => {
    const container = containerRef.current
    if (!container) {
      return
    }

    const term = new Terminal({
      cursorBlink: true,
      convertEol: false,
      scrollback: 10_000,
      smoothScrollDuration: 0,
      fastScrollModifier: 'alt',
      fontFamily: TERMINAL_FONT_FAMILY,
      fontSize,
      lineHeight: 1,
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

    const notifyBackendResize = (cols: number, rows: number) => {
      if (!activeRef.current) {
        return
      }

      resizeTerminal(sessionId, cols, rows)
    }

    if (activeRef.current) {
      scheduleTerminalFit(fitAddon, term, { notifyBackend: true, onSized: notifyBackendResize })
    }

    joinTerminal(sessionId)

    const unsubscribeOutput = subscribeTerminalOutput(sessionId, (dataBase64) => {
      term.write(base64ToBytes(dataBase64))
    })

    const unsubscribeExit = subscribeTerminalExit(sessionId, (exitCode) => {
      term.writeln(`\r\n[Process exited with code ${exitCode}]`)
      onSessionExit?.(sessionId, exitCode)
    })

    const dataDisposable = term.onData((data) => {
      const bytes = new TextEncoder().encode(data)
      sendTerminalInput(sessionId, bytesToBase64(bytes))
    })

    const onWheel = (event: WheelEvent) => {
      if (!(event.ctrlKey || event.metaKey)) {
        return
      }

      if (!activeRef.current || !onZoomRef.current) {
        return
      }

      event.preventDefault()
      event.stopPropagation()
      onZoomRef.current(event.deltaY < 0 ? 1 : -1)
    }
    container.addEventListener('wheel', onWheel, { passive: false })

    return () => {
      container.removeEventListener('wheel', onWheel)
      dataDisposable.dispose()
      unsubscribeOutput()
      unsubscribeExit()
      leaveTerminal(sessionId)
      term.dispose()
      terminalRef.current = null
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps -- fontSize handled in dedicated effect
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
    scheduleTerminalFit(terminal.fitAddon, terminal.term, {
      notifyBackend: active,
      onSized: (cols, rows) => resizeTerminal(sessionId, cols, rows),
    })
  }, [active, fontSize, resizeTerminal, sessionId])

  useEffect(() => {
    const container = containerRef.current
    const terminal = terminalRef.current
    if (!container || !terminal) {
      return
    }

    if (!active) {
      return
    }

    const fit = () => {
      scheduleTerminalFit(terminal.fitAddon, terminal.term, {
        notifyBackend: true,
        onSized: (cols, rows) => resizeTerminal(sessionId, cols, rows),
      })
      terminal.term.focus()
    }

    const resizeObserver = new ResizeObserver(() => fit())
    resizeObserver.observe(container)
    fit()

    return () => resizeObserver.disconnect()
  }, [active, resizeTerminal, sessionId])

  return (
    <div
      ref={containerRef}
      className={cn(
        'thoth-terminal absolute inset-0 overflow-hidden',
        active ? 'z-10' : 'z-0 pointer-events-none invisible',
      )}
      aria-hidden={!active}
    />
  )
}