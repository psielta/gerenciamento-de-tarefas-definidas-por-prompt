import { z } from 'zod'

export const targetAgentSchema = z.enum(['ClaudeCode', 'Codex'])
export const promptKindSchema = z.enum(['General', 'Planning'])
export const promptStatusSchema = z.enum(['Draft', 'Ready', 'Archived'])
export const futureTaskStatusSchema = z.enum(['Open', 'InProgress', 'Done', 'Archived'])
export const futureTaskTypeSchema = z.enum(['Bug', 'Feature', 'Task'])
export const linkedDocumentStatusSchema = z.enum(['Draft', 'Tracking', 'Paused', 'Error', 'Missing'])
export const linkedDocumentTypeSchema = z.enum(['ClaudeCodePlan'])
export const linkedDocumentVersionSourceSchema = z.enum(['Initial', 'FileChanged', 'ManualRefresh', 'Resumed'])
export const promptTemplateKeySchema = z.string().min(1)

export type TargetAgent = z.infer<typeof targetAgentSchema>
export type PromptKind = z.infer<typeof promptKindSchema>
export type PromptStatus = z.infer<typeof promptStatusSchema>
export type FutureTaskStatus = z.infer<typeof futureTaskStatusSchema>
export type FutureTaskType = z.infer<typeof futureTaskTypeSchema>
export type LinkedDocumentStatus = z.infer<typeof linkedDocumentStatusSchema>
export type LinkedDocumentType = z.infer<typeof linkedDocumentTypeSchema>
export type LinkedDocumentVersionSource = z.infer<typeof linkedDocumentVersionSourceSchema>
export type PromptTemplateKey = z.infer<typeof promptTemplateKeySchema>

export const fileMentionSchema = z.object({
  id: z.string().min(1),
  label: z.string().nullable().optional(),
  relativePath: z.string().optional(),
})

export const workingDirectorySchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  absolutePath: z.string(),
  respectGitignore: z.boolean(),
  enableAiContext: z.boolean(),
  taskNumberPattern: z.string().nullable(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
})

export const validatePathResponseSchema = z.object({
  isValid: z.boolean(),
  canonicalPath: z.string().nullable(),
  error: z.string().nullable(),
})

export const fileSearchResultSchema = z.object({
  relativePath: z.string(),
  fileName: z.string(),
  isDirectory: z.boolean(),
  score: z.number(),
})

export const fileTreeNodeSchema = z.object({
  relativePath: z.string(),
  name: z.string(),
  isDirectory: z.boolean(),
})

export const fileContentSchema = z.object({
  relativePath: z.string(),
  content: z.string(),
  sizeBytes: z.number(),
  truncated: z.boolean(),
  isBinary: z.boolean(),
})

export const gitFileStatusValueSchema = z.enum(['Modified', 'Added', 'Deleted', 'Renamed', 'Untracked'])

export const gitFileStatusSchema = z.object({
  path: z.string(),
  status: gitFileStatusValueSchema,
  originalPath: z.string().nullable().optional(),
})

export const gitOriginalFileSchema = z.object({
  content: z.string(),
})

export const gitDiffSchema = z.object({
  diff: z.string(),
})

export const fileReferenceValidationSchema = z.object({
  rawPath: z.string(),
  relativePath: z.string(),
  exists: z.boolean(),
  isDirectory: z.boolean(),
  error: z.string().nullable(),
})

