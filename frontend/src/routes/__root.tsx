import { Link, Outlet, createRootRoute } from '@tanstack/react-router'
import { ThemeToggle } from '@/components/theme/theme-toggle'
import { ThothLogo } from '@/components/thoth-logo'
import { UsageIndicator } from '@/features/agent-usage/usage-indicator'
import { GlobalNewPromptButton } from '@/features/prompts/global-new-prompt-button'
import { GlobalTerminalsButton } from '@/features/terminals/global-terminals-button'

export const Route = createRootRoute({
  component: RootLayout,
})

function RootLayout() {
  return (
    <div className="min-h-svh bg-background text-foreground">
      <header className="border-b border-border bg-card">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3">
          <Link
            to="/"
            className="group flex min-w-0 items-center gap-2.5 text-sm font-semibold text-foreground"
          >
            <ThothLogo className="h-8 w-8 shrink-0 text-primary transition-transform duration-150 group-hover:scale-105" />
            <span className="flex items-center gap-1 truncate">
              <span className="truncate tracking-tight">Thoth</span>
              <span aria-hidden className="logo-caret h-3.5 w-[2px] shrink-0 rounded-[1px] bg-primary" />
            </span>
          </Link>
          <nav className="flex items-center gap-1 text-sm">
            <Link
              to="/"
              activeOptions={{ exact: true }}
              className="rounded-md px-2.5 py-1.5 text-foreground transition-colors hover:bg-accent [&.active]:bg-accent [&.active]:font-semibold"
            >
              Quadro
            </Link>
            <Link
              to="/workspaces"
              className="rounded-md px-2.5 py-1.5 text-foreground transition-colors hover:bg-accent [&.active]:bg-accent [&.active]:font-semibold"
            >
              Diretórios
            </Link>
            <Link
              to="/files"
              className="rounded-md px-2.5 py-1.5 text-foreground transition-colors hover:bg-accent [&.active]:bg-accent [&.active]:font-semibold"
            >
              Arquivos
            </Link>
            <Link
              to="/notas"
              className="rounded-md px-2.5 py-1.5 text-foreground transition-colors hover:bg-accent [&.active]:bg-accent [&.active]:font-semibold"
            >
              Notas
            </Link>
            <Link
              to="/diagramas"
              className="rounded-md px-2.5 py-1.5 text-foreground transition-colors hover:bg-accent [&.active]:bg-accent [&.active]:font-semibold"
            >
              Diagramas
            </Link>
            <Link
              to="/terminais"
              className="rounded-md px-2.5 py-1.5 text-foreground transition-colors hover:bg-accent [&.active]:bg-accent [&.active]:font-semibold"
            >
              Terminais
            </Link>
          </nav>
          <div className="flex items-center gap-2">
            <ThemeToggle />
            <UsageIndicator />
          </div>
        </div>
      </header>
      <main className="mx-auto grid min-w-0 w-full max-w-7xl gap-6 px-4 py-6">
        <Outlet />
      </main>
      <GlobalNewPromptButton />
      <GlobalTerminalsButton />
    </div>
  )
}
