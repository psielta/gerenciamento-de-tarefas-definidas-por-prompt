import { FileText, Loader2, Search } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useDebouncedValue } from '@/hooks/use-debounced-value'
import { cn } from '@/lib/utils'
import { useFileSearch } from './use-file-queries'

type FileSearchPaletteProps = {
  workingDirectoryId: string
  onSelectFile: (relativePath: string) => void
  onClose: () => void
}

/**
 * Paleta de busca rapida estilo cmdk para abrir arquivos do workspace pelo
 * nome/caminho. Navegacao por teclado: setas movem a selecao e Enter abre o
 * arquivo. O fechamento por Escape fica a cargo de quem monta a paleta.
 */
export function FileSearchPalette({ workingDirectoryId, onSelectFile, onClose }: FileSearchPaletteProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLUListElement>(null)
  const [query, setQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(0)
  const debouncedQuery = useDebouncedValue(query.trim(), 250)
  const isSearching = debouncedQuery.length >= 2
  const searchQuery = useFileSearch(workingDirectoryId, debouncedQuery, isSearching)

  const results = useMemo(
    () => (searchQuery.data ?? []).filter((result) => !result.isDirectory),
    [searchQuery.data],
  )
  // O indice e reiniciado ao digitar; o clamp cobre resultados que encolhem.
  const highlightedIndex = Math.min(activeIndex, Math.max(results.length - 1, 0))

  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  useEffect(() => {
    const active = listRef.current?.querySelector<HTMLElement>(`[data-index="${highlightedIndex}"]`)
    // jsdom nao implementa scrollIntoView; a chamada opcional evita quebrar testes.
    active?.scrollIntoView?.({ block: 'nearest' })
  }, [highlightedIndex])

  const openResult = (relativePath: string) => {
    onSelectFile(relativePath)
    onClose()
  }

  const handleInputKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setActiveIndex(Math.min(highlightedIndex + 1, Math.max(results.length - 1, 0)))
      return
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActiveIndex(Math.max(highlightedIndex - 1, 0))
      return
    }

    if (event.key === 'Enter') {
      event.preventDefault()
      const active = results[highlightedIndex]
      if (active) {
        openResult(active.relativePath)
      }
    }
  }

  return (
    <div
      className="fixed inset-0 z-[60] flex items-start justify-center bg-black/50 p-4 pt-[12vh] backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-label="Buscar arquivos no workspace"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
    >
      <div className="grid w-full max-w-xl grid-rows-[auto_minmax(0,1fr)] overflow-hidden rounded-lg border border-border bg-card shadow-2xl">
        <div className="relative border-b border-border">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={(event) => {
              setQuery(event.target.value)
              setActiveIndex(0)
            }}
            onKeyDown={handleInputKeyDown}
            placeholder="Buscar arquivos por nome ou caminho"
            aria-label="Buscar arquivos por nome ou caminho"
            className="h-11 w-full bg-transparent pl-9 pr-3 text-sm text-foreground outline-none placeholder:text-muted-foreground"
          />
        </div>

        <div className="max-h-[50vh] overflow-y-auto p-1">
          {!isSearching ? (
            <p className="px-3 py-4 text-center text-xs text-muted-foreground">
              Digite pelo menos 2 caracteres para buscar.
            </p>
          ) : null}

          {isSearching && searchQuery.isLoading ? (
            <div className="flex items-center justify-center gap-2 px-3 py-4 text-xs text-muted-foreground">
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
              Buscando arquivos
            </div>
          ) : null}

          {isSearching && searchQuery.isError ? (
            <p className="px-3 py-4 text-center text-xs text-destructive">Nao foi possivel buscar os arquivos.</p>
          ) : null}

          {isSearching && !searchQuery.isLoading && !searchQuery.isError && !results.length ? (
            <p className="px-3 py-4 text-center text-xs text-muted-foreground">Nenhum arquivo encontrado.</p>
          ) : null}

          {results.length ? (
            <ul ref={listRef} className="grid gap-0.5" role="listbox" aria-label="Resultados da busca">
              {results.map((result, index) => (
                <li key={result.relativePath}>
                  <button
                    type="button"
                    data-index={index}
                    role="option"
                    aria-selected={index === highlightedIndex}
                    onMouseEnter={() => setActiveIndex(index)}
                    onClick={() => openResult(result.relativePath)}
                    className={cn(
                      'flex w-full min-w-0 items-center gap-2 rounded-md px-2.5 py-1.5 text-left text-xs transition-colors hover:bg-muted',
                      index === highlightedIndex && 'bg-accent text-foreground',
                    )}
                    title={result.relativePath}
                  >
                    <FileText className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                    <span className="flex min-w-0 flex-col">
                      <span className="truncate font-mono">{result.fileName}</span>
                      <span className="truncate text-[0.68rem] text-muted-foreground">{result.relativePath}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      </div>
    </div>
  )
}