export const promptSchema = z.object({
  id: z.string().uuid(),
  workingDirectoryId: z.string().uuid(),
  parentPromptId: z.string().uuid().nullable(),
  futureTaskId: z.string().uuid().nullable(),
  taskNumber: z.string().nullable(),
  title: z.string(),
  content: z.string(),
  targetAgent: targetAgentSchema,
  kind: promptKindSchema,
  status: promptStatusSchema,
  currentVersion: z.number(),
  rowVersion: z.string(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
  mentions: z.array(fileMentionSchema),
})

export const futureTaskSchema = z.object({
  id: z.string().uuid(),
  workingDirectoryId: z.string().uuid(),
  title: z.string(),
  description: z.string(),
  status: futureTaskStatusSchema,
  type: futureTaskTypeSchema,
  labels: z.array(z.string()),
  issueGithubId: z.string().nullable(),
  rowVersion: z.string(),
  linkedPromptCount: z.number(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
})

export const promptVersionSchema = z.object({
  id: z.string().uuid(),
  promptId: z.string().uuid(),
  versionNumber: z.number(),
  title: z.string(),
  content: z.string(),
  targetAgent: targetAgentSchema,
  kind: promptKindSchema,
  status: promptStatusSchema,
  changeNote: z.string().nullable(),
  createdAtUtc: z.string(),
})

export const linkedDocumentSchema = z.object({
  id: z.string().uuid(),
  promptId: z.string().uuid(),
  workingDirectoryId: z.string().uuid().nullable(),
  absolutePath: z.string(),
  displayName: z.string(),
  documentType: linkedDocumentTypeSchema,
  status: linkedDocumentStatusSchema,
  pullRequestReference: z.string().nullable(),
  currentVersion: z.number(),
  lastContentHash: z.string().nullable(),
  sizeBytes: z.number().nullable(),
  lastError: z.string().nullable(),
  lastSyncedAtUtc: z.string().nullable(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
})

export const linkedDocumentContentSchema = z.object({
  linkedDocumentId: z.string().uuid(),
  versionNumber: z.number(),
  content: z.string(),
  contentHash: z.string(),
  sizeBytes: z.number(),
  createdAtUtc: z.string(),
})

export const linkedDocumentVersionSchema = z.object({
  id: z.string().uuid(),
  linkedDocumentId: z.string().uuid(),
  versionNumber: z.number(),
  contentHash: z.string(),
  sizeBytes: z.number(),
  source: linkedDocumentVersionSourceSchema,
  createdAtUtc: z.string(),
})

export const promptTemplateInputSchema = z.object({
  key: z.string().min(1),
  label: z.string(),
  placeholder: z.string(),
  helpText: z.string(),
  required: z.boolean(),
  multiline: z.boolean().optional().default(false),
})

export const promptTemplateSchema = z.object({
  key: promptTemplateKeySchema,
  displayName: z.string(),
  description: z.string(),
  defaultTargetAgent: targetAgentSchema,
  defaultKind: promptKindSchema,
  input: promptTemplateInputSchema.nullable().optional(),
  inputs: z.array(promptTemplateInputSchema).optional().default([]),
})

export const promptDraftSchema = z.object({
  templateKey: promptTemplateKeySchema,
  linkedDocumentId: z.string().uuid(),
  workingDirectoryId: z.string().uuid(),
  parentPromptId: z.string().uuid(),
  title: z.string(),
  content: z.string(),
  targetAgent: targetAgentSchema,
  kind: promptKindSchema,
})

export type FileMention = z.infer<typeof fileMentionSchema>
export type WorkingDirectory = z.infer<typeof workingDirectorySchema>
export type ValidatePathResponse = z.infer<typeof validatePathResponseSchema>
export type FileSearchResult = z.infer<typeof fileSearchResultSchema>
export type FileTreeNode = z.infer<typeof fileTreeNodeSchema>
export type FileContent = z.infer<typeof fileContentSchema>
export type GitFileStatusValue = z.infer<typeof gitFileStatusValueSchema>
export type GitFileStatus = z.infer<typeof gitFileStatusSchema>
export type GitOriginalFile = z.infer<typeof gitOriginalFileSchema>
export type GitDiff = z.infer<typeof gitDiffSchema>
export type FileReferenceValidation = z.infer<typeof fileReferenceValidationSchema>
export type Prompt = z.infer<typeof promptSchema>
export type FutureTask = z.infer<typeof futureTaskSchema>
export type PromptVersion = z.infer<typeof promptVersionSchema>
export type LinkedDocument = z.infer<typeof linkedDocumentSchema>
export type LinkedDocumentContent = z.infer<typeof linkedDocumentContentSchema>
export type LinkedDocumentVersion = z.infer<typeof linkedDocumentVersionSchema>
export type PromptTemplate = z.infer<typeof promptTemplateSchema>
export type PromptTemplateInput = z.infer<typeof promptTemplateInputSchema>
export type GeneratedPromptDraft = z.infer<typeof promptDraftSchema>

export const workingDirectoryListSchema = z.array(workingDirectorySchema)
export const fileSearchResultListSchema = z.array(fileSearchResultSchema)
export const fileTreeNodeListSchema = z.array(fileTreeNodeSchema)
export const gitStatusListSchema = z.array(gitFileStatusSchema)
export const fileReferenceValidationListSchema = z.array(fileReferenceValidationSchema)
export const promptListSchema = z.array(promptSchema)
export const futureTaskListSchema = z.array(futureTaskSchema)
export const promptVersionListSchema = z.array(promptVersionSchema)
export const linkedDocumentListSchema = z.array(linkedDocumentSchema)
export const linkedDocumentVersionListSchema = z.array(linkedDocumentVersionSchema)
export const promptTemplateListSchema = z.array(promptTemplateSchema)

export const workflowActorSchema = z.enum(['ClaudeCode', 'Codex', 'Human'])
export const promptWorkflowStatusSchema = z.enum(['Active', 'Done'])
export const workflowEventTypeSchema = z.enum([
  'WorkflowStarted',
  'PhaseChanged',
  'ActorChanged',
  'Note',
  'Completed',
  'Reopened',
  'PhasesEdited',
])
export const workflowPhaseRoleSchema = z.enum([
  'PromptEngineering',
  'Planning',
  'PlanReview',
  'PlanCorrection',
  'Implementation',
  'CodeReview',
  'ReviewCorrection',
  'PracticalTest',
  'Rebase',
  'Merge',
])

export type WorkflowActor = z.infer<typeof workflowActorSchema>
export type PromptWorkflowStatus = z.infer<typeof promptWorkflowStatusSchema>
export type WorkflowEventType = z.infer<typeof workflowEventTypeSchema>
export type WorkflowPhaseRole = z.infer<typeof workflowPhaseRoleSchema>

export const agentUsageStatusSchema = z.enum([
  'Ok',
  'NoToken',
  'Unauthorized',
  'RateLimited',
  'HttpError',
  'Timeout',
  'NetworkError',
  'NoData',
  'Disabled',
  'Unavailable',
])

export const agentUsageWindowSchema = z.object({
  key: z.string(),
  label: z.string(),
  usedPercent: z.number(),
  resetsAtUtc: z.string().nullable(),
  windowMinutes: z.number().nullable(),
  estimated: z.boolean(),
  usedTokens: z.number().nullable(),
  limitTokens: z.number().nullable(),
})

export const agentUsageInfoSchema = z.object({
  agent: z.string(),
  status: agentUsageStatusSchema,
  httpStatusCode: z.number().nullable(),
  statusDetail: z.string().nullable(),
  plan: z.string().nullable(),
  windows: z.array(agentUsageWindowSchema),
})

export const agentUsageSchema = z.object({
  capturedAtUtc: z.string(),
  claude: agentUsageInfoSchema,
  codex: agentUsageInfoSchema,
})

export type AgentUsageStatus = z.infer<typeof agentUsageStatusSchema>
export type AgentUsageWindow = z.infer<typeof agentUsageWindowSchema>
export type AgentUsageInfo = z.infer<typeof agentUsageInfoSchema>
export type AgentUsage = z.infer<typeof agentUsageSchema>

export const workflowPhaseSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  defaultActor: workflowActorSchema,
  orderIndex: z.number(),
  color: z.string(),
  role: workflowPhaseRoleSchema.nullable(),
})

export const workflowEventSchema = z.object({
  id: z.string().uuid(),
  type: workflowEventTypeSchema,
  phaseId: z.string().uuid().nullable(),
  phaseName: z.string().nullable(),
  actor: workflowActorSchema.nullable(),
  note: z.string().nullable(),
  occurredAtUtc: z.string(),
})

export const workflowSchema = z.object({
  id: z.string().uuid(),
  promptId: z.string().uuid(),
  status: promptWorkflowStatusSchema,
  currentPhaseId: z.string().uuid().nullable(),
  currentPhaseName: z.string().nullable(),
  currentPhaseColor: z.string().nullable(),
  currentActor: workflowActorSchema.nullable(),
  startedAtUtc: z.string(),
  enteredCurrentPhaseAtUtc: z.string().nullable(),
  currentPhaseIteration: z.number(),
  reviewVerdictSourcePhaseName: z.string().nullable(),
  updatedAtUtc: z.string(),
  rowVersion: z.string(),
  phases: z.array(workflowPhaseSchema),
  events: z.array(workflowEventSchema),
})

export const taskSummarySchema = z.object({
  promptId: z.string().uuid(),
  workingDirectoryId: z.string().uuid(),
  workingDirectoryName: z.string(),
  taskNumber: z.string().nullable(),
  title: z.string(),
  promptStatus: promptStatusSchema,
  workflowStatus: promptWorkflowStatusSchema.nullable(),
  currentPhaseId: z.string().uuid().nullable(),
  currentPhaseName: z.string().nullable(),
  currentPhaseColor: z.string().nullable(),
  currentActor: workflowActorSchema.nullable(),
  enteredCurrentPhaseAtUtc: z.string().nullable(),
  currentPhaseIteration: z.number(),
  reviewVerdictSourcePhaseName: z.string().nullable(),
  updatedAtUtc: z.string(),
  hasChildPrompts: z.boolean(),
  hasLinkedPlan: z.boolean(),
  linkedDocumentId: z.string().uuid().nullable(),
  pullRequestReference: z.string().nullable(),
  promptRowVersion: z.string(),
  phases: z.array(workflowPhaseSchema),
  rowVersion: z.string().nullable(),
})

export const workflowTemplateSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  phases: z.array(workflowPhaseSchema),
})

