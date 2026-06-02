import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getAiModels, getAiSettings, translatePrompt } from '@/api/ai'
import { TranslateDialog } from './translate-dialog'

vi.mock('@/api/ai', () => ({
  getAiModels: vi.fn(),
  getAiSettings: vi.fn(),
  translatePrompt: vi.fn(),
}))

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

function renderDialog(overrides: {
  onApply?: (translated: string) => void
  onClose?: () => void
} = {}) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })

  const onApply = overrides.onApply ?? vi.fn()
  const onClose = overrides.onClose ?? vi.fn()

  render(
    <QueryClientProvider client={queryClient}>
      <TranslateDialog
        content="Prompt original com @src/main.cs"
        onApply={onApply}
        onClose={onClose}
      />
    </QueryClientProvider>,
  )

  return { onApply, onClose }
}

describe('TranslateDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAiModels).mockResolvedValue([
      {
        id: 'gemini-test',
        label: 'Gemini Test',
        thinkingMode: 'none',
        canDisableThinking: true,
        thinkingBudgetMin: 0,
        thinkingBudgetMax: 0,
        minCacheTokens: 1024,
      },
    ])
    vi.mocked(getAiSettings).mockResolvedValue({
      model: 'gemini-test',
      temperature: 0.4,
      thinkingEnabled: false,
      thinkingBudget: null,
      thinkingLevel: null,
    })
    vi.mocked(translatePrompt).mockResolvedValue({
      content: 'Translated prompt with @src/main.cs',
      promptTokens: 12,
      candidateTokens: 5,
    })
  })

  afterEach(() => {
    cleanup()
  })

  it('translates without refine-only controls and applies translated preview', async () => {
    const user = userEvent.setup()
    const { onApply, onClose } = renderDialog()

    await waitFor(() => {
      expect(screen.getByDisplayValue('Gemini Test')).toBeInTheDocument()
    })

    expect(screen.queryByLabelText('Buscar arquivos de contexto')).not.toBeInTheDocument()
    expect(screen.queryByText(/Instruções de refinamento/)).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^Traduzir$/ }))

    await waitFor(() => {
      expect(translatePrompt).toHaveBeenCalledWith(expect.objectContaining({
        content: 'Prompt original com @src/main.cs',
        model: 'gemini-test',
        temperature: 0.4,
      }))
    })

    expect(await screen.findByText('Original')).toBeInTheDocument()
    expect(screen.getByText('Tradução para inglês')).toBeInTheDocument()
    expect(screen.getByText('Prompt original com @src/main.cs')).toBeInTheDocument()
    expect(screen.getByText('Translated prompt with @src/main.cs')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^Aplicar no editor$/ }))

    expect(onApply).toHaveBeenCalledWith('Translated prompt with @src/main.cs')
    expect(onClose).toHaveBeenCalled()
  })
})
