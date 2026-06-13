import type {
  DiagramType,
  FutureTaskStatus,
  FutureTaskType,
  PromptKind,
  PromptStatus,
  PromptWorkflowStatus,
  TargetAgent,
} from './schemas'

export type PromptFilters = {
  workingDirectoryId?: string
  parentPromptId?: string
  rootOnly?: boolean
  status?: PromptStatus
  agent?: TargetAgent
  kind?: PromptKind
  q?: string
  futureTaskId?: string
}

export type FutureTaskFilters = {
  workingDirectoryId?: string
  status?: FutureTaskStatus
  type?: FutureTaskType
  label?: string
  includeArchived?: boolean
  q?: string
}

export type WorkflowBoardFilters = {
  workflowStatus?: PromptWorkflowStatus
  promptStatus?: PromptStatus
  workingDirectoryId?: string
  q?: string
}

export type NoteFilters = {
  notebookId?: string
  q?: string
  includeArchived?: boolean
}

export type DiagramFilters = {
  workingDirectoryId?: string
  type?: DiagramType
  q?: string
  includeArchived?: boolean
}

export const queryKeys = {
  workingDirectories: {
    all: ['working-directories'] as const,
    detail: (id: string) => ['working-directories', id] as const,
  },
  files: {
    search: (workingDirectoryId: string, query: string, limit: number) =>
      ['files', 'search', workingDirectoryId, query, limit] as const,
    searches: (workingDirectoryId: string) => ['files', 'search', workingDirectoryId] as const,
    tree: (workingDirectoryId: string, relativePath: string) =>
      ['files', 'tree', workingDirectoryId, relativePath] as const,
    trees: (workingDirectoryId: string) => ['files', 'tree', workingDirectoryId] as const,
    content: (workingDirectoryId: string, relativePath: string) =>
      ['files', 'content', workingDirectoryId, relativePath] as const,
  },
  git: {
    status: (workingDirectoryId: string) => ['git', 'status', workingDirectoryId] as const,
    originalFile: (workingDirectoryId: string, path: string) =>
      ['git', 'original-file', workingDirectoryId, path] as const,
    diff: (workingDirectoryId: string, path: string) => ['git', 'diff', workingDirectoryId, path] as const,
    history: (workingDirectoryId: string, path: string) => ['git', 'history', workingDirectoryId, path] as const,
    commitContent: (workingDirectoryId: string, path: string, hash: string) =>
      ['git', 'commit-content', workingDirectoryId, path, hash] as const,
  },
  prompts: {
    all: ['prompts'] as const,
    list: (filters: PromptFilters) => ['prompts', 'list', filters] as const,
    detail: (id: string) => ['prompts', id] as const,
    byTaskNumber: (workingDirectoryId: string, taskNumber: string) =>
      ['prompts', 'task-number', workingDirectoryId, taskNumber] as const,
    versions: (id: string) => ['prompts', id, 'versions'] as const,
  },
  futureTasks: {
    all: ['future-tasks'] as const,
    list: (filters: FutureTaskFilters) => ['future-tasks', 'list', filters] as const,
    detail: (id: string) => ['future-tasks', id] as const,
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
  notebooks: {
    all: ['notebooks'] as const,
    list: (includeArchived: boolean) => ['notebooks', 'list', includeArchived] as const,
    detail: (id: string) => ['notebooks', id] as const,
  },
  notes: {
    all: ['notes'] as const,
    list: (filters: NoteFilters) => ['notes', 'list', filters] as const,
    detail: (id: string) => ['notes', id] as const,
  },
  diagrams: {
    all: ['diagrams'] as const,
    list: (filters: DiagramFilters) => ['diagrams', 'list', filters] as const,
    detail: (id: string) => ['diagrams', id] as const,
  },
  terminals: {
    all: () => ['terminals', 'all'] as const,
    generic: () => ['terminals', 'generic'] as const,
    capabilities: () => ['terminals', 'capabilities'] as const,
    forPrompt: (promptId: string) => ['terminals', 'prompt', promptId] as const,
  },
}