export type WorkflowPhase = z.infer<typeof workflowPhaseSchema>
export type WorkflowEvent = z.infer<typeof workflowEventSchema>
export type Workflow = z.infer<typeof workflowSchema>
export type TaskSummary = z.infer<typeof taskSummarySchema>
export type WorkflowTemplate = z.infer<typeof workflowTemplateSchema>

export const taskSummaryListSchema = z.array(taskSummarySchema)

export const geminiModelSchema = z.object({
  id: z.string(),
  label: z.string(),
  thinkingMode: z.enum(['budget', 'level', 'none']),
  canDisableThinking: z.boolean(),
  thinkingBudgetMin: z.number(),
  thinkingBudgetMax: z.number(),
  minCacheTokens: z.number(),
})
export const aiSettingsSchema = z.object({
  model: z.string(),
  temperature: z.number(),
  thinkingEnabled: z.boolean(),
  thinkingBudget: z.number().nullable(),
  thinkingLevel: z.string().nullable(),
})
export const aiChatMessageSchema = z.object({
  id: z.string().uuid(),
  role: z.enum(['user', 'model']),
  content: z.string(),
  sequence: z.number(),
  cachedTokens: z.number().nullable(),
  createdAtUtc: z.string(),
})
export const aiChatSessionSchema = z.object({
  id: z.string().uuid(),
  workingDirectoryId: z.string().uuid().nullable(),
  promptId: z.string().uuid().nullable(),
  title: z.string(),
  model: z.string(),
  temperature: z.number(),
  thinkingEnabled: z.boolean(),
  thinkingBudget: z.number().nullable(),
  thinkingLevel: z.string().nullable(),
  createdAtUtc: z.string(),
  messages: z.array(aiChatMessageSchema),
})
export const refinedPromptSchema = z.object({
  content: z.string(),
  promptTokens: z.number(),
  candidateTokens: z.number(),
})
export const refinePromptRequestSchema = z.object({
  content: z.string(),
  model: z.string(),
  temperature: z.number(),
  thinkingMode: z.string().optional(),
  thinkingBudget: z.number().nullable().optional(),
  thinkingLevel: z.string().nullable().optional(),
  workingDirectoryId: z.string().uuid().optional(),
  contextFiles: z.array(z.string()),
  customInstructions: z.string().optional(),
})
export const translatePromptRequestSchema = z.object({
  content: z.string(),
  model: z.string(),
  temperature: z.number(),
  thinkingMode: z.string().optional(),
  thinkingBudget: z.number().nullable().optional(),
  thinkingLevel: z.string().nullable().optional(),
})
export const chatChunkSchema = z.object({
  text: z.string(),
  isThought: z.boolean(),
  done: z.boolean(),
  cachedTokens: z.number().nullable(),
})
export type GeminiModel = z.infer<typeof geminiModelSchema>
export type AiSettings = z.infer<typeof aiSettingsSchema>
export type AiChatMessage = z.infer<typeof aiChatMessageSchema>
export type AiChatSession = z.infer<typeof aiChatSessionSchema>
export type RefinedPrompt = z.infer<typeof refinedPromptSchema>
export type RefinePromptRequest = z.infer<typeof refinePromptRequestSchema>
export type TranslatePromptRequest = z.infer<typeof translatePromptRequestSchema>
export type ChatChunk = z.infer<typeof chatChunkSchema>
export const geminiModelListSchema = z.array(geminiModelSchema)
export const aiChatSessionListSchema = z.array(aiChatSessionSchema)

