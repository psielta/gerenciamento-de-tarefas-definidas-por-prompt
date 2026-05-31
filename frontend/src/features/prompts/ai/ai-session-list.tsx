import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bot, MessageSquare, Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { deleteChatSession, getChatSession, listChatSessions } from '@/api/ai'
import { queryKeys } from '@/api/query-keys'
import { type AiChatSession } from '@/api/schemas'
import { getErrorMessage } from '@/api/client'

type AiSessionListProps = {
  promptId?: string
  workingDirectoryId?: string
  activeSessionId?: string
  onSelectSession: (session: AiChatSession) => void
  onNewSession: () => void
}

export function AiSessionList({
  promptId,
  workingDirectoryId,
  activeSessionId,
  onSelectSession,
  onNewSession,
}: AiSessionListProps) {
  const queryClient = useQueryClient()

  const sessionsQuery = useQuery({
    queryKey: queryKeys.ai.sessions(promptId, workingDirectoryId),
    queryFn: () => listChatSessions({ promptId, workingDirectoryId }),
  })

  const loadSessionMutation = useMutation({
    mutationFn: (id: string) => getChatSession(id),
    onSuccess: (session) => {
      onSelectSession(session)
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  })

  const deleteSessionMutation = useMutation({
    mutationFn: (id: string) => deleteChatSession(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.ai.sessions(promptId, workingDirectoryId) })
      toast.success('Sessao removida.')
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  })

  const sessions = sessionsQuery.data ?? []

  return (
    <div className="flex h-full flex-col">
      {/* Toolbar */}
      <div className="flex items-center justify-between border-b border-[#e8ede5] px-4 py-3">
        <span className="text-sm font-medium text-[#172126]">
          {sessions.length === 0 ? 'Nenhuma sessao' : `${sessions.length} sessao${sessions.length > 1 ? 'es' : ''}`}
        </span>
        <button
          onClick={onNewSession}
          className="flex items-center gap-1.5 rounded-lg bg-[#254632] px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-[#1a3323]"
        >
          <Plus className="h-3.5 w-3.5" />
          Nova sessao
        </button>
      </div>

      {/* List */}
      <div className="flex-1 overflow-y-auto p-3">
        {sessionsQuery.isLoading ? (
          <div className="flex items-center justify-center py-12 text-sm text-[#9aaf9e]">
            Carregando...
          </div>
        ) : sessions.length === 0 ? (
          <EmptyHistory />
        ) : (
          <div className="flex flex-col gap-1">
            {sessions.map((s) => (
              <SessionItem
                key={s.id}
                session={s}
                isActive={s.id === activeSessionId}
                isLoading={loadSessionMutation.isPending && loadSessionMutation.variables === s.id}
                isDeleting={deleteSessionMutation.isPending && deleteSessionMutation.variables === s.id}
                onSelect={() => loadSessionMutation.mutate(s.id)}
                onDelete={() => deleteSessionMutation.mutate(s.id)}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

function SessionItem({
  session,
  isActive,
  isLoading,
  isDeleting,
  onSelect,
  onDelete,
}: {
  session: AiChatSession
  isActive: boolean
  isLoading: boolean
  isDeleting: boolean
  onSelect: () => void
  onDelete: () => void
}) {
  const date = new Date(session.createdAtUtc)
  const formatted = date.toLocaleDateString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })

  return (
    <div
      className={`group flex cursor-pointer items-start gap-3 rounded-lg px-3 py-2.5 transition-colors ${
        isActive
          ? 'bg-[#eef2eb] ring-1 ring-[#254632]/20'
          : 'hover:bg-[#f7f8f6]'
      }`}
      onClick={onSelect}
    >
      {/* Icon */}
      <div className={`mt-0.5 flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full ${isActive ? 'bg-[#254632]' : 'bg-[#e8ede5]'}`}>
        <MessageSquare className={`h-3.5 w-3.5 ${isActive ? 'text-white' : 'text-[#66746b]'}`} />
      </div>

      {/* Content */}
      <div className="min-w-0 flex-1">
        <p className={`truncate text-sm font-medium ${isActive ? 'text-[#172126]' : 'text-[#374151]'}`}>
          {isLoading ? 'Carregando...' : session.title}
        </p>
        <div className="mt-0.5 flex items-center gap-2">
          <span className="truncate text-xs text-[#9aaf9e]">{session.model}</span>
          <span className="text-[#d9dfd5]">·</span>
          <span className="flex-shrink-0 text-xs text-[#9aaf9e]">{formatted}</span>
        </div>
      </div>

      {/* Delete */}
      <button
        onClick={(e) => { e.stopPropagation(); onDelete() }}
        disabled={isDeleting}
        className="ml-1 flex-shrink-0 rounded-md p-1 text-transparent transition-colors group-hover:text-[#9aaf9e] hover:!text-[#b42318]"
        title="Remover sessao"
      >
        <Trash2 className="h-3.5 w-3.5" />
      </button>
    </div>
  )
}

function EmptyHistory() {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-center">
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-[#f7f8f6]">
        <Bot className="h-6 w-6 text-[#d9dfd5]" />
      </div>
      <div>
        <p className="text-sm font-medium text-[#66746b]">Nenhuma conversa ainda</p>
        <p className="mt-1 text-xs text-[#9aaf9e]">Inicie uma nova sessao no Chat.</p>
      </div>
    </div>
  )
}
