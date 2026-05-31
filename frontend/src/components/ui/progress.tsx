import { cva, type VariantProps } from 'class-variance-authority'
import type * as React from 'react'
import { cn } from '@/lib/utils'

const progressFillVariants = cva('h-full rounded-full transition-[width,background-color]', {
  variants: {
    variant: {
      ok: 'bg-[#254632]',
      warn: 'bg-[#c88712]',
      crit: 'bg-[#b42318]',
      muted: 'bg-[#9aa69d]',
    },
  },
  defaultVariants: {
    variant: 'ok',
  },
})

export type ProgressProps = React.HTMLAttributes<HTMLDivElement> &
  VariantProps<typeof progressFillVariants> & {
    value: number
  }

export function Progress({ value, variant, className, ...props }: ProgressProps) {
  const normalized = Math.min(100, Math.max(0, Number.isFinite(value) ? value : 0))

  return (
    <div
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={Math.round(normalized)}
      className={cn('h-1.5 overflow-hidden rounded-full bg-[#e7ece6]', className)}
      {...props}
    >
      <div className={progressFillVariants({ variant })} style={{ width: `${normalized}%` }} />
    </div>
  )
}
