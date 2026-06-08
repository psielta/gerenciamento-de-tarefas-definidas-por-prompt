import { Link, Outlet, createFileRoute, useLocation, useRouterState } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Check, Files, ListTodo, Loader2, List, Pencil, Radio, Shapes, Sparkles, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { getWorkingDirectory, updateWorkingDirectory } from '@/api/working-directories'
import { queryKeys } from '@/api/query-keys'
import { getErrorMessage } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
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
  const isFutureTasksRoute = pathname.includes(`/workspaces/${workspaceId}/future-tasks`)
  const isDiagramsRoute = pathname.includes(`/workspaces/${workspaceId}/diagrams`)
  const isPromptsRoute = !isFilesRoute && !isFutureTasksRoute && !isDiagramsRoute
  const [isEditingName, setIsEditingName] = useState(false)
  const [nameDraft, setNameDraft] = useState('')

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

  const renameMutation = useMutation({
    mutationFn: (name: string) => {
      if (!workspaceQuery.data) {
        throw new Error('Diretorio de trabalho ainda nao carregado.')
      }

      return updateWorkingDirectory(workspaceId, {
        name,
        absolutePath: workspaceQuery.data.absolutePath,
        respectGitignore: workspaceQuery.data.respectGitignore,
        enableAiContext: workspaceQuery.data.enableAiContext,
        taskNumberPattern: workspaceQuery.data.taskNumberPattern,
      })
    },
    onSuccess: async (workspace) => {
      queryClient.setQueryData(queryKeys.workingDirectories.detail(workspaceId), workspace)
      await queryClient.invalidateQueries({ queryKey: queryKeys.workingDirectories.all })
      setIsEditingName(false)
      toast.success('Nome do diretorio atualizado.')
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const startEditingName = () => {
    setNameDraft(workspaceQuery.data?.name ?? '')
    setIsEditingName(true)
  }

  const cancelEditingName = () => {
    setIsEditingName(false)
  }

  const trimmedNameDraft = nameDraft.trim()
  const canSaveName =
    trimmedNameDraft.length >= 2 && trimmedNameDraft !== workspaceQuery.data?.name && !renameMutation.isPending

  const submitRename = () => {
    if (!canSaveName) {
      return
    }

    renameMutation.mutate(trimmedNameDraft)
  }

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
              {isEditingName ? (
                <div className="flex items-center gap-2">
                  <Input
                    value={nameDraft}
                    onChange={(event) => setNameDraft(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter') {
                        event.preventDefault()
                        submitRename()
                      } else if (event.key === 'Escape') {
                        event.preventDefault()
                        cancelEditingName()
                      }
                    }}
                    autoFocus
                    maxLength={160}
                    aria-label="Nome do diretorio"
                    className="h-9 max-w-sm text-lg font-semibold"
                  />
                  <Button
                    type="button"
                    size="icon"
                    className="h-9 w-9 shrink-0"
                    onClick={submitRename}
                    disabled={!canSaveName}
                    aria-label="Salvar nome"
                    title="Salvar"
                  >
                    {renameMutation.isPending ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Check className="h-4 w-4" />
                    )}
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-9 w-9 shrink-0"
                    onClick={cancelEditingName}
                    disabled={renameMutation.isPending}
                    aria-label="Cancelar edicao"
                    title="Cancelar"
                  >
                    <X className="h-4 w-4" />
                  </Button>
                </div>
              ) : (
                <div className="flex items-center gap-1.5">
                  <h1 className="truncate text-2xl font-semibold text-foreground" title={workspaceQuery.data?.name}>
                    {workspaceName}
                  </h1>
                  {workspaceQuery.data ? (
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8 shrink-0 text-muted-foreground"
                      onClick={startEditingName}
                      aria-label="Editar nome"
                      title="Editar nome"
                    >
                      <Pencil className="h-4 w-4" />
                    </Button>
                  ) : null}
                </div>
              )}
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
        <Link to="/workspaces/$workspaceId/future-tasks" params={{ workspaceId }}>
          <Button type="button" variant={isFutureTasksRoute ? 'default' : 'ghost'} size="sm">
            <ListTodo className="h-4 w-4" />
            Tarefas futuras
          </Button>
        </Link>
        <Link to="/workspaces/$workspaceId/files" params={{ workspaceId }}>
          <Button type="button" variant={isFilesRoute ? 'default' : 'ghost'} size="sm">
            <Files className="h-4 w-4" />
            Arquivos
          </Button>
        </Link>
        <Link to="/workspaces/$workspaceId/diagrams" params={{ workspaceId }}>
          <Button type="button" variant={isDiagramsRoute ? 'default' : 'ghost'} size="sm">
            <Shapes className="h-4 w-4" />
            Diagramas
          </Button>
        </Link>
      </nav>
      <Outlet />
    </div>
  )
}
