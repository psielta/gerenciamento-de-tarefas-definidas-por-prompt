import { autoUpdate, computePosition, flip, offset, shift } from '@floating-ui/dom'
import { Bot, ChevronDown, Code2, Loader2, Sparkles } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import type { TerminalAgentLaunch } from '@/api/schemas'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const AGENT_OPTIONS: Array<{
  id: TerminalAgentLaunch
  label: string
  description: string
  command: string
  icon: typeof Bot
}> = [
  {
    id: 'Claude',
    label: 'Claude',
    description: 'Inicia Claude Code com esforco maximo',
    command: 'claude --dangerously-skip-permissions --effort max',
    icon: Bot,
  },
  {
    id: 'Codex',
    label: 'Codex',
    description: 'Inicia Codex em modo yolo',
    command: 'codex --yolo',
    icon: Code2,
  },
  {
    id: 'Grok',
    label: 'Grok',
    description: 'Inicia Grok com always-approve',
    command: 'grok --always-approve',
    icon: Sparkles,
  },
]

type TerminalAgentMenuProps = {
  disabled?: boolean
  onSelectAgent: (agent: TerminalAgentLaunch) => void
}

export function TerminalAgentMenu({ disabled, onSelectAgent }: TerminalAgentMenuProps) {
  const buttonRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const [open, setOpen] = useState(false)
  const [menuPosition, setMenuPosition] = useState({ left: 0, top: 0 })

  useEffect(() => {
    if (!open || !buttonRef.current || !menuRef.current) {
      return
    }

    const button = buttonRef.current
    const menu = menuRef.current
    const update = () =>
      computePosition(button, menu, {
        placement: 'bottom-start',
        strategy: 'fixed',
        middleware: [offset(6), flip(), shift({ padding: 8 })],
      }).then(({ x, y }) => setMenuPosition({ left: x, top: y }))

    const cleanup = autoUpdate(button, menu, update)
    update()
    return cleanup
  }, [open])

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

    window.addEventListener('pointerdown', onPointerDown, true)
    window.addEventListener('keydown', onKeyDown)
    return () => {
      window.removeEventListener('pointerdown', onPointerDown, true)
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  return (
    <>
      <Button
        ref={buttonRef}
        type="button"
        size="sm"
        variant="secondary"
        className="rounded-l-none border-l border-border px-2"
        disabled={disabled}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Abrir terminal com agente"
        title="Abrir terminal com agente"
        onClick={() => setOpen((current) => !current)}
      >
        {disabled ? <Loader2 className="h-4 w-4 animate-spin" /> : <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} />}
      </Button>

      {open
        ? createPortal(
            <div
              ref={menuRef}
              role="menu"
              className="fixed z-50 grid w-72 gap-1 rounded-md border border-border bg-card p-1 shadow-xl"
              style={{ left: menuPosition.left, top: menuPosition.top }}
            >
              {AGENT_OPTIONS.map((option) => {
                const Icon = option.icon
                return (
                  <button
                    key={option.id}
                    type="button"
                    role="menuitem"
                    className="grid gap-0.5 rounded-md px-2 py-2 text-left transition-colors hover:bg-secondary"
                    onClick={() => {
                      onSelectAgent(option.id)
                      setOpen(false)
                    }}
                  >
                    <span className="flex items-center gap-2 text-sm font-medium text-foreground">
                      <Icon className="h-4 w-4 text-primary" />
                      {option.label}
                    </span>
                    <span className="pl-6 text-xs text-muted-foreground">{option.description}</span>
                    <span className="truncate pl-6 font-mono text-[0.65rem] text-muted-foreground" title={option.command}>
                      {option.command}
                    </span>
                  </button>
                )
              })}
            </div>,
            document.body,
          )
        : null}
    </>
  )
}