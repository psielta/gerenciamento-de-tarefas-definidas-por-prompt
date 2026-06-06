import { X } from 'lucide-react'
import { useState } from 'react'

import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import type { WorkingDirectory } from '@/api/schemas'
import { useFileViewer } from '@/features/files/use-file-viewer'
import { WorkspaceFileTree } from '@/features/files/workspace-file-tree'
import { PromptForm } from '@/features/prompts/prompt-form'

type NewPromptDrawerProps = {
  defaultWorkingDirectoryId?: string
  workspaces: WorkingDirectory[]
  onClose: () => void
  onCreated: () => void
}

export function NewPromptDrawer({
  defaultWorkingDirectoryId,
  workspaces,
  onClose,
  onCreated,
}: NewPromptDrawerProps) {
  const { openFile } = useFileViewer()
  const [workingDirectoryId, setWorkingDirectoryId] = useState(
    defaultWorkingDirectoryId ?? (workspaces.length === 1 ? workspaces[0].id : '')
  )

  const handleCreated = () => {
    onCreated()
  }

  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-black/50 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="new-prompt-drawer-title"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
    >
      <div className="grid h-full w-full max-w-[min(96vw,56rem)] grid-rows-[auto_minmax(0,1fr)] border-l border-border bg-card shadow-2xl">
        <div className="flex min-w-0 items-center justify-between gap-2 border-b border-border px-4 py-2.5">
          <h2 id="new-prompt-drawer-title" className="min-w-0 truncate text-base font-semibold text-foreground">
            Novo prompt
          </h2>

          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="h-8 w-8 shrink-0 text-muted-foreground"
            onClick={onClose}
            aria-label="Fechar"
            title="Fechar"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div className="min-h-0 overflow-auto px-4 py-3">
          <div className="grid gap-4">
            <label className="grid gap-1 text-xs font-medium text-foreground">
              Diretório
              <Select
                value={workingDirectoryId}
                onChange={(event) => setWorkingDirectoryId(event.target.value)}
              >
                <option value="">Selecione um diretório</option>
                {workspaces.map((workspace) => (
                  <option key={workspace.id} value={workspace.id}>
                    {workspace.name}
                  </option>
                ))}
              </Select>
            </label>

            {workingDirectoryId ? (
              <div className="grid min-h-0 gap-4 lg:grid-cols-[14rem_minmax(0,1fr)]">
                <WorkspaceFileTree
                  workingDirectoryId={workingDirectoryId}
                  onOpenFile={(relativePath) => openFile(workingDirectoryId, relativePath)}
                  className="min-h-[24rem]"
                />
                <PromptForm
                  key={workingDirectoryId}
                  workingDirectoryId={workingDirectoryId}
                  onCreated={handleCreated}
                  showWorkspaceFileTree={false}
                />
              </div>
            ) : (
              <div className="rounded-md border border-dashed border-input bg-card p-3 text-sm text-muted-foreground">
                Selecione um diretório de trabalho para começar a escrever o prompt.
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
