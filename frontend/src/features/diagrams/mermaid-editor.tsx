import { AlertCircle } from 'lucide-react'
import mermaid from 'mermaid'
import { useEffect, useId, useState } from 'react'
import { useTheme } from '@/components/theme/theme-provider'
import { Textarea } from '@/components/ui/textarea'

type MermaidEditorProps = {
  value: string
  onChange: (content: string) => void
}

export default function MermaidEditor({ value, onChange }: MermaidEditorProps) {
  const { resolvedTheme } = useTheme()
  // useId() yields colons that are invalid in a DOM id selector, so strip them.
  const renderId = `mermaid-${useId().replace(/[^a-zA-Z0-9-]/g, '')}`
  const [svg, setSvg] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    // Render securely: never execute click handlers or scripts embedded in the
    // diagram source, and render on demand instead of scanning the document.
    mermaid.initialize({
      startOnLoad: false,
      securityLevel: 'strict',
      theme: resolvedTheme === 'dark' ? 'dark' : 'default',
    })

    const code = value.trim()

    // Debounce rendering; every state update happens inside this async callback
    // so opening or typing never triggers a synchronous setState in the effect.
    const handle = window.setTimeout(() => {
      if (cancelled) {
        return
      }
      if (!code) {
        setSvg('')
        setError(null)
        return
      }

      // Clear any orphan node a previous failed render may have left behind.
      document.getElementById(renderId)?.remove()
      document.getElementById(`d${renderId}`)?.remove()

      mermaid
        .render(renderId, code)
        .then((result) => {
          if (!cancelled) {
            setSvg(result.svg)
            setError(null)
          }
        })
        .catch((renderError: unknown) => {
          if (!cancelled) {
            setSvg('')
            setError(renderError instanceof Error ? renderError.message : 'Codigo Mermaid invalido.')
          }
        })
    }, 300)

    return () => {
      cancelled = true
      window.clearTimeout(handle)
    }
  }, [value, resolvedTheme, renderId])

  return (
    <div className="grid min-h-[20rem] flex-1 gap-3 lg:grid-cols-2">
      <Textarea
        value={value}
        onChange={(event) => onChange(event.target.value)}
        spellCheck={false}
        placeholder={'flowchart TD\n    A[Inicio] --> B[Fim]'}
        className="min-h-[16rem] flex-1 resize-none font-mono text-xs leading-relaxed"
        aria-label="Codigo Mermaid"
      />
      <div className="min-h-[16rem] overflow-auto rounded-md border border-border bg-card p-3">
        {error ? (
          <div className="flex items-start gap-2 text-sm text-destructive">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <pre className="whitespace-pre-wrap break-words font-mono text-xs">{error}</pre>
          </div>
        ) : svg ? (
          // mermaid output is sanitized via securityLevel: 'strict' before it is injected here
          <div className="flex justify-center [&_svg]:max-w-full" dangerouslySetInnerHTML={{ __html: svg }} />
        ) : (
          <p className="text-sm text-muted-foreground">O preview do diagrama aparece aqui.</p>
        )}
      </div>
    </div>
  )
}
