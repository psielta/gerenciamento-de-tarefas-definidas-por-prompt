import { useContext } from 'react'
import { AgentTerminalContext } from './agent-terminal-context'

export function useAgentTerminal() {
  const context = useContext(AgentTerminalContext)
  if (!context) {
    throw new Error('useAgentTerminal must be used within AgentTerminalProvider')
  }

  return context
}

/**
 * Variante que nao lanca quando o provider esta ausente. Usada por componentes
 * (ex.: GeneratePromptDrawer) cujo funcionamento principal nao depende do
 * terminal de agente e que tambem sao renderizados isoladamente em testes.
 */
export function useOptionalAgentTerminal() {
  return useContext(AgentTerminalContext)
}
