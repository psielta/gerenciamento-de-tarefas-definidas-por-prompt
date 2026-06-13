import { useQuery } from '@tanstack/react-query'
import { Terminal } from 'lucide-react'
import { useState } from 'react'
import { queryKeys } from '@/api/query-keys'
import { getTerminalCapabilities } from '@/api/terminals'
import { Button } from '@/components/ui/button'
import { GenericTerminalsDrawer } from './generic-terminals-drawer'

/**
 * Floating action button (stacked above the "Novo prompt" button) that opens a
 * drawer of generic terminals. Only rendered when terminals are enabled for
 * this instance.
 */
export function GlobalTerminalsButton() {
  const [open, setOpen] = useState(false)

  const capabilitiesQuery = useQuery({
    queryKey: queryKeys.terminals.capabilities(),
    queryFn: getTerminalCapabilities,
  })

  if (!capabilitiesQuery.data?.enabled) {
    return null
  }

  return (
    <>
      {!open ? (
        <Button
          type="button"
          size="icon"
          onClick={() => setOpen(true)}
          title="Terminais"
          aria-label="Terminais"
          className="fixed bottom-[4.75rem] right-5 z-40 h-12 w-12 rounded-full shadow-lg sm:bottom-[5.25rem] sm:right-6"
        >
          <Terminal className="h-5 w-5" />
        </Button>
      ) : null}

      {open ? <GenericTerminalsDrawer onClose={() => setOpen(false)} /> : null}
    </>
  )
}
