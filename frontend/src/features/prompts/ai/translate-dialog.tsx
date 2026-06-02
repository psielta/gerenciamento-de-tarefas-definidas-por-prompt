import { useMutation, useQuery } from '@tanstack/react-query'
import { Languages, Loader2, RotateCcw, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import { getAiSettings, translatePrompt } from '@/api/ai'
import { getErrorMessage } from '@/api/client'
import { queryKeys } from '@/api/query-keys'
import { Button } from '@/components/ui/button'
import { AiModelConfig, type ModelConfig } from './ai-model-config'
import { MarkdownContent } from './markdown-content'

type TranslateDialogProps = {
  content: string
  onApply: (translated: string) => void
  onClose: () => void
}

const DEFAULT_CONFIG: ModelConfig = {
  model: 'gemini-3.5-flash',
  temperature: 0.4,
  thinkingEnabled: true,
  thinkingBudget: null,
  thinkingLevel: 'high',
}

function getThinkingMode(config: ModelConfig) {
  if (!config.thinkingEnabled) {
    return 'none'
  }

  if (config.thinkingBudget != null) {
    return 'budget'
  }

  if (config.thinkingLevel != null) {
    return 'level'
  }

  return 'none'
}

export function TranslateDialog({ content, onApply, onClose }: TranslateDialogProps) {
  const settingsQuery = useQuery({
    queryKey: queryKeys.ai.settings(),
    queryFn: getAiSettings,
  })

  const [config, setConfig] = useState<ModelConfig>(DEFAULT_CONFIG)
  const [preview, setPreview] = useState<string | null>(null)

  const applied = useRef(false)
  useEffect(() => {
    if (settingsQuery.data && !applied.current) {
      applied.current = true
      setConfig({
        model: settingsQuery.data.model,
        temperature: settingsQuery.data.temperature,
        thinkingEnabled: settingsQuery.data.thinkingEnabled,
        thinkingBudget: settingsQuery.data.thinkingBudget ?? null,
        thinkingLevel: settingsQuery.data.thinkingLevel ?? null,
      })
    }
  }, [settingsQuery.data])

  const translateMutation = useMutation({
    mutationFn: () =>
      translatePrompt({
        content,
        model: config.model,
        temperature: config.temperature,
        thinkingMode: getThinkingMode(config),
        thinkingBudget: config.thinkingEnabled ? config.thinkingBudget : null,
        thinkingLevel: config.thinkingEnabled ? config.thinkingLevel : null,
      }),
    onSuccess: (result) => {
      setPreview(result.content)
      toast.success(`Traduzido — ${result.promptTokens} tokens entrada, ${result.candidateTokens} gerados.`)
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  })

  const handleApply = () => {
    if (preview) {
      onApply(preview)
      onClose()
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 pt-16">
      <div className="flex w-full max-w-6xl flex-col gap-5 rounded-xl border border-border bg-card p-6 shadow-xl">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-full bg-muted">
              <Languages className="h-4 w-4 text-primary" />
            </div>
            <div>
              <h2 className="text-sm font-semibold text-foreground">Traduzir para inglês</h2>
              <p className="text-xs text-subtle-foreground">
                O prompt atual será traduzido para inglês, preservando o original
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1.5 text-muted-foreground hover:bg-muted hover:text-foreground"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="rounded-lg border border-secondary bg-background p-4">
          <AiModelConfig value={config} onChange={setConfig} compact />
        </div>

        {!preview ? (
          <div className="flex flex-col gap-1.5">
            <p className="text-xs font-medium uppercase tracking-wide text-subtle-foreground">
              Conteúdo atual · {content.length} caracteres
            </p>
            <div className="max-h-48 overflow-y-auto rounded-lg border border-secondary bg-card p-4">
              <pre className="whitespace-pre-wrap text-xs leading-relaxed text-muted-foreground">
                {content.length > 600 ? `${content.slice(0, 600)}\n…` : content}
              </pre>
            </div>
          </div>
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            <div className="flex min-w-0 flex-col gap-1.5">
              <p className="text-xs font-medium uppercase tracking-wide text-subtle-foreground">
                Original
              </p>
              <div className="max-h-[55vh] overflow-y-auto rounded-lg border border-secondary bg-card p-4">
                <MarkdownContent content={content} />
              </div>
            </div>
            <div className="flex min-w-0 flex-col gap-1.5">
              <div className="flex items-center justify-between gap-3">
                <p className="text-xs font-medium uppercase tracking-wide text-primary">
                  Tradução para inglês
                </p>
                <button
                  type="button"
                  onClick={() => setPreview(null)}
                  className="flex shrink-0 items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
                >
                  <RotateCcw className="h-3 w-3" />
                  Tentar novamente
                </button>
              </div>
              <div className="max-h-[55vh] overflow-y-auto rounded-lg border border-success-soft bg-card p-4">
                <MarkdownContent content={preview} />
              </div>
            </div>
          </div>
        )}

        <div className="flex items-center justify-between gap-3">
          <p className="text-xs text-subtle-foreground">
            Revise antes de aplicar. Menções @arquivo serão revalidadas.
          </p>
          <div className="flex gap-2">
            <Button type="button" variant="secondary" onClick={onClose}>
              Cancelar
            </Button>
            {!preview ? (
              <Button
                type="button"
                onClick={() => translateMutation.mutate()}
                disabled={translateMutation.isPending || !content.trim()}
              >
                {translateMutation.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Languages className="h-4 w-4" />
                )}
                {translateMutation.isPending ? 'Traduzindo...' : 'Traduzir'}
              </Button>
            ) : (
              <Button type="button" onClick={handleApply}>
                Aplicar no editor
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
