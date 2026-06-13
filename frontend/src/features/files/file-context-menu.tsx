import { GitBranch } from 'lucide-react'
import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

type FileContextMenuProps = {
  children: ReactNode
  onShowGitHistory: () => void
}

type MenuPosition = {
  x: number
  y: number
}

const MENU_WIDTH = 220
const MENU_HEIGHT = 44

export function FileContextMenu({ children, onShowGitHistory }: FileContextMenuProps) {
  const [open, setOpen] = useState(false)
  const [position, setPosition] = useState<MenuPosition>({ x: 0, y: 0 })
  const menuRef = useRef<HTMLDivElement>(null)

  const closeMenu = useCallback(() => {
    setOpen(false)
  }, [])

  const handleContextMenu = (event: React.MouseEvent) => {
    event.preventDefault()
    setPosition({ x: event.clientX, y: event.clientY })
    setOpen(true)
  }

  useLayoutEffect(() => {
    if (!open || !menuRef.current) {
      return
    }

    const rect = menuRef.current.getBoundingClientRect()
    const maxX = window.innerWidth - rect.width - 8
    const maxY = window.innerHeight - rect.height - 8
    setPosition((current) => ({
      x: Math.max(8, Math.min(current.x, maxX)),
      y: Math.max(8, Math.min(current.y, maxY)),
    }))
  }, [open])

  useEffect(() => {
    if (!open) {
      return
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        closeMenu()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [closeMenu, open])

  return (
    <>
      <div onContextMenu={handleContextMenu}>{children}</div>

      {open ? (
        <>
          <button
            type="button"
            aria-label="Fechar menu de contexto"
            className="fixed inset-0 z-[90] cursor-default bg-transparent"
            onClick={closeMenu}
          />
          <div
            ref={menuRef}
            role="menu"
            className="fixed z-[91] min-w-[13.75rem] overflow-hidden rounded-md border border-border bg-card py-1 shadow-lg"
            style={{ left: position.x, top: position.y, width: MENU_WIDTH, minHeight: MENU_HEIGHT }}
          >
            <button
              type="button"
              role="menuitem"
              className={cn(
                'flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-foreground transition-colors hover:bg-muted',
              )}
              onClick={() => {
                onShowGitHistory()
                closeMenu()
              }}
            >
              <GitBranch className="h-4 w-4 shrink-0 text-muted-foreground" />
              Ver historico do git
            </button>
          </div>
        </>
      ) : null}
    </>
  )
}