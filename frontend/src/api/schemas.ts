import { z } from 'zod'

export const targetAgentSchema = z.enum(['ClaudeCode', 'Codex'])
export const promptKindSchema = z.enum(['General', 'Planning'])
export const promptStatusSchema = z.enum(['Draft', 'Ready', 'Archived'])
export const linkedDocumentStatusSchema = z.enum(['Draft', 'Tracking', 'Paused', 'Error', 'Missing'])
export const linkedDocumentTypeSchema = z.enum(['ClaudeCodePlan'])
export const linkedDocumentVersionSourceSchema = z.enum(['Initial', 'FileChanged', 'ManualRefresh', 'Resumed'])
export const promptTemplateKeySchema = z.string().min(1)

export type TargetAgent = z.infer<typeof targetAgentSchema>
export type PromptKind = z.infer<typeof promptKindSchema>
export type PromptStatus = z.infer<typeof promptStatusSchema>
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
export type FileReferenceValidation = z.infer<typeof fileReferenceValidationSchema>
export type Prompt = z.infer<typeof promptSchema>
export type PromptVersion = z.infer<typeof promptVersionSchema>
export type LinkedDocument = z.infer<typeof linkedDocumentSchema>
export type LinkedDocumentContent = z.infer<typeof linkedDocumentContentSchema>
export type LinkedDocumentVersion = z.infer<typeof linkedDocumentVersionSchema>
export type PromptTemplate = z.infer<typeof promptTemplateSchema>
export type PromptTemplateInput = z.infer<typeof promptTemplateInputSchema>
export type GeneratedPromptDraft = z.infer<typeof promptDraftSchema>

export const workingDirectoryListSchema = z.array(workingDirectorySchema)
export const fileSearchResultListSchema = z.array(fileSearchResultSchema)
export const fileReferenceValidationListSchema = z.array(fileReferenceValidationSchema)
export const promptListSchema = z.array(promptSchema)
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

export type WorkflowActor = z.infer<typeof workflowActorSchema>
export type PromptWorkflowStatus = z.infer<typeof promptWorkflowStatusSchema>
export type WorkflowEventType = z.infer<typeof workflowEventTypeSchema>

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
  updatedAtUtc: z.string(),
  hasChildPrompts: z.boolean(),
  hasLinkedPlan: z.boolean(),
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
