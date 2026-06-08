import { Loader2, Shapes } from 'lucide-react'
import { useState } from 'react'
import { DiagramEditor } from './diagram-editor'
import { DiagramList } from './diagram-list'
import { useDiagram } from './use-diagrams'

type DiagramsWorkspaceProps = {
  workspaceId: string
}

export function DiagramsWorkspace({ workspaceId }: DiagramsWorkspaceProps) {
  const [selectedDiagramId, setSelectedDiagramId] = useState<string | null>(null)
  const diagramQuery = useDiagram(selectedDiagramId)

  return (
    <div className="flex min-h-[36rem] flex-col gap-4 lg:h-[calc(100svh-15rem)]">
      <div className="grid min-h-0 flex-1 gap-4 lg:grid-cols-[20rem_minmax(0,1fr)]">
        <DiagramList
          workspaceId={workspaceId}
          selectedDiagramId={selectedDiagramId}
          onSelect={setSelectedDiagramId}
        />

        {selectedDiagramId && diagramQuery.isLoading ? (
          <div className="flex min-h-[20rem] items-center justify-center rounded-lg border border-border bg-card text-sm text-muted-foreground">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Carregando diagrama
          </div>
        ) : selectedDiagramId && diagramQuery.data ? (
          <DiagramEditor key={diagramQuery.data.id} diagram={diagramQuery.data} />
        ) : (
          <div className="flex min-h-[20rem] flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-input bg-card p-6 text-center text-sm text-muted-foreground">
            <Shapes className="h-6 w-6 text-muted-foreground" />
            Selecione um diagrama ou crie um novo (Excalidraw ou Mermaid) para comecar.
          </div>
        )}
      </div>
    </div>
  )
}
