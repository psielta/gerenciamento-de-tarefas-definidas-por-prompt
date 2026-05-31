import { autoUpdate, computePosition, flip, offset, shift } from '@floating-ui/dom'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

type PopoverProps = {
  trigger: ReactNode
  children: ReactNode
  className?: string
  triggerClassName?: string
  ariaLabel?: string
}

export function Popover({ trigger, children, className, triggerClassName, ariaLabel }: PopoverProps) {
  const [open, setOpen] = useState(false)
  const referenceRef = useRef<HTMLButtonElement | null>(null)
  const floatingRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!open || !referenceRef.current || !floatingRef.current) {
      return
    }

    const update = () => {
      if (!referenceRef.current || !floatingRef.current) {
        return
      }

      void computePosition(referenceRef.current, floatingRef.current, {
        placement: 'bottom-end',
        middleware: [offset(8), flip(), shift({ padding: 8 })],
      }).then(({ x, y }) => {
        if (floatingRef.current) {
          Object.assign(floatingRef.current.style, {
            left: `${x}px`,
            top: `${y}px`,
          })
        }
      })
    }

    const cleanup = autoUpdate(referenceRef.current, floatingRef.current, update, {
      elementResize: typeof ResizeObserver !== 'undefined',
    })

    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node | null
      if (
        target &&
        !referenceRef.current?.contains(target) &&
        !floatingRef.current?.contains(target)
      ) {
        setOpen(false)
      }
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
      }
    }

    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)

    return () => {
      cleanup()
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  return (
    <>
      <button
        ref={referenceRef}
        type="button"
        aria-label={ariaLabel}
        aria-expanded={open}
        className={cn('text-left', triggerClassName)}
        onClick={() => setOpen((value) => !value)}
      >
        {trigger}
      </button>
      {open ? (
        <div
          ref={floatingRef}
          className={cn(
            'fixed z-50 w-[min(24rem,calc(100vw-1rem))] rounded-md border border-[#d9dfd5] bg-white p-3 shadow-xl',
            className,
          )}
        >
          {children}
        </div>
      ) : null}
    </>
  )
}
