import { api } from './client'
import { terminalAgentLaunchSchema, terminalCapabilitiesSchema, terminalSessionSchema } from './schemas'
import type { TerminalAgentLaunch } from './schemas'
import { z } from 'zod'

export async function getTerminalCapabilities() {
  const data = await api.get('terminals/capabilities').json<unknown>()
  return terminalCapabilitiesSchema.parse(data)
}

export async function listTerminals(promptId: string) {
  const data = await api.get(`prompts/${promptId}/terminals`).json<unknown>()
  return z.array(terminalSessionSchema).parse(data)
}

type CreateTerminalOptions = {
  shell?: string
  agentLaunch?: TerminalAgentLaunch
}

export async function createTerminal(promptId: string, options: CreateTerminalOptions = {}) {
  const payload: { shell?: string; agentLaunch?: TerminalAgentLaunch } = {}
  if (options.shell) {
    payload.shell = options.shell
  }
  if (options.agentLaunch) {
    payload.agentLaunch = terminalAgentLaunchSchema.parse(options.agentLaunch)
  }

  const data = await api.post(`prompts/${promptId}/terminals`, { json: payload }).json<unknown>()
  return terminalSessionSchema.parse(data)
}

export async function closeTerminal(sessionId: string) {
  await api.delete(`terminals/${sessionId}`)
}