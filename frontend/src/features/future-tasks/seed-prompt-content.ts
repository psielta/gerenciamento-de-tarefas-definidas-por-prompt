import type { FutureTask } from '@/api/schemas'

// Seeds a new prompt's content from a future task. When the task already has a
// GitHub issue id, the agent is pointed at the issue; otherwise the task content
// is embedded inline. Phrasing is intentionally fixed (see issue #7).
export function buildSeededPromptContent(task: FutureTask): string {
  const header = task.issueGithubId
    ? `Please work on GitHub issue #${task.issueGithubId} in this repo. Then open a PR`
    : 'Please work on issue below in this repo. Then, open a PR'

  const body = task.description.trim() ? `# ${task.title}\n\n${task.description.trim()}` : `# ${task.title}`

  return `${header}\n\n---\n\n${body}\n`
}
