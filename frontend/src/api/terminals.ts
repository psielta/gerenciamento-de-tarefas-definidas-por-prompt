import { api } from './client'
import {
  genericTerminalSessionSchema,
  terminalAgentLaunchSchema,
  terminalCapabilitiesSchema,
  terminalGroupSchema,
  terminalOutputHistorySchema,
  terminalSessionSchema,
} from './schemas'
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

export async function listAllTerminals() {
  const data = await api.get('terminals').json<unknown>()
  return z.array(terminalGroupSchema).parse(data)
}

export async function listGenericTerminals() {
  const data = await api.get('terminals/generic').json<unknown>()
  return z.array(genericTerminalSessionSchema).parse(data)
}

export async function getTerminalOutputHistory(sessionId: string) {
  const data = await api.get(`terminals/${sessionId}/output-history`).json<unknown>()
  return terminalOutputHistorySchema.parse(data)
}

type CreateTerminalOptions = {
  shell?: string
  agentLaunch?: TerminalAgentLaunch
  submitPrompt?: boolean
}

export async function createTerminal(promptId: string, options: CreateTerminalOptions = {}) {
  const payload: { shell?: string; agentLaunch?: TerminalAgentLaunch; submitPrompt?: boolean } = {}
  if (options.shell) {
    payload.shell = options.shell
  }
  if (options.agentLaunch) {
    payload.agentLaunch = terminalAgentLaunchSchema.parse(options.agentLaunch)
  }
  if (options.submitPrompt) {
    payload.submitPrompt = true
  }

  const data = await api.post(`prompts/${promptId}/terminals`, { json: payload }).json<unknown>()
  return terminalSessionSchema.parse(data)
}

export async function createGenericTerminal(options: CreateTerminalOptions = {}) {
  const payload: { shell?: string; agentLaunch?: TerminalAgentLaunch } = {}
  if (options.shell) {
    payload.shell = options.shell
  }
  if (options.agentLaunch) {
    payload.agentLaunch = terminalAgentLaunchSchema.parse(options.agentLaunch)
  }

  const data = await api.post('terminals/generic', { json: payload }).json<unknown>()
  return genericTerminalSessionSchema.parse(data)
}

export async function closeTerminal(sessionId: string) {
  await api.delete(`terminals/${sessionId}`)
}
