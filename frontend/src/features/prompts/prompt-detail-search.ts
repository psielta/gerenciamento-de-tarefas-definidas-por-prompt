export type DetailTab = 'prompt' | 'linked-plan' | 'children' | 'timeline'

const TABS: ReadonlyArray<DetailTab> = ['prompt', 'linked-plan', 'children', 'timeline']

export function isDetailTab(value: unknown): value is DetailTab {
  return typeof value === 'string' && (TABS as readonly string[]).includes(value)
}

export function validatePromptDetailSearch(search: Record<string, unknown>): { tab?: DetailTab } {
  return {
    tab: isDetailTab(search.tab) ? search.tab : undefined,
  }
}
