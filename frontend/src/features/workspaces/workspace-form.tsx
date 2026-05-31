import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { FolderCheck, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { FormField } from '@/components/form-field'
import { createWorkingDirectory, validateWorkingDirectoryPath } from '@/api/working-directories'
import { queryKeys } from '@/api/query-keys'
import { getErrorMessage } from '@/api/client'

const workspaceFormSchema = z.object({
  name: z.string().trim().min(2, 'Informe um nome com pelo menos 2 caracteres.'),
  absolutePath: z.string().trim().min(3, 'Informe o caminho absoluto do diretorio.'),
  respectGitignore: z.boolean(),
  enableAiContext: z.boolean(),
})

type WorkspaceFormValues = z.infer<typeof workspaceFormSchema>

export function WorkspaceForm() {
  const queryClient = useQueryClient()
  const [pathFeedback, setPathFeedback] = useState<string | null>(null)

  const form = useForm<WorkspaceFormValues>({
    resolver: zodResolver(workspaceFormSchema),
    defaultValues: {
      name: '',
      absolutePath: '',
      respectGitignore: true,
      enableAiContext: false,
    },
  })

  const createMutation = useMutation({
    mutationFn: createWorkingDirectory,
    onSuccess: async () => {
      form.reset({ name: '', absolutePath: '', respectGitignore: true, enableAiContext: false })
      setPathFeedback(null)
      await queryClient.invalidateQueries({ queryKey: queryKeys.workingDirectories.all })
      toast.success('Diretorio de trabalho criado.')
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const validateMutation = useMutation({
    mutationFn: validateWorkingDirectoryPath,
    onSuccess: (result) => {
      setPathFeedback(result.isValid ? `Caminho valido: ${result.canonicalPath}` : result.error)
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values))

  return (
    <form onSubmit={onSubmit} className="grid gap-4 rounded-lg border border-[#d9dfd5] bg-white p-4">
      <div className="grid gap-1">
        <h2 className="text-base font-semibold text-[#172126]">Novo diretorio</h2>
        <p className="text-sm text-[#66746b]">Registre a raiz onde os prompts pesquisarao arquivos.</p>
      </div>

      <FormField label="Nome" htmlFor="workspace-name" error={form.formState.errors.name?.message}>
        <Input id="workspace-name" placeholder="Projeto principal" {...form.register('name')} />
      </FormField>

      <FormField label="Caminho absoluto" htmlFor="workspace-path" error={form.formState.errors.absolutePath?.message}>
        <div className="flex flex-col gap-2 sm:flex-row">
          <Input
            id="workspace-path"
            placeholder="D:\\repos\\meu-projeto"
            {...form.register('absolutePath')}
          />
          <Button
            type="button"
            variant="secondary"
            onClick={() => validateMutation.mutate(form.getValues('absolutePath'))}
            disabled={validateMutation.isPending}
          >
            {validateMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <FolderCheck className="h-4 w-4" />}
            Validar
          </Button>
        </div>
      </FormField>

      {pathFeedback ? <p className="text-sm text-[#425048]">{pathFeedback}</p> : null}

      <label className="flex items-center gap-2 text-sm text-[#425048]">
        <input type="checkbox" className="h-4 w-4" {...form.register('respectGitignore')} />
        Respeitar .gitignore ao buscar arquivos
      </label>

      <label className="flex items-center gap-2 text-sm text-[#425048]">
        <input type="checkbox" className="h-4 w-4" {...form.register('enableAiContext')} />
        Injetar README.md, CLAUDE.md e AGENT.md no contexto da IA
      </label>

      <Button type="submit" disabled={createMutation.isPending}>
        {createMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
        Criar diretorio
      </Button>
    </form>
  )
}
