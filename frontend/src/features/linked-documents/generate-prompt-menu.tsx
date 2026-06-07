import { autoUpdate, computePosition, flip, offset, shift } from '@floating-ui/dom'
import { useQuery } from '@tanstack/react-query'
import { ChevronDown, Loader2, Sparkles } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { getErrorMessage } from '@/api/client'
import { listPromptTemplates } from '@/api/prompt-templates'
import { queryKeys } from '@/api/query-keys'
import type { PromptTemplate } from '@/api/schemas'
import { Button } from '@/components/ui/button'
import { GeneratePromptDrawer } from './generate-prompt-drawer'

type GeneratePromptMenuProps = {
  linkedDocumentId: string
  disabled?: boolean
  pullRequestReference?: string | null
  // Quando fornecido (ex.: card do quadro), o menu apenas borbulha o template escolhido
  // e nao renderiza o drawer inline — quem renderiza e o componente pai (drawer elevado).
  onSelectTemplate?: (template: PromptTemplate) => void
}

export function GeneratePromptMenu({
  linkedDocumentId,
  disabled,
  pullRequestReference,
  onSelectTemplate,
}: GeneratePromptMenuProps) {
  const buttonRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const [open, setOpen] = useState(false)
  const [menuPosition, setMenuPosition] = useState({ left: 0, top: 0 })
  const [selectedTemplate, setSelectedTemplate] = useState<PromptTemplate | null>(null)

  const templatesQuery = useQuery({
    queryKey: queryKeys.promptTemplates.all,
    queryFn: listPromptTemplates,
  })

  useEffect(() => {
    if (!open || !buttonRef.current || !menuRef.current) {
      return
    }

    const button = buttonRef.current
    const menu = menuRef.current
    const update = () =>
      computePosition(button, menu, {
        placement: 'bottom-end',
        strategy: 'fixed',
        middleware: [offset(6), flip(), shift({ padding: 8 })],
      }).then(({ x, y }) => setMenuPosition({ left: x, top: y }))

    const cleanup = autoUpdate(button, menu, update)
    update()
    return cleanup
  }, [open, templatesQuery.data])

  useEffect(() => {
    if (!open) {
      return
    }

    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node
      if (buttonRef.current?.contains(target) || menuRef.current?.contains(target)) {
        return
      }

      setOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
      }
    }
    const onScrollOrResize = () => setOpen(false)

    window.addEventListener('pointerdown', onPointerDown, true)
    window.addEventListener('keydown', onKeyDown)
    window.addEventListener('scroll', onScrollOrResize, true)
    window.addEventListener('resize', onScrollOrResize)
    return () => {
      window.removeEventListener('pointerdown', onPointerDown, true)
      window.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('scroll', onScrollOrResize, true)
      window.removeEventListener('resize', onScrollOrResize)
    }
  }, [open])

  const isDisabled = disabled || templatesQuery.isLoading

  return (
    <>
      <Button
        ref={buttonRef}
        type="button"
        variant="secondary"
        size="sm"
        className="min-w-[9rem] border-warning-solid bg-warning-soft font-semibold text-warning-foreground ring-2 ring-warning-solid/25 hover:bg-warning-soft/80 data-[state=open]:ring-warning-solid/40"
        data-state={open ? 'open' : 'closed'}
        onClick={() => setOpen((current) => !current)}
        disabled={isDisabled}
        aria-haspopup="menu"
        aria-expanded={open}
      >
        {templatesQuery.isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
        Gerar prompt filho
        <ChevronDown className={`h-3.5 w-3.5 transition-transform ${open ? 'rotate-180' : ''}`} />
      </Button>

      {open
        ? createPortal(
            <div
              ref={menuRef}
              role="menu"
              className="fixed z-50 grid w-64 gap-1 rounded-md border border-border bg-card p-1 shadow-xl"
              style={{ left: menuPosition.left, top: menuPosition.top }}
            >
              {templatesQuery.error ? (
                <div className="p-2 text-xs text-danger-soft-foreground">{getErrorMessage(templatesQuery.error)}</div>
              ) : null}

              {templatesQuery.data?.map((template) => (
                <button
                  key={template.key}
                  type="button"
                  role="menuitem"
                  className="grid min-w-0 gap-0.5 rounded-md px-2 py-1.5 text-left text-sm text-foreground hover:bg-muted focus:bg-muted focus:outline-none"
                  onClick={() => {
                    setOpen(false)
                    if (onSelectTemplate) {
                      onSelectTemplate(template)
                    } else {
                      setSelectedTemplate(template)
                    }
                  }}
                >
                  <span className="truncate font-medium">{template.displayName}</span>
                  <span className="truncate text-xs text-muted-foreground">{template.description}</span>
                </button>
              ))}
            </div>,
            document.body,
          )
        : null}

      {selectedTemplate ? (
        <GeneratePromptDrawer
          linkedDocumentId={linkedDocumentId}
          template={selectedTemplate}
          initialPullRequestReference={pullRequestReference}
          onClose={() => setSelectedTemplate(null)}
        />
      ) : null}
    </>
  )
}
