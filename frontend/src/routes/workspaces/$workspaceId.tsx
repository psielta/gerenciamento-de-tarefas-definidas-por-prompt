import { Link, Outlet, createFileRoute } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Loader2, Radio, Sparkles } from 'lucide-react'
import { useEffect } from 'react'
import { toast } from 'sonner'
import { getWorkingDirectory, updateWorkingDirectory } from '@/api/working-directories'
import { queryKeys } from '@/api/query-keys'
import { getErrorMessage } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { usePromptHub } from '@/realtime/prompt-hub'

export const Route = createFileRoute('/workspaces/$workspaceId')({
  component: WorkspaceLayout,
})

function WorkspaceLayout() {
  const { workspaceId } = Route.useParams()
  const queryClient = useQueryClient()
  const hub = usePromptHub()
  const { joinWorkingDirectory, leaveWorkingDirectory } = hub
  const workspaceQuery = useQuery({
    queryKey: queryKeys.workingDirectories.detail(workspaceId),
    queryFn: () => getWorkingDirectory(workspaceId),
  })

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
      <div className="flex flex-col gap-3 rounded-lg border border-[#d9dfd5] bg-white p-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <Link to="/workspaces">
            <Button type="button" variant="ghost" size="sm" className="-ml-2 mb-2">
              <ArrowLeft className="h-4 w-4" />
              Diretorios
            </Button>
          </Link>
          {workspaceQuery.isLoading ? (
            <div className="flex items-center gap-2 text-sm text-[#66746b]">
              <Loader2 className="h-4 w-4 animate-spin" />
              Carregando diretorio
            </div>
          ) : (
            <>
              <h1 className="truncate text-2xl font-semibold text-[#172126]">{workspaceQuery.data?.name}</h1>
              <p className="mt-1 truncate text-sm text-[#66746b]">{workspaceQuery.data?.absolutePath}</p>
            </>
          )}
        </div>
        <div className="flex items-center gap-2 rounded-md border border-[#d9dfd5] px-2.5 py-1.5 text-xs text-[#66746b]">
          <Radio className={hub.connected ? 'h-3.5 w-3.5 text-[#1f7a3a]' : 'h-3.5 w-3.5 text-[#b42318]'} />
          {hub.connected ? 'Tempo real ativo' : 'Reconectando'}
        </div>
      </div>
      {workspaceQuery.data ? (
        <div className="flex flex-col gap-3 rounded-lg border border-[#d9dfd5] bg-white p-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex min-w-0 items-start gap-3">
            <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-md bg-[#eef2eb]">
              <Sparkles className="h-4 w-4 text-[#254632]" />
            </div>
            <div className="min-w-0">
              <h2 className="text-sm font-semibold text-[#172126]">Contexto de IA</h2>
              <p className="mt-1 text-sm text-[#66746b]">
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
      <Outlet />
    </div>
  )
}
