import { Link, Outlet, createRootRoute } from '@tanstack/react-router'
import { Terminal } from 'lucide-react'
import { ThemeToggle } from '@/components/theme/theme-toggle'
import { UsageIndicator } from '@/features/agent-usage/usage-indicator'

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
            <span className="logo-badge flex h-7 w-7 shrink-0 items-center justify-center rounded-lg shadow-sm ring-1 ring-inset ring-white/15 transition-transform duration-150 group-hover:scale-105">
              <Terminal className="h-4 w-4 text-primary-foreground" strokeWidth={2.75} />
            </span>
            <span className="flex items-center gap-1 truncate">
              <span className="truncate tracking-tight">Prompt Tasks</span>
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
    </div>
  )
}
