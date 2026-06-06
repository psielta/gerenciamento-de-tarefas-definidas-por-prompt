import type { PromptKind, PromptStatus, PromptWorkflowStatus, TargetAgent } from './schemas'

export type PromptFilters = {
  workingDirectoryId?: string
  parentPromptId?: string
  rootOnly?: boolean
  status?: PromptStatus
  agent?: TargetAgent
  kind?: PromptKind
  q?: string
}

export type WorkflowBoardFilters = {
  workflowStatus?: PromptWorkflowStatus
  promptStatus?: PromptStatus
  workingDirectoryId?: string
  q?: string
}

export const queryKeys = {
  workingDirectories: {
    all: ['working-directories'] as const,
    detail: (id: string) => ['working-directories', id] as const,
  },
  files: {
    search: (workingDirectoryId: string, query: string, limit: number) =>
      ['files', 'search', workingDirectoryId, query, limit] as const,
    tree: (workingDirectoryId: string, relativePath: string) =>
      ['files', 'tree', workingDirectoryId, relativePath] as const,
    content: (workingDirectoryId: string, relativePath: string) =>
      ['files', 'content', workingDirectoryId, relativePath] as const,
  },
  prompts: {
    all: ['prompts'] as const,
    list: (filters: PromptFilters) => ['prompts', 'list', filters] as const,
    detail: (id: string) => ['prompts', id] as const,
    byTaskNumber: (workingDirectoryId: string, taskNumber: string) =>
      ['prompts', 'task-number', workingDirectoryId, taskNumber] as const,
    versions: (id: string) => ['prompts', id, 'versions'] as const,
  },
  promptTemplates: {
    all: ['prompt-templates'] as const,
    draft: (linkedDocumentId: string, templateKey: string, inputs?: Record<string, string>) =>
      ['prompt-templates', 'draft', linkedDocumentId, templateKey, inputs ?? {}] as const,
  },
  linkedDocuments: {
    all: ['linked-documents'] as const,
    forPrompt: (promptId: string) => ['linked-documents', 'prompt', promptId] as const,
    detail: (id: string) => ['linked-documents', id] as const,
    contentRoot: (id: string) => ['linked-documents', id, 'content'] as const,
    content: (id: string, version?: number) =>
      ['linked-documents', id, 'content', version ?? 'latest'] as const,
    versions: (id: string) => ['linked-documents', id, 'versions'] as const,
  },
  workflow: {
    all: ['workflow'] as const,
    board: (filters: WorkflowBoardFilters) => ['workflow', 'board', filters] as const,
    detail: (promptId: string) => ['workflow', 'detail', promptId] as const,
    template: () => ['workflow', 'template'] as const,
  },
  agentUsage: {
    current: () => ['agent-usage', 'current'] as const,
  },
  ai: {
    models: () => ['ai', 'models'] as const,
    settings: () => ['ai', 'settings'] as const,
    session: (id: string) => ['ai', 'sessions', id] as const,
    sessions: (promptId?: string, workingDirectoryId?: string) =>
      ['ai', 'sessions', { promptId, workingDirectoryId }] as const,
  },
}
