import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, ZoomIn, ZoomOut } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { queryKeys } from '@/api/query-keys'
import type { GenericTerminalSession, TerminalAgentLaunch } from '@/api/schemas'
import { closeTerminal, createGenericTerminal, listGenericTerminals } from '@/api/terminals'
import { Button } from '@/components/ui/button'
import { TerminalAgentMenu } from '@/features/prompts/terminal-agent-menu'
import {
  TERMINAL_FONT_SIZE_DEFAULT,
  TERMINAL_FONT_SIZE_STORAGE_KEY,
  clampTerminalFontSize,
} from '@/features/prompts/terminal-font-size'
import { TerminalSwitcher } from '@/features/prompts/terminal-switcher'
import { TerminalTabButton } from '@/features/prompts/terminal-tab-button'
import { defaultPreferenceForAgent } from '@/features/prompts/terminal-tab-preferences'
import { TerminalView } from '@/features/prompts/terminal-view'
import { useTerminalSwitcher } from '@/features/prompts/use-terminal-switcher'
import { useTerminalTabPreferences } from '@/features/prompts/use-terminal-tab-preferences'
import { useLocalStorage } from '@/hooks/use-local-storage'
import { cn } from '@/lib/utils'

// Tab preferences are namespaced by scope; generic terminals share one bucket
// since they are not tied to any prompt.
const GENERIC_TERMINAL_PREFS_SCOPE = 'generic'

/**
 * Tab-based terminal surface for generic (promptless) terminals. Mirrors the
 * prompt TerminalsPanel but talks to the generic endpoints; the shell opens in
 * a default directory and the user navigates with `cd`.
 */
export function GenericTerminalsPanel() {
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
    queryKey: queryKeys.terminals.generic(),
    queryFn: listGenericTerminals,
  })

  const sessions = useMemo(() => terminalsQuery.data ?? [], [terminalsQuery.data])
  const sessionIds = useMemo(() => sessions.map((session) => session.id), [sessions])
  const { preferences, setSessionPreference, removeSessionPreference } = useTerminalTabPreferences(
    GENERIC_TERMINAL_PREFS_SCOPE,
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

  const { switcherOpen, highlightedSessionId, handleKeyboardEvent } = useTerminalSwitcher({
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
    (session: GenericTerminalSession) => {
      queryClient.setQueryData(
        queryKeys.terminals.generic(),
        (current: GenericTerminalSession[] | undefined) => [...(current ?? []), session],
      )
      setActiveSessionId(session.id)
    },
    [queryClient],
  )

  const createMutation = useMutation({
    mutationFn: (agentLaunch?: TerminalAgentLaunch) => createGenericTerminal({ agentLaunch }),
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
      queryClient.setQueryData(
        queryKeys.terminals.generic(),
        (current: GenericTerminalSession[] | undefined) =>
          (current ?? []).filter((session) => session.id !== sessionId),
      )
      removeSessionPreference(sessionId)
      if (resolvedActiveId === sessionId) {
        setActiveSessionId(null)
      }
    },
    [queryClient, removeSessionPreference, resolvedActiveId],
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
            hidePlan
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
            Nenhum terminal aberto. Use &quot;cd&quot; para navegar onde quiser.
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
              onKeyboardShortcut={session.id === resolvedActiveId ? handleKeyboardEvent : undefined}
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
