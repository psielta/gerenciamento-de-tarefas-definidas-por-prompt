import '@excalidraw/excalidraw/index.css'
import { Excalidraw, serializeAsJSON } from '@excalidraw/excalidraw'
import { useCallback, useEffect, useRef, useState, type ComponentType } from 'react'
import { useTheme } from '@/components/theme/theme-provider'

type ExcalidrawEditorProps = {
  value: string
  onChange: (content: string) => void
}

// Excalidraw ships very large, version-specific types. We only need a tiny slice
// of its surface here, so wrap the component in a permissive type to keep the
// editor decoupled and the build stable across Excalidraw upgrades.
type ExcalidrawCanvasProps = {
  initialData?: unknown
  onChange?: (elements: readonly unknown[], appState: unknown, files: unknown) => void
  theme?: 'light' | 'dark'
}
const ExcalidrawCanvas = Excalidraw as unknown as ComponentType<ExcalidrawCanvasProps>

function parseInitialData(value: string): ExcalidrawCanvasProps['initialData'] {
  if (!value.trim()) {
    return undefined
  }
  try {
    const parsed = JSON.parse(value) as { elements?: unknown; appState?: unknown; files?: unknown }
    return {
      elements: Array.isArray(parsed.elements) ? parsed.elements : [],
      // scrollToContent centers the saved scene when the diagram is reopened.
      appState: { ...(typeof parsed.appState === 'object' && parsed.appState ? parsed.appState : {}), collaborators: undefined },
      files: parsed.files ?? {},
      scrollToContent: true,
    }
  } catch {
    return undefined
  }
}

export default function ExcalidrawEditor({ value, onChange }: ExcalidrawEditorProps) {
  const { resolvedTheme } = useTheme()
  // Compute the initial scene exactly once; Excalidraw is uncontrolled afterwards.
  const [initialData] = useState(() => parseInitialData(value))
  // Excalidraw fires onChange once right after mount; skip it so merely opening a
  // diagram does not mark it dirty and trigger an autosave.
  const skipFirstChange = useRef(true)
  const debounceRef = useRef<number | undefined>(undefined)

  useEffect(() => () => window.clearTimeout(debounceRef.current), [])

  const handleChange = useCallback(
    (elements: readonly unknown[], appState: unknown, files: unknown) => {
      if (skipFirstChange.current) {
        skipFirstChange.current = false
        return
      }
      window.clearTimeout(debounceRef.current)
      debounceRef.current = window.setTimeout(() => {
        onChange(serializeAsJSON(elements as never, appState as never, files as never, 'local'))
      }, 400)
    },
    [onChange],
  )

  return (
    <div className="min-h-[20rem] flex-1 overflow-hidden rounded-md border border-border">
      <ExcalidrawCanvas
        initialData={initialData}
        onChange={handleChange}
        theme={resolvedTheme === 'dark' ? 'dark' : 'light'}
      />
    </div>
  )
}
