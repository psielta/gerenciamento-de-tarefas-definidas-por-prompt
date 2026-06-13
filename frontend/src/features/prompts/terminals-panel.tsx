import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Terminal as TerminalIcon, X, ZoomIn, ZoomOut } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { closeTerminal, createTerminal, listTerminals } from '@/api/terminals'
import { queryKeys } from '@/api/query-keys'
import { Button } from '@/components/ui/button'
import { getErrorMessage } from '@/api/client'
import { useLocalStorage } from '@/hooks/use-local-storage'
import { cn } from '@/lib/utils'
import { toast } from 'sonner'
import {
  TERMINAL_FONT_SIZE_DEFAULT,
  TERMINAL_FONT_SIZE_STORAGE_KEY,
  clampTerminalFontSize,
} from './terminal-font-size'
import { TerminalView } from './terminal-view'

type TerminalsPanelProps = {
  promptId: string
}

export function TerminalsPanel({ promptId }: TerminalsPanelProps) {
  const queryClient = useQueryClient()
  const [activeSessionId, setActiveSessionId] = useState<string | null>(null)
  const [storedFontSize, setStoredFontSize] = useLocalStorage(
    TERMINAL_FONT_SIZE_STORAGE_KEY,
    String(TERMINAL_FONT_SIZE_DEFAULT),
  )
  const fontSize = clampTerminalFontSize(Number.parseInt(storedFontSize, 10))

  const adjustFontSize = useCallback(
    (delta: number) => {
      setStoredFontSize(String(clampTerminalFontSize(fontSize + delta)))
    },
    [fontSize, setStoredFontSize],
  )

  const terminalsQuery = useQuery({
    queryKey: queryKeys.terminals.forPrompt(promptId),
    queryFn: () => listTerminals(promptId),
  })

  const sessions = useMemo(() => terminalsQuery.data ?? [], [terminalsQuery.data])

  const resolvedActiveId = useMemo(() => {
    if (activeSessionId && sessions.some((session) => session.id === activeSessionId)) {
      return activeSessionId
    }
    return sessions[0]?.id ?? null
  }, [activeSessionId, sessions])

  const activeSession = useMemo(
    () => sessions.find((session) => session.id === resolvedActiveId) ?? null,
    [resolvedActiveId, sessions],
  )

  const createMutation = useMutation({
    mutationFn: () => createTerminal(promptId),
    onSuccess: (session) => {
      queryClient.setQueryData(queryKeys.terminals.forPrompt(promptId), (current: typeof sessions | undefined) => [
        ...(current ?? []),
        session,
      ])
      setActiveSessionId(session.id)
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const removeSession = useCallback(
    (sessionId: string) => {
      queryClient.setQueryData(queryKeys.terminals.forPrompt(promptId), (current: typeof sessions | undefined) =>
        (current ?? []).filter((session) => session.id !== sessionId),
      )
      if (resolvedActiveId === sessionId) {
        setActiveSessionId(null)
      }
    },
    [promptId, queryClient, resolvedActiveId],
  )

  const closeMutation = useMutation({
    mutationFn: (sessionId: string) => closeTerminal(sessionId),
    onSuccess: (_, sessionId) => {
      queryClient.setQueryData(queryKeys.terminals.forPrompt(promptId), (current: typeof sessions | undefined) =>
        (current ?? []).filter((session) => session.id !== sessionId),
      )
      if (resolvedActiveId === sessionId) {
        setActiveSessionId(null)
      }
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <Button type="button" size="sm" onClick={() => createMutation.mutate()} disabled={createMutation.isPending}>
          <Plus className="h-4 w-4" />
          Novo terminal
        </Button>
        {activeSession ? (
          <span
            className="min-w-0 max-w-full truncate font-mono text-xs text-muted-foreground"
            title={activeSession.cwd}
          >
            {activeSession.cwd}
          </span>
        ) : null}
        {sessions.length === 0 ? (
          <span className="text-sm text-muted-foreground">Nenhum terminal aberto. O diretório inicial é o workspace do prompt.</span>
        ) : (
          sessions.map((session, index) => {
            const isActive = session.id === resolvedActiveId
            return (
              <div key={session.id} className="flex items-center gap-1">
                <Button
                  type="button"
                  size="sm"
                  variant={isActive ? 'default' : 'secondary'}
                  onClick={() => setActiveSessionId(session.id)}
                >
                  <TerminalIcon className="h-4 w-4" />
                  Terminal {index + 1}
                </Button>
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  aria-label={`Fechar terminal ${index + 1}`}
                  onClick={() => closeMutation.mutate(session.id)}
                  disabled={closeMutation.isPending}
                >
                  <X className="h-4 w-4" />
                </Button>
              </div>
            )
          })
        )}
      </div>

      {sessions.length > 0 ? (
        <div className="relative h-[min(70vh,640px)] w-full overflow-hidden rounded-md border border-border bg-[#0f1117]">
          <div
            role="group"
            aria-label="Zoom do terminal"
            className="pointer-events-auto absolute right-2 top-2 z-10 flex items-center gap-0.5 rounded-md border border-border bg-card/95 p-0.5 shadow-sm backdrop-blur-sm"
          >
            <Button
              type="button"
              size="icon"
              variant="ghost"
              className="h-7 w-7"
              title="Diminuir fonte"
              aria-label="Diminuir fonte do terminal"
              onClick={() => adjustFontSize(-1)}
            >
              <ZoomOut className="h-3.5 w-3.5" />
            </Button>
            <button
              type="button"
              title="Restaurar tamanho padrao da fonte"
              aria-label="Restaurar tamanho padrao da fonte do terminal"
              className={cn(
                'rounded px-1.5 py-0.5 font-mono text-[0.65rem] tabular-nums text-muted-foreground transition-colors',
                'hover:bg-secondary hover:text-foreground',
              )}
              onClick={() => setStoredFontSize(String(TERMINAL_FONT_SIZE_DEFAULT))}
            >
              {fontSize}px
            </button>
            <Button
              type="button"
              size="icon"
              variant="ghost"
              className="h-7 w-7"
              title="Aumentar fonte (Ctrl+scroll no terminal tambem aplica zoom)"
              aria-label="Aumentar fonte do terminal"
              onClick={() => adjustFontSize(1)}
            >
              <ZoomIn className="h-3.5 w-3.5" />
            </Button>
          </div>
          <div className="absolute inset-0 pt-10">
            {sessions.map((session) => (
              <TerminalView
                key={session.id}
                sessionId={session.id}
                active={session.id === resolvedActiveId}
                fontSize={fontSize}
                onZoom={adjustFontSize}
                onSessionExit={removeSession}
              />
            ))}
          </div>
        </div>
      ) : null}
    </div>
  )
}