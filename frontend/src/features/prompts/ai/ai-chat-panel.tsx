import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Bot, Loader2, Send, Trash2, User } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import { deleteChatSession, startChatSession } from '@/api/ai'
import { queryKeys } from '@/api/query-keys'
import { type AiChatSession } from '@/api/schemas'
import { getErrorMessage } from '@/api/client'
import { Switch } from '@/components/ui/switch'
import { MarkdownContent } from './markdown-content'
import { type ModelConfig } from './ai-model-config'
import { useChatStream, type ChatStreamMessage } from './use-chat-stream'

type AiChatPanelProps = {
  promptId?: string
  workingDirectoryId?: string
  promptContent?: string
  modelConfig: ModelConfig
  activeSession: AiChatSession | null
  onSessionCreated: (session: AiChatSession) => void
  onSessionDeleted: () => void
}

export function AiChatPanel({
  promptId,
  workingDirectoryId,
  promptContent,
  modelConfig,
  activeSession,
  onSessionCreated,
  onSessionDeleted,
}: AiChatPanelProps) {
  const queryClient = useQueryClient()
  const [input, setInput] = useState('')
  const [includeContext, setIncludeContext] = useState(false)
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const { messages, isStreaming, error, sendMessage, initMessages } = useChatStream()

  useEffect(() => {
    if (activeSession === null) {
      // Session deleted — clear the list
      initMessages([])
      return
    }
    // Only restore when the session already has persisted history.
    // New sessions arrive with messages: [] — skipping prevents wiping
    // the optimistic messages that sendMessage already added to state.
    if (activeSession.messages.length > 0) {
      initMessages(
        activeSession.messages.map((m) => ({
          id: m.id,
          role: m.role as 'user' | 'model',
          content: m.content,
          cachedTokens: m.cachedTokens,
        })),
      )
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeSession?.id, initMessages])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  // Auto-resize textarea
  useEffect(() => {
    const ta = textareaRef.current
    if (!ta) return
    ta.style.height = 'auto'
    ta.style.height = `${Math.min(ta.scrollHeight, 160)}px`
  }, [input])

  const startSessionMutation = useMutation({
    mutationFn: () =>
      startChatSession({
        title: 'Chat',
        promptId,
        workingDirectoryId,
        model: modelConfig.model,
        temperature: modelConfig.temperature,
        thinkingEnabled: modelConfig.thinkingEnabled,
        thinkingBudget: modelConfig.thinkingBudget,
        thinkingLevel: modelConfig.thinkingLevel,
      }),
    onSuccess: (session) => {
      onSessionCreated(session)
      void queryClient.invalidateQueries({ queryKey: queryKeys.ai.sessions(promptId) })
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  })

  const deleteSessionMutation = useMutation({
    mutationFn: () => deleteChatSession(activeSession!.id),
    onSuccess: () => {
      onSessionDeleted()
      void queryClient.invalidateQueries({ queryKey: queryKeys.ai.sessions(promptId) })
      toast.success('Sessao removida.')
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  })

  const handleSend = async () => {
    const text = input.trim()
    if (!text || isStreaming) return

    let sessionId = activeSession?.id
    if (!sessionId) {
      const session = await startSessionMutation.mutateAsync()
      sessionId = session.id
    }

    setInput('')
    await sendMessage({
      sessionId,
      text,
      includePromptContext: includeContext,
      promptContent,
    })
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void handleSend()
    }
  }

  const isBusy = isStreaming || startSessionMutation.isPending

  return (
    <div className="flex h-full flex-col">
      {/* Session header */}
      {activeSession ? (
        <div className="flex items-center justify-between border-b border-[#e8ede5] bg-[#f7f8f6] px-4 py-2.5">
          <div className="flex items-center gap-2 min-w-0">
            <div className="h-2 w-2 rounded-full bg-[#4ade80] flex-shrink-0" />
            <span className="text-xs font-medium text-[#172126] truncate">{activeSession.title}</span>
            <span className="text-xs text-[#9aaf9e] flex-shrink-0">· {activeSession.model}</span>
          </div>
          <button
            onClick={() => deleteSessionMutation.mutate()}
            disabled={deleteSessionMutation.isPending}
            className="ml-2 flex-shrink-0 rounded-md p-1.5 text-[#9aaf9e] transition-colors hover:bg-[#fee2e2] hover:text-[#b42318]"
            title="Encerrar sessao"
          >
            <Trash2 className="h-3.5 w-3.5" />
          </button>
        </div>
      ) : null}

      {/* Messages area */}
      <div className="flex-1 overflow-y-auto p-4 space-y-5">
        {messages.length === 0 ? (
          <EmptyState hasPromptContent={Boolean(promptContent)} />
        ) : (
          <>
            {messages.map((msg) => (
              <ChatMessage key={msg.id} message={msg} />
            ))}
            {isBusy && messages[messages.length - 1]?.role !== 'model' ? (
              <TypingIndicator />
            ) : null}
            {error ? (
              <div className="rounded-lg border border-[#fca5a5] bg-[#fff1f1] px-4 py-3 text-sm text-[#991b1b]">
                {error}
              </div>
            ) : null}
          </>
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Input area */}
      <div className="border-t border-[#e8ede5] p-3">
        {promptContent ? (
          <div className="mb-2">
            <Switch
              id="include-context"
              checked={includeContext}
              onChange={(e) => setIncludeContext(e.target.checked)}
              label="Incluir conteudo do prompt como contexto"
            />
          </div>
        ) : null}
        <div className="flex items-end gap-2 rounded-xl border border-[#d9dfd5] bg-white px-3 py-2 shadow-sm focus-within:border-[#254632] focus-within:ring-1 focus-within:ring-[#254632] transition-all">
          <textarea
            ref={textareaRef}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Mensagem... (Enter envia, Shift+Enter nova linha)"
            rows={1}
            style={{ height: 'auto', minHeight: '24px', maxHeight: '160px' }}
            className="flex-1 resize-none bg-transparent text-sm text-[#172126] placeholder:text-[#9aaf9e] focus:outline-none leading-6"
            disabled={isBusy}
          />
          <button
            type="button"
            onClick={() => void handleSend()}
            disabled={!input.trim() || isBusy}
            className="flex-shrink-0 flex items-center justify-center h-8 w-8 rounded-lg bg-[#254632] text-white transition-all hover:bg-[#1a3323] disabled:bg-[#d9dfd5] disabled:text-[#9aaf9e] disabled:cursor-not-allowed"
          >
            {isBusy ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Send className="h-4 w-4" />
            )}
          </button>
        </div>
        <p className="mt-1.5 text-center text-[10px] text-[#b0bcb4]">
          Gemini pode cometer erros. Verifique informacoes importantes.
        </p>
      </div>
    </div>
  )
}

function EmptyState({ hasPromptContent }: { hasPromptContent: boolean }) {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-3 py-12 text-center">
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-[#eef2eb]">
        <Bot className="h-7 w-7 text-[#254632]" />
      </div>
      <div>
        <p className="text-sm font-medium text-[#172126]">Assistente de Engenharia de Prompts</p>
        <p className="mt-1 text-xs text-[#66746b]">
          Tire duvidas sobre como estruturar seus prompts para Claude Code e Codex.
        </p>
      </div>
      {hasPromptContent ? (
        <p className="rounded-lg border border-[#d9dfd5] bg-[#f7f8f6] px-3 py-2 text-xs text-[#66746b]">
          Ative &quot;Incluir conteudo do prompt&quot; para enviar o prompt atual como contexto.
        </p>
      ) : null}
    </div>
  )
}

function TypingIndicator() {
  return (
    <div className="flex items-start gap-3">
      <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-full bg-[#eef2eb]">
        <Bot className="h-4 w-4 text-[#254632]" />
      </div>
      <div className="rounded-2xl rounded-tl-sm border border-[#e8ede5] bg-white px-4 py-3">
        <div className="flex items-center gap-1.5">
          <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-[#9aaf9e]" style={{ animationDelay: '0ms' }} />
          <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-[#9aaf9e]" style={{ animationDelay: '150ms' }} />
          <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-[#9aaf9e]" style={{ animationDelay: '300ms' }} />
        </div>
      </div>
    </div>
  )
}

function ChatMessage({ message }: { message: ChatStreamMessage }) {
  const isUser = message.role === 'user'

  if (isUser) {
    return (
      <div className="flex items-start justify-end gap-3">
        <div className="flex max-w-[80%] flex-col items-end gap-1">
          <div className="rounded-2xl rounded-tr-sm bg-[#254632] px-4 py-2.5 text-sm leading-relaxed text-white">
            <p className="whitespace-pre-wrap">{message.content}</p>
          </div>
        </div>
        <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-full bg-[#254632]">
          <User className="h-4 w-4 text-white" />
        </div>
      </div>
    )
  }

  return (
    <div className="flex items-start gap-3">
      {/* Avatar */}
      <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-full bg-[#eef2eb]">
        <Bot className="h-4 w-4 text-[#254632]" />
      </div>

      {/* Full-width model message — no bubble constraint so code blocks scroll properly */}
      <div className="min-w-0 flex-1">
        {message.thought ? (
          <details className="mb-2">
            <summary className="cursor-pointer select-none rounded-lg border border-[#e8ede5] bg-[#f7f8f6] px-3 py-1.5 text-xs text-[#66746b] hover:bg-[#eef2eb]">
              Ver raciocinio da IA
            </summary>
            <div className="mt-1 rounded-lg border border-[#e8ede5] bg-[#fafbf9] px-3 py-2 text-xs italic leading-relaxed text-[#66746b]">
              {message.thought}
            </div>
          </details>
        ) : null}

        {message.content ? (
          <MarkdownContent content={message.content} />
        ) : (
          <span className="text-sm text-[#9aaf9e]">...</span>
        )}

        {message.cachedTokens ? (
          <p className="mt-1 text-[10px] text-[#b0bcb4]">{message.cachedTokens} tokens em cache</p>
        ) : null}
      </div>
    </div>
  )
}

