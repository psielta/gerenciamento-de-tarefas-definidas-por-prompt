import { Settings2, Terminal as TerminalIcon, X } from 'lucide-react'
import { useRef } from 'react'
import { Button } from '@/components/ui/button'
import { Popover } from '@/components/ui/popover'
import { cn } from '@/lib/utils'
import {
  resolveTerminalTabLabel,
  sanitizeTerminalTabName,
  TERMINAL_TAB_COLORS,
  TERMINAL_TAB_NAME_MAX_LENGTH,
  type TerminalTabPreference,
} from './terminal-tab-preferences'

type TerminalTabButtonProps = {
  index: number
  isActive: boolean
  preference?: TerminalTabPreference
  closeDisabled?: boolean
  onActivate: () => void
  onClose: () => void
  onPreferenceChange: (patch: TerminalTabPreference) => void
}

export function TerminalTabButton({
  index,
  isActive,
  preference,
  closeDisabled,
  onActivate,
  onClose,
  onPreferenceChange,
}: TerminalTabButtonProps) {
  const nameInputRef = useRef<HTMLInputElement>(null)
  const label = resolveTerminalTabLabel(preference, index)
  const accentColor = preference?.color ?? null

  const saveName = () => {
    const sanitized = sanitizeTerminalTabName(nameInputRef.current?.value ?? '')
    onPreferenceChange({ name: sanitized.length > 0 ? sanitized : undefined })
  }

  return (
    <div className="flex items-center gap-1">
      <Button
        type="button"
        size="sm"
        variant={isActive ? 'default' : 'secondary'}
        className={cn('relative gap-1.5 pl-2', accentColor && !isActive && 'border-transparent')}
        style={
          accentColor
            ? {
                boxShadow: `inset 3px 0 0 0 ${accentColor}`,
              }
            : undefined
        }
        onClick={onActivate}
        title={label}
      >
        <TerminalIcon className="h-4 w-4 shrink-0" />
        <span className="max-w-[9rem] truncate">{label}</span>
      </Button>

      <Popover
        ariaLabel={`Configurar aba ${label}`}
        triggerClassName="inline-flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
        trigger={<Settings2 className="h-3.5 w-3.5" />}
      >
        <div className="grid gap-3 p-0">
          <div className="grid gap-1.5">
            <label htmlFor={`terminal-tab-name-${index}`} className="text-xs font-medium text-foreground">
              Nome da aba
            </label>
            <input
              id={`terminal-tab-name-${index}`}
              ref={nameInputRef}
              type="text"
              defaultValue={preference?.name ?? ''}
              maxLength={TERMINAL_TAB_NAME_MAX_LENGTH}
              placeholder={`Terminal ${index + 1}`}
              className="h-9 rounded-md border border-border bg-background px-2 text-sm outline-none ring-primary focus:ring-2"
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.preventDefault()
                  saveName()
                }
              }}
            />
          </div>

          <div className="grid gap-1.5">
            <span className="text-xs font-medium text-foreground">Cor da aba</span>
            <div className="grid grid-cols-5 gap-1.5">
              {TERMINAL_TAB_COLORS.map((color) => (
                <button
                  key={color.id}
                  type="button"
                  title={color.label}
                  aria-label={color.label}
                  className={cn(
                    'h-7 rounded-md border border-border transition-transform hover:scale-105',
                    preference?.color === color.value || (!preference?.color && color.value === null)
                      ? 'ring-2 ring-primary ring-offset-1 ring-offset-card'
                      : '',
                  )}
                  style={{ backgroundColor: color.value ?? 'var(--secondary)' }}
                  onClick={() => onPreferenceChange({ color: color.value })}
                />
              ))}
            </div>
          </div>

          <Button type="button" size="sm" onClick={saveName}>
            Salvar nome
          </Button>
        </div>
      </Popover>

      <Button
        type="button"
        size="icon"
        variant="ghost"
        aria-label={`Fechar ${label}`}
        onClick={onClose}
        disabled={closeDisabled}
      >
        <X className="h-4 w-4" />
      </Button>
    </div>
  )
}