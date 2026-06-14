import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2, TerminalSquare, X } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '@/api/client'
import { queryKeys } from '@/api/query-keys'
import { createTerminal } from '@/api/terminals'
import type { Prompt, TargetAgent, TerminalAgentLaunch, TerminalSession } from '@/api/schemas'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'

const AGENT_LAUNCH_OPTIONS: Array<{ value: TerminalAgentLaunch; label: string }> = [
  { value: 'Claude', label: 'Claude' },
  { value: 'Codex', label: 'Codex' },
  { value: 'Grok', label: 'Grok' },
]

/** Mapeia o agente alvo do prompt para o agente executor do terminal. */
const TARGET_AGENT_TO_LAUNCH: Record<TargetAgent, TerminalAgentLaunch> = {
  ClaudeCode: 'Claude',
  Codex: 'Codex',
  Grok: 'Grok',
}

type CreateAgentTerminalDialogProps = {
  prompt: Prompt
  onCancel: () => void
  onCreated: (promptId: string, session: TerminalSession) => void
}

export function CreateAgentTerminalDialog({ prompt, onCancel, onCreated }: CreateAgentTerminalDialogProps) {
  const queryClient = useQueryClient()
  const [agentLaunch, setAgentLaunch] = useState<TerminalAgentLaunch>(TARGET_AGENT_TO_LAUNCH[prompt.targetAgent])

  const createMutation = useMutation({
    mutationFn: () => createTerminal(prompt.id, { agentLaunch, submitPrompt: true }),
    onSuccess: (session) => {
      queryClient.setQueryData(
        queryKeys.terminals.forPrompt(prompt.id),
        (current: TerminalSession[] | undefined) => [...(current ?? []), session],
      )
      onCreated(prompt.id, session)
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 pt-16">
      <div className="flex w-full max-w-lg flex-col gap-5 rounded-xl border border-border bg-card p-6 shadow-xl">
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 items-start gap-2.5">
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted">
              <TerminalSquare className="h-4 w-4 text-primary" />
            </div>
            <div className="min-w-0">
              <h2 className="text-sm font-semibold text-foreground">Criar terminal com agente?</h2>
              <p className="mt-0.5 truncate text-xs text-muted-foreground" title={prompt.title}>
                {prompt.title}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onCancel}
            disabled={createMutation.isPending}
            className="rounded-lg p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
            aria-label="Fechar"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <p className="text-sm text-muted-foreground">
          Abrir um terminal e pedir para o agente executar este prompt filho agora? O conteudo sera enviado
          automaticamente assim que o agente iniciar.
        </p>

        <FormField label="Agente" htmlFor="agent-terminal-launch">
          <Select
            id="agent-terminal-launch"
            value={agentLaunch}
            onChange={(event) => setAgentLaunch(event.target.value as TerminalAgentLaunch)}
            disabled={createMutation.isPending}
          >
            {AGENT_LAUNCH_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </Select>
        </FormField>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onCancel} disabled={createMutation.isPending}>
            Agora nao
          </Button>
          <Button type="button" onClick={() => createMutation.mutate()} disabled={createMutation.isPending}>
            {createMutation.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <TerminalSquare className="h-4 w-4" />
            )}
            Criar e abrir
          </Button>
        </div>
      </div>
    </div>
  )
}
