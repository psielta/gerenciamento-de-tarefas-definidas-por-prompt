import type { AgentUsageInfo, AgentUsageStatus, AgentUsageWindow } from '@/api/schemas'

export const statusLabels: Record<AgentUsageStatus, string> = {
  Ok: 'Disponivel',
  NoToken: 'Sem token',
  Unauthorized: 'Nao autorizado',
  RateLimited: 'Limitado',
  HttpError: 'Erro HTTP',
  Timeout: 'Timeout',
  NetworkError: 'Erro de rede',
  NoData: 'Sem dados',
  Disabled: 'Desativado',
  Unavailable: 'Indisponivel',
}

export const windowLabels: Record<string, string> = {
  five_hour: 'Sessao 5h',
  seven_day: 'Semana',
  seven_day_opus: 'Semana Opus',
  primary: 'Sessao 5h',
  secondary: 'Semana',
}

export function getUsageVariant(value: number | null | undefined, status: AgentUsageStatus) {
  if (status !== 'Ok' || value === null || value === undefined) {
    return 'muted' as const
  }

  if (value >= 90) {
    return 'crit' as const
  }

  if (value >= 70) {
    return 'warn' as const
  }

  return 'ok' as const
}

export function getPrimaryWindow(info: AgentUsageInfo): AgentUsageWindow | null {
  const preferredKey = info.agent.toLowerCase().includes('codex') ? 'primary' : 'five_hour'
  return info.windows.find((window) => window.key === preferredKey) ?? info.windows[0] ?? null
}

export function formatPercent(value: number | null | undefined) {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '--'
  }

  return `${Math.round(value)}%`
}

export function formatReset(resetsAtUtc: string | null) {
  if (!resetsAtUtc) {
    return 'reset indisponivel'
  }

  const reset = new Date(resetsAtUtc)
  const now = new Date()
  const diffMs = reset.getTime() - now.getTime()
  if (!Number.isFinite(diffMs)) {
    return 'reset indisponivel'
  }

  if (diffMs <= 0) {
    return 'reset agora'
  }

  const minutes = Math.floor(diffMs / 60_000)
  const hours = Math.floor(minutes / 60)
  const days = Math.floor(hours / 24)

  if (days > 0) {
    return `reset em ${days}d ${hours % 24}h`
  }

  if (hours > 0) {
    return `reset em ${hours}h ${minutes % 60}m`
  }

  return `reset em ${minutes}m`
}

export function formatTokens(window: AgentUsageWindow) {
  if (window.usedTokens === null || window.limitTokens === null) {
    return null
  }

  return `${window.usedTokens.toLocaleString('pt-BR')} / ${window.limitTokens.toLocaleString('pt-BR')} tokens`
}
