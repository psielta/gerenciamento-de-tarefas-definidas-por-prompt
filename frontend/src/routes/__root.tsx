import { Link, Outlet, createRootRoute } from '@tanstack/react-router'
import { FolderKanban } from 'lucide-react'
import { UsageIndicator } from '@/features/agent-usage/usage-indicator'

export const Route = createRootRoute({
  component: RootLayout,
})

function RootLayout() {
  return (
    <div className="min-h-svh bg-[#f7f8f6] text-[#172126]">
      <header className="border-b border-[#d9dfd5] bg-white">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3">
          <Link to="/" className="flex min-w-0 items-center gap-2 text-sm font-semibold text-[#172126]">
            <FolderKanban className="h-5 w-5 shrink-0 text-[#254632]" />
            <span className="truncate">Prompt Tasks</span>
          </Link>
          <nav className="flex items-center gap-1 text-sm">
            <Link
              to="/"
              activeOptions={{ exact: true }}
              className="rounded-md px-2.5 py-1.5 text-[#253035] transition-colors hover:bg-[#e7ece6] [&.active]:bg-[#e7ece6] [&.active]:font-semibold"
            >
              Quadro
            </Link>
            <Link
              to="/workspaces"
              className="rounded-md px-2.5 py-1.5 text-[#253035] transition-colors hover:bg-[#e7ece6] [&.active]:bg-[#e7ece6] [&.active]:font-semibold"
            >
              Diretórios
            </Link>
          </nav>
          <UsageIndicator />
        </div>
      </header>
      <main className="mx-auto grid w-full max-w-7xl gap-6 px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
