import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { GitCommit } from '@/api/schemas'
import { GitHistoryPanel } from './git-history-panel'

const commits: GitCommit[] = [
  {
    hash: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
    shortHash: 'bbbbbbb',
    author: 'Author Two',
    date: '2026-01-02T00:00:00Z',
    message: 'Second',
    parentHash: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
  },
  {
    hash: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    shortHash: 'aaaaaaa',
    author: 'Author One',
    date: '2026-01-01T00:00:00Z',
    message: 'First',
    parentHash: '',
  },
]

function renderPanel(overrides: Partial<React.ComponentProps<typeof GitHistoryPanel>> = {}) {
  const onSelectCommit = vi.fn()
  const onChangeMode = vi.fn()
  const onPickBase = vi.fn()
  const onPickTarget = vi.fn()

  render(
    <GitHistoryPanel
      commits={commits}
      isRepository
      isLoading={false}
      error={null}
      selectedHash="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
      compareMode="parent"
      baseHash={null}
      targetHash={null}
      onSelectCommit={onSelectCommit}
      onChangeMode={onChangeMode}
      onPickBase={onPickBase}
      onPickTarget={onPickTarget}
      {...overrides}
    />,
  )

  return { onSelectCommit, onChangeMode, onPickBase, onPickTarget }
}

describe('GitHistoryPanel', () => {
  afterEach(() => {
    cleanup()
  })

  it('renders commits and selects one', async () => {
    const user = userEvent.setup()
    const { onSelectCommit } = renderPanel()

    expect(screen.getByText('Second')).toBeInTheDocument()
    expect(screen.getByText('First')).toBeInTheDocument()

    await user.click(screen.getByText('First'))
    expect(onSelectCommit).toHaveBeenCalledWith('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')
  })

  it('shows not-a-repo state', () => {
    renderPanel({ isRepository: false, commits: [] })

    expect(screen.getByText('Nao e um repositorio git.')).toBeInTheDocument()
  })

  it('shows empty history state', () => {
    renderPanel({ commits: [] })

    expect(screen.getByText('Sem historico para este arquivo.')).toBeInTheDocument()
  })

  it('changes compare mode', async () => {
    const user = userEvent.setup()
    const { onChangeMode } = renderPanel()

    await user.click(screen.getByRole('button', { name: 'vs working tree' }))
    expect(onChangeMode).toHaveBeenCalledWith('working')
  })
})