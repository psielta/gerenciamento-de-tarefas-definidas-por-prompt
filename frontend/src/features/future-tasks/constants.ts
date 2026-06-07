import { z } from 'zod'
import type { BadgeProps } from '@/components/ui/badge'
import { futureTaskTypeSchema, type FutureTaskStatus, type FutureTaskType } from '@/api/schemas'

export const STATUS_LABELS: Record<FutureTaskStatus, string> = {
  Open: 'Aberta',
  InProgress: 'Em andamento',
  Done: 'Concluida',
  Archived: 'Arquivada',
}

export const STATUS_BADGE_VARIANTS: Record<FutureTaskStatus, BadgeProps['variant']> = {
  Open: 'blue',
  InProgress: 'amber',
  Done: 'green',
  Archived: 'neutral',
}

export const TYPE_LABELS: Record<FutureTaskType, string> = {
  Bug: 'Bug',
  Feature: 'Feature',
  Task: 'Tarefa',
}

export const TYPE_BADGE_VARIANTS: Record<FutureTaskType, BadgeProps['variant']> = {
  Bug: 'red',
  Feature: 'green',
  Task: 'neutral',
}

export const STATUS_OPTIONS = [
  { value: 'Open', label: STATUS_LABELS.Open },
  { value: 'InProgress', label: STATUS_LABELS.InProgress },
  { value: 'Done', label: STATUS_LABELS.Done },
  { value: 'Archived', label: STATUS_LABELS.Archived },
] satisfies Array<{ value: FutureTaskStatus; label: string }>

export const TYPE_OPTIONS = [
  { value: 'Bug', label: TYPE_LABELS.Bug },
  { value: 'Feature', label: TYPE_LABELS.Feature },
  { value: 'Task', label: TYPE_LABELS.Task },
] satisfies Array<{ value: FutureTaskType; label: string }>

export const LABEL_OPTIONS = ['backend', 'frontend', 'database', 'devops', 'ai', 'priority:high'] as const

export const futureTaskFormSchema = z.object({
  title: z.string().trim().min(3, 'Informe um titulo com pelo menos 3 caracteres.'),
  description: z.string().max(20000),
  type: futureTaskTypeSchema,
  labels: z.array(z.string()),
  issueGithubId: z.string().trim().max(64),
})

export type FutureTaskFormValues = z.infer<typeof futureTaskFormSchema>
