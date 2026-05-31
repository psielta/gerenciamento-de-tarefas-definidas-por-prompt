import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getAgentUsage } from '@/api/agent-usage'
import type { AgentUsage } from '@/api/schemas'
import { UsageIndicator } from './usage-indicator'

vi.mock('@/api/agent-usage')

const usage: AgentUsage = {
  capturedAtUtc: '2026-05-31T12:00:00Z',
  claude: {
    agent: 'Claude',
    status: 'Ok',
    httpStatusCode: null,
    statusDetail: null,
    plan: 'max',
    windows: [
      {
        key: 'five_hour',
        label: 'Sessao 5h',
        usedPercent: 23,
        resetsAtUtc: '2026-05-31T14:00:00Z',
        windowMinutes: 300,
        estimated: false,
        usedTokens: null,
        limitTokens: null,
      },
      {
        key: 'seven_day',
        label: 'Semana',
        usedPercent: 51,
        resetsAtUtc: '2026-06-07T12:00:00Z',
        windowMinutes: 10080,
        estimated: false,
        usedTokens: null,
        limitTokens: null,
      },
      {
        key: 'seven_day_opus',
        label: 'Semana Opus',
        usedPercent: 7,
        resetsAtUtc: '2026-06-07T12:00:00Z',
        windowMinutes: 10080,
        estimated: false,
        usedTokens: null,
        limitTokens: null,
      },
    ],
  },
  codex: {
    agent: 'Codex',
    status: 'Ok',
    httpStatusCode: null,
    statusDetail: null,
    plan: 'pro',
    windows: [
      {
        key: 'primary',
        label: 'Sessao 5h',
        usedPercent: 82,
        resetsAtUtc: '2026-05-31T13:00:00Z',
        windowMinutes: 300,
        estimated: false,
        usedTokens: null,
        limitTokens: null,
      },
      {
        key: 'secondary',
        label: 'Semana',
        usedPercent: 18,
        resetsAtUtc: '2026-06-07T12:00:00Z',
        windowMinutes: 10080,
        estimated: false,
        usedTokens: null,
        limitTokens: null,
      },
    ],
  },
}

function renderIndicator(data: AgentUsage = usage) {
  vi.mocked(getAgentUsage).mockResolvedValue(data)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const result = render(
    <QueryClientProvider client={client}>
      <UsageIndicator />
    </QueryClientProvider>,
  )

  return within(result.container)
}

describe('UsageIndicator', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders compact percentages and opens the details popover', async () => {
    const view = renderIndicator()

    await waitFor(() => expect(view.getByText('23%')).toBeInTheDocument())
    expect(view.getByText('82%')).toBeInTheDocument()
    expect(view.getAllByRole('progressbar')[0]).toHaveAttribute('aria-valuenow', '23')

    fireEvent.click(view.getByRole('button', { name: 'Ver limites de uso dos agentes' }))

    expect(await view.findByText('Limites dos agentes')).toBeInTheDocument()
    expect(view.getByText('Semana Opus')).toBeInTheDocument()
    expect(view.getAllByText('Codex')).toHaveLength(2)
  })

  it('shows degraded statuses without breaking the header', async () => {
    const degraded: AgentUsage = {
      ...usage,
      claude: {
        ...usage.claude,
        status: 'NoToken',
        statusDetail: 'Claude credentials were not found.',
        windows: [],
      },
      codex: {
        ...usage.codex,
        status: 'Disabled',
        statusDetail: 'Agent usage monitoring is disabled.',
        windows: [],
      },
    }
    const view = renderIndicator(degraded)

    await waitFor(() => expect(view.getByText('Sem token')).toBeInTheDocument())
    expect(view.getByText('Desativado')).toBeInTheDocument()

    fireEvent.click(view.getByRole('button', { name: 'Ver limites de uso dos agentes' }))

    expect(await view.findByText('Claude credentials were not found.')).toBeInTheDocument()
    expect(view.getByText('Agent usage monitoring is disabled.')).toBeInTheDocument()
  })
})
