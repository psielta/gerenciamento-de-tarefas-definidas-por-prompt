import { Link, Outlet, createFileRoute, useLocation, useRouterState } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Files, Loader2, List, Radio, Sparkles } from 'lucide-react'
import { useEffect } from 'react'
import { toast } from 'sonner'
import { getWorkingDirectory, updateWorkingDirectory } from '@/api/working-directories'
import { queryKeys } from '@/api/query-keys'
import { getErrorMessage } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { usePromptHub } from '@/realtime/prompt-hub'

const WORKSPACE_NAME_MAX_LENGTH = 48

function formatWorkspaceName(name?: string) {
  if (!name) {
    return ''
  }

  const normalizedName = name.trim()
  if (normalizedName.length <= WORKSPACE_NAME_MAX_LENGTH) {
    return normalizedName
  }

  return `${normalizedName.slice(0, WORKSPACE_NAME_MAX_LENGTH - 3).trimEnd()}...`
}

export const Route = createFileRoute('/workspaces/$workspaceId')({
  component: WorkspaceLayout,
})

function WorkspaceLayout() {
  const { workspaceId } = Route.useParams()
  const location = useLocation()
  // On the workspace page itself, "back" goes to the directories list; inside a
  // prompt (detail/new) it goes back to the open workspace.
  const isWorkspaceRoot = location.pathname.replace(/\/$/, '') === `/workspaces/${workspaceId}`
  const queryClient = useQueryClient()
  const hub = usePromptHub()
  const { joinWorkingDirectory, leaveWorkingDirectory } = hub
  const workspaceQuery = useQuery({
    queryKey: queryKeys.workingDirectories.detail(workspaceId),
    queryFn: () => getWorkingDirectory(workspaceId),
  })
  const workspaceName = formatWorkspaceName(workspaceQuery.data?.name)
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const isFilesRoute = pathname.includes(`/workspaces/${workspaceId}/files`)
  const isPromptsRoute = !isFilesRoute

  const aiContextMutation = useMutation({
    mutationFn: (enableAiContext: boolean) => {
      if (!workspaceQuery.data) {
        throw new Error('Diretorio de trabalho ainda nao carregado.')
      }

      return updateWorkingDirectory(workspaceId, {
        name: workspaceQuery.data.name,
        absolutePath: workspaceQuery.data.absolutePath,
        respectGitignore: workspaceQuery.data.respectGitignore,
        enableAiContext,
      })
    },
    onSuccess: async (workspace) => {
      queryClient.setQueryData(queryKeys.workingDirectories.detail(workspaceId), workspace)
      await queryClient.invalidateQueries({ queryKey: queryKeys.workingDirectories.all })
      toast.success('Configuracao de contexto de IA atualizada.')
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  useEffect(() => {
    joinWorkingDirectory(workspaceId)
    return () => leaveWorkingDirectory(workspaceId)
  }, [joinWorkingDirectory, leaveWorkingDirectory, workspaceId])

  return (
    <div className="grid gap-5">
      <div className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          {isWorkspaceRoot ? (
            <Link to="/workspaces">
              <Button type="button" variant="ghost" size="sm" className="-ml-2 mb-2">
                <ArrowLeft className="h-4 w-4" />
                Diretorios
              </Button>
            </Link>
          ) : (
            <Link to="/workspaces/$workspaceId" params={{ workspaceId }}>
              <Button type="button" variant="ghost" size="sm" className="-ml-2 mb-2 max-w-[16rem]">
                <ArrowLeft className="h-4 w-4 shrink-0" />
                <span className="truncate" title={workspaceQuery.data?.name}>
                  {workspaceName || 'Voltar'}
                </span>
              </Button>
            </Link>
          )}
          {workspaceQuery.isLoading ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Carregando diretorio
            </div>
          ) : (
            <>
              <h1 className="truncate text-2xl font-semibold text-foreground" title={workspaceQuery.data?.name}>
                {workspaceName}
              </h1>
              <p className="mt-1 truncate text-sm text-muted-foreground">{workspaceQuery.data?.absolutePath}</p>
            </>
          )}
        </div>
        <div className="flex items-center gap-2 rounded-md border border-border px-2.5 py-1.5 text-xs text-muted-foreground">
          <Radio className={hub.connected ? 'h-3.5 w-3.5 text-success-foreground' : 'h-3.5 w-3.5 text-destructive'} />
          {hub.connected ? 'Tempo real ativo' : 'Reconectando'}
        </div>
      </div>
      {workspaceQuery.data ? (
        <div className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex min-w-0 items-start gap-3">
            <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-md bg-muted">
              <Sparkles className="h-4 w-4 text-primary" />
            </div>
            <div className="min-w-0">
              <h2 className="text-sm font-semibold text-foreground">Contexto de IA</h2>
              <p className="mt-1 text-sm text-muted-foreground">
                README.md, CLAUDE.md e AGENT.md entram nas instrucoes do Gemini.
              </p>
            </div>
          </div>
          <Switch
            id="workspace-ai-context"
            checked={workspaceQuery.data.enableAiContext}
            disabled={aiContextMutation.isPending}
            onChange={(event) => aiContextMutation.mutate(event.target.checked)}
            label={workspaceQuery.data.enableAiContext ? 'Ativo' : 'Inativo'}
          />
        </div>
      ) : null}
      <nav className="flex flex-wrap gap-2 rounded-lg border border-border bg-card p-2">
        <Link to="/workspaces/$workspaceId" params={{ workspaceId }}>
          <Button type="button" variant={isPromptsRoute ? 'default' : 'ghost'} size="sm">
            <List className="h-4 w-4" />
            Prompts
          </Button>
        </Link>
        <Link to="/workspaces/$workspaceId/files" params={{ workspaceId }}>
          <Button type="button" variant={isFilesRoute ? 'default' : 'ghost'} size="sm">
            <Files className="h-4 w-4" />
            Arquivos
          </Button>
        </Link>
      </nav>
      <Outlet />
    </div>
  )
}
