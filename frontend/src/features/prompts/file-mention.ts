import {
  autoUpdate,
  computePosition,
  flip,
  offset,
  shift,
  size,
  type VirtualElement,
} from '@floating-ui/dom'
import { Mention } from '@tiptap/extension-mention'
import { PluginKey } from '@tiptap/pm/state'
import { exitSuggestion } from '@tiptap/suggestion'
import type { SuggestionKeyDownProps, SuggestionOptions, SuggestionProps } from '@tiptap/suggestion'
import type { FileSearchResult } from '@/api/schemas'

type MentionAttrs = {
  id: string | null
  label?: string | null
}

type SearchMentions = (query: string) => Promise<FileSearchResult[]>

const FileMentionPluginKey = new PluginKey('fileMention')

export const FileMention = Mention.extend({
  renderMarkdown(node) {
    return `@${node.attrs?.id ?? ''}`
  },
})

export function createFileMentionSuggestion(searchMentions: SearchMentions) {
  return {
    char: '@',
    pluginKey: FileMentionPluginKey,
    allowedPrefixes: null,
    items: ({ query }) => searchMentions(query),
    command: ({ editor, range, props }) => {
      if (!props.id) {
        return
      }

      editor
        .chain()
        .focus()
        .insertContentAt(range, [
          {
            type: 'mention',
            attrs: {
              id: props.id,
              label: props.label,
              mentionSuggestionChar: '@',
            },
          },
          {
            type: 'text',
            text: ' ',
          },
        ])
        .run()
    },
    render: () => {
      let container: HTMLDivElement | null = null
      let cleanupPosition: (() => void) | null = null
      let pendingExit: number | null = null
      let selectedIndex = 0
      let lastProps: SuggestionProps<FileSearchResult, MentionAttrs> | null = null

      const referenceElement: VirtualElement = {
        getBoundingClientRect: () => lastProps?.clientRect?.() ?? new DOMRect(0, 0, 0, 0),
        getClientRects: () => {
          const rect = lastProps?.clientRect?.()
          return rect ? [rect] : []
        },
      }

      const updatePosition = async () => {
        if (!container || !lastProps?.clientRect) {
          return
        }

        const rect = lastProps.clientRect()
        if (!rect) {
          container.style.visibility = 'hidden'
          return
        }

        container.style.visibility = 'visible'

        const { x, y } = await computePosition(referenceElement, container, {
          placement: 'bottom-start',
          strategy: 'fixed',
          middleware: [
            offset(8),
            flip({ padding: 8 }),
            shift({ padding: 8 }),
            size({
              padding: 8,
              apply({ availableWidth, availableHeight, elements }) {
                elements.floating.style.maxWidth = `${Math.min(560, availableWidth)}px`
                elements.floating.style.maxHeight = `${Math.min(360, availableHeight)}px`
              },
            }),
          ],
        })

        Object.assign(container.style, {
          left: `${x}px`,
          top: `${y}px`,
        })
      }

      const clearPendingExit = () => {
        if (pendingExit === null) {
          return
        }

        window.clearTimeout(pendingExit)
        pendingExit = null
      }

      const requestExitForEmptyItems = (props: SuggestionProps<FileSearchResult, MentionAttrs>) => {
        clearPendingExit()
        pendingExit = window.setTimeout(() => {
          pendingExit = null

          if (lastProps === props && props.items.length === 0) {
            exitSuggestion(props.editor.view, FileMentionPluginKey)
          }
        }, 0)
      }

      const selectItem = (index: number) => {
        const item = lastProps?.items[index]
        if (!item || !lastProps) {
          return false
        }

        lastProps.command({
          id: item.relativePath,
          label: item.relativePath,
        })

        return true
      }

      const renderItems = (props: SuggestionProps<FileSearchResult, MentionAttrs>) => {
        lastProps = props
        selectedIndex = Math.min(selectedIndex, Math.max(props.items.length - 1, 0))

        if (!container) {
          return
        }

        container.replaceChildren()

        if (!props.items.length) {
          container.style.display = 'none'
          requestExitForEmptyItems(props)
          return
        }

        clearPendingExit()
        container.style.display = ''

        props.items.forEach((item, index) => {
          const button = document.createElement('button')
          button.type = 'button'
          button.dataset.selected = String(index === selectedIndex)
          button.addEventListener('mousedown', (event) => {
            event.preventDefault()
            selectItem(index)
          })

          const type = document.createElement('span')
          type.className = 'mention-suggestion-type'
          type.dataset.kind = item.isDirectory ? 'directory' : 'file'
          type.textContent = item.isDirectory ? 'Pasta' : 'Arquivo'

          const content = document.createElement('span')
          content.className = 'mention-suggestion-content'

          const name = document.createElement('span')
          name.className = 'mention-suggestion-name'
          name.textContent = item.fileName

          const path = document.createElement('small')
          path.className = 'mention-suggestion-path'
          path.textContent = item.relativePath

          content.append(name, path)
          button.append(type, content)
          container?.appendChild(button)

          if (index === selectedIndex) {
            button.scrollIntoView({ block: 'nearest' })
          }
        })

        void updatePosition()
      }

      const moveSelection = (delta: number) => {
        const count = lastProps?.items.length ?? 0
        if (!count) {
          return
        }

        selectedIndex = (selectedIndex + delta + count) % count
        if (lastProps) {
          renderItems(lastProps)
        }
      }

      return {
        onStart: (props) => {
          container = document.createElement('div')
          container.className = 'mention-suggestion'
          document.body.appendChild(container)

          referenceElement.contextElement = props.decorationNode instanceof Element ? props.decorationNode : undefined
          cleanupPosition = autoUpdate(referenceElement, container, () => {
            void updatePosition()
          })

          renderItems(props)
        },
        onUpdate: (props) => {
          referenceElement.contextElement = props.decorationNode instanceof Element ? props.decorationNode : undefined
          renderItems(props)
        },
        onKeyDown: ({ view, event }: SuggestionKeyDownProps) => {
          if (event.key === 'ArrowDown') {
            if (!lastProps?.items.length) {
              return false
            }

            event.preventDefault()
            moveSelection(1)
            return true
          }

          if (event.key === 'ArrowUp') {
            if (!lastProps?.items.length) {
              return false
            }

            event.preventDefault()
            moveSelection(-1)
            return true
          }

          if (event.key === 'Enter' || event.key === 'Tab') {
            if (selectItem(selectedIndex)) {
              event.preventDefault()
              return true
            }

            return false
          }

          if (event.key === 'Escape') {
            event.preventDefault()
            exitSuggestion(view, FileMentionPluginKey)
            return true
          }

          return false
        },
        onExit: () => {
          clearPendingExit()
          cleanupPosition?.()
          cleanupPosition = null
          container?.remove()
          container = null
          lastProps = null
        },
      }
    },
  } satisfies Omit<SuggestionOptions<FileSearchResult, MentionAttrs>, 'editor'>
}
