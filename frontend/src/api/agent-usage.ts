import { api } from './client'
import { agentUsageSchema, type AgentUsage } from './schemas'

export async function getAgentUsage(): Promise<AgentUsage> {
  const data = await api.get('agent-usage').json<unknown>()
  return agentUsageSchema.parse(data)
}
