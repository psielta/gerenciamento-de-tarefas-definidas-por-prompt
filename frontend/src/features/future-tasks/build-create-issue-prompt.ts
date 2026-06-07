import type { FutureTask } from '@/api/schemas'
import { TYPE_LABELS } from './constants'

// Builds a clipboard-ready prompt that asks an agent to open a GitHub issue
// following best practices, seeded with the future task content.
export function buildCreateIssuePrompt(task: FutureTask): string {
  const labels = task.labels.length > 0 ? task.labels.join(', ') : '(choose appropriate labels)'
  const description = task.description.trim() ? task.description.trim() : '(no description provided)'

  return [
    'Please create a GitHub issue in this repository following best practices.',
    '',
    'Guidelines:',
    '- Write a clear, action-oriented title.',
    '- Structure the body in Markdown: context/motivation, proposed scope, and acceptance criteria.',
    '- Apply appropriate labels (type and area), creating them if needed.',
    '- Prefer the GitHub CLI (gh issue create) when available, then return the created issue number.',
    '',
    `Type: ${TYPE_LABELS[task.type]}`,
    `Suggested labels: ${labels}`,
    '',
    `Title: ${task.title}`,
    '',
    'Description:',
    description,
  ].join('\n')
}