export const generateNoteRequestSchema = z.object({
  instruction: z.string(),
  format: z.string().optional(),
  model: z.string(),
  temperature: z.number(),
  thinkingMode: z.string().optional(),
  thinkingBudget: z.number().nullable().optional(),
  thinkingLevel: z.string().nullable().optional(),
  notebookId: z.string().uuid().optional(),
  currentContent: z.string().optional(),
})
export const generatedNoteSchema = z.object({
  suggestedTitle: z.string().nullable(),
  contentMarkdown: z.string(),
  promptTokens: z.number(),
  candidateTokens: z.number(),
})
export const generateMermaidRequestSchema = z.object({
  instruction: z.string(),
  diagramKind: z.string().optional(),
  model: z.string(),
  temperature: z.number(),
  thinkingMode: z.string().optional(),
  thinkingBudget: z.number().nullable().optional(),
  thinkingLevel: z.string().nullable().optional(),
  workingDirectoryId: z.string().uuid().optional(),
  diagramId: z.string().uuid().optional(),
  currentCode: z.string().optional(),
})
export const generatedMermaidSchema = z.object({
  mermaidCode: z.string(),
  titleSuggestion: z.string().nullable(),
  promptTokens: z.number(),
  candidateTokens: z.number(),
  warnings: z.array(z.string()),
})
export type GenerateNoteRequest = z.infer<typeof generateNoteRequestSchema>
export type GeneratedNote = z.infer<typeof generatedNoteSchema>
export type GenerateMermaidRequest = z.infer<typeof generateMermaidRequestSchema>
export type GeneratedMermaid = z.infer<typeof generatedMermaidSchema>

