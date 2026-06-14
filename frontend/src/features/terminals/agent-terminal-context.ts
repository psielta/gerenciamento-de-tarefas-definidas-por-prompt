import { createContext } from 'react'
import type { Prompt } from '@/api/schemas'

export type AgentTerminalContextValue = {
  /**
   * Abre o fluxo "criar terminal com agente" para um prompt filho recem-criado:
   * mostra o dialog de confirmacao e, ao confirmar, abre o drawer com o agente
   * ja executando o conteudo do prompt.
   */
  requestAgentTerminal: (prompt: Prompt) => void
}

export const AgentTerminalContext = createContext<AgentTerminalContextValue | null>(null)
