import { useQuery } from '@tanstack/react-query'
import { Bot, Code2 } from 'lucide-react'
import type { AgentUsageInfo } from '@/api/schemas'
import { getAgentUsage } from '@/api/agent-usage'
import { queryKeys } from '@/api/query-keys'
import { Popover } from '@/components/ui/popover'
import { Progress } from '@/components/ui/progress'
import { formatPercent, getPrimaryWindow, getUsageVariant, statusLabels } from './constants'
import { UsagePopover } from './usage-popover'

export function UsageIndicator() {
  const query = useQuery({
    queryKey: queryKeys.agentUsage.current(),
    queryFn: getAgentUsage,
    refetchInterval: 60_000,
  })

  const content = query.data ? (
    <UsagePopover claude={query.data.claude} codex={query.data.codex} capturedAtUtc={query.data.capturedAtUtc} />
  ) : (
    <div className="text-sm text-[#66746b]">
      {query.isError ? 'Limites indisponiveis.' : 'Carregando limites...'}
    </div>
  )

  return (
    <Popover
      ariaLabel="Ver limites de uso dos agentes"
      triggerClassName="rounded-md focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#254632]"
      trigger={
        <div className="flex items-center gap-2 rounded-md border border-[#d9dfd5] bg-white px-2.5 py-1.5">
          <AgentPill label="Claude" icon="claude" info={query.data?.claude ?? null} loading={query.isLoading} />
          <span className="h-5 w-px bg-[#d9dfd5]" />
          <AgentPill label="Codex" icon="codex" info={query.data?.codex ?? null} loading={query.isLoading} />
        </div>
      }
    >
      {content}
    </Popover>
  )
}

function AgentPill({
  label,
  icon,
  info,
  loading,
}: {
  label: string
  icon: 'claude' | 'codex'
  info: AgentUsageInfo | null
  loading: boolean
}) {
  const primary = info ? getPrimaryWindow(info) : null
  const value = primary?.usedPercent
  const status = info?.status ?? (loading ? 'Unavailable' : 'NoData')
  const Icon = icon === 'codex' ? Code2 : Bot

  return (
    <span className="grid min-w-[4.8rem] gap-0.5">
      <span className="flex items-center justify-between gap-1 text-[0.68rem] font-semibold text-[#253035]">
        <span className="inline-flex min-w-0 items-center gap-1">
          <Icon className="h-3.5 w-3.5 shrink-0 text-[#254632]" />
          <span className="truncate">{label}</span>
        </span>
        <span>{loading ? '--' : formatPercent(value)}</span>
      </span>
      <Progress value={value ?? 0} variant={getUsageVariant(value, status)} />
      {info && info.status !== 'Ok' ? (
        <span className="truncate text-[0.62rem] text-[#8a5a00]">{statusLabels[info.status]}</span>
      ) : null}
    </span>
  )
}