export const notebookSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  description: z.string().nullable(),
  workingDirectoryId: z.string().uuid().nullable(),
  workingDirectoryName: z.string().nullable(),
  isArchived: z.boolean(),
  noteCount: z.number(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
})
export type Notebook = z.infer<typeof notebookSchema>
export const notebookListSchema = z.array(notebookSchema)

export const noteSchema = z.object({
  id: z.string().uuid(),
  notebookId: z.string().uuid(),
  title: z.string(),
  contentMarkdown: z.string(),
  isPinned: z.boolean(),
  isArchived: z.boolean(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
})
export type Note = z.infer<typeof noteSchema>
export const noteListSchema = z.array(noteSchema)

export const diagramTypeSchema = z.enum(['Excalidraw', 'Mermaid'])
export type DiagramType = z.infer<typeof diagramTypeSchema>

const diagramBaseSchema = z.object({
  id: z.string().uuid(),
  workingDirectoryId: z.string().uuid(),
  title: z.string(),
  description: z.string().nullable(),
  type: diagramTypeSchema,
  isArchived: z.boolean(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
})

// The list summary carries the workspace name so the global /diagramas page can
// label which workspace each diagram belongs to.
export const diagramSummarySchema = diagramBaseSchema.extend({
  workingDirectoryName: z.string(),
})
export type DiagramSummary = z.infer<typeof diagramSummarySchema>
export const diagramSummaryListSchema = z.array(diagramSummarySchema)

export const diagramSchema = diagramBaseSchema.extend({
  content: z.string(),
  metadataJson: z.string().nullable(),
})
export type Diagram = z.infer<typeof diagramSchema>

export const terminalAgentLaunchSchema = z.enum(['Claude', 'Codex', 'Grok'])
export type TerminalAgentLaunch = z.infer<typeof terminalAgentLaunchSchema>

export const terminalSessionSchema = z.object({
  id: z.string().uuid(),
  promptId: z.string().uuid(),
  shell: z.string(),
  cwd: z.string(),
  createdAtUtc: z.string(),
})
export type TerminalSession = z.infer<typeof terminalSessionSchema>

export const terminalCapabilitiesSchema = z.object({
  enabled: z.boolean(),
})
export type TerminalCapabilities = z.infer<typeof terminalCapabilitiesSchema>
