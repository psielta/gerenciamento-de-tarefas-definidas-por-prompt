import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, ZoomIn, ZoomOut } from 'lucide-react'
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
import type { TerminalAgentLaunch } from '@/api/schemas'
import { TerminalAgentMenu } from './terminal-agent-menu'
import { defaultPreferenceForAgent } from './terminal-tab-preferences'
import { TerminalSwitcher } from './terminal-switcher'
import { TerminalTabButton } from './terminal-tab-button'
import { TerminalView } from './terminal-view'
import { useTerminalSwitcher } from './use-terminal-switcher'
import { useTerminalTabPreferences } from './use-terminal-tab-preferences'

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
  const sessionIds = useMemo(() => sessions.map((session) => session.id), [sessions])
  const { preferences, setSessionPreference, removeSessionPreference } = useTerminalTabPreferences(
    promptId,
    sessionIds,
  )

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

  const { switcherOpen, highlightedSessionId } = useTerminalSwitcher({
    enabled: sessions.length > 0,
    sessionIds,
    activeSessionId: resolvedActiveId,
    onSelectSession: setActiveSessionId,
  })

  const switcherItems = useMemo(
    () =>
      sessions.map((session, index) => ({
        sessionId: session.id,
        index,
        preference: preferences[session.id],
      })),
    [preferences, sessions],
  )

  const handleCreateSuccess = useCallback(
    (session: (typeof sessions)[number]) => {
      queryClient.setQueryData(queryKeys.terminals.forPrompt(promptId), (current: typeof sessions | undefined) => [
        ...(current ?? []),
        session,
      ])
      setActiveSessionId(session.id)
    },
    [promptId, queryClient],
  )

  const createMutation = useMutation({
    mutationFn: (agentLaunch?: TerminalAgentLaunch) => createTerminal(promptId, { agentLaunch }),
    onSuccess: (session, agentLaunch) => {
      handleCreateSuccess(session)
      if (agentLaunch) {
        setSessionPreference(session.id, defaultPreferenceForAgent(agentLaunch))
      }
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const removeSession = useCallback(
    (sessionId: string) => {
      queryClient.setQueryData(queryKeys.terminals.forPrompt(promptId), (current: typeof sessions | undefined) =>
        (current ?? []).filter((session) => session.id !== sessionId),
      )
      removeSessionPreference(sessionId)
      if (resolvedActiveId === sessionId) {
        setActiveSessionId(null)
      }
    },
    [promptId, queryClient, removeSessionPreference, resolvedActiveId],
  )

  const closeMutation = useMutation({
    mutationFn: (sessionId: string) => closeTerminal(sessionId),
    onSuccess: (_, sessionId) => {
      removeSession(sessionId)
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <div className="inline-flex items-stretch">
          <Button
            type="button"
            size="sm"
            className="rounded-r-none"
            onClick={() => createMutation.mutate(undefined)}
            disabled={createMutation.isPending}
          >
            <Plus className="h-4 w-4" />
            Novo terminal
          </Button>
          <TerminalAgentMenu
            disabled={createMutation.isPending}
            onSelectAgent={(agent) => createMutation.mutate(agent)}
          />
        </div>
        {activeSession ? (
          <span
            className="min-w-0 max-w-full truncate font-mono text-xs text-muted-foreground"
            title={activeSession.cwd}
          >
            {activeSession.cwd}
          </span>
        ) : null}
        {sessions.length === 0 ? (
          <span className="text-sm text-muted-foreground">
            Nenhum terminal aberto. O diretório inicial é o workspace do prompt.
          </span>
        ) : (
          sessions.map((session, index) => {
            const isActive = session.id === resolvedActiveId
            return (
              <TerminalTabButton
                key={session.id}
                index={index}
                isActive={isActive}
                preference={preferences[session.id]}
                closeDisabled={closeMutation.isPending}
                onActivate={() => setActiveSessionId(session.id)}
                onClose={() => closeMutation.mutate(session.id)}
                onPreferenceChange={(patch) => setSessionPreference(session.id, patch)}
              />
            )
          })
        )}
        {sessions.length > 0 ? (
          <div
            role="group"
            aria-label="Zoom do terminal"
            className="ml-auto flex items-center gap-0.5 rounded-md border border-border bg-card p-0.5"
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
        ) : null}
      </div>

      {sessions.length > 0 ? (
        <div className="relative h-[min(70vh,640px)] w-full overflow-hidden rounded-md border border-border bg-[#0f1117]">
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
      ) : null}

      {switcherOpen && highlightedSessionId ? (
        <TerminalSwitcher items={switcherItems} highlightedSessionId={highlightedSessionId} />
      ) : null}
    </div>
  )
}