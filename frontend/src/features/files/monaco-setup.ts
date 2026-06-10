import { loader } from '@monaco-editor/react'
import * as monaco from 'monaco-editor'
import editorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker'
import cssWorker from 'monaco-editor/esm/vs/language/css/css.worker?worker'
import htmlWorker from 'monaco-editor/esm/vs/language/html/html.worker?worker'
import jsonWorker from 'monaco-editor/esm/vs/language/json/json.worker?worker'
import tsWorker from 'monaco-editor/esm/vs/language/typescript/ts.worker?worker'

self.MonacoEnvironment = {
  getWorker(_workerId, label) {
    switch (label) {
      case 'json':
        return new jsonWorker()
      case 'css':
      case 'scss':
      case 'less':
        return new cssWorker()
      case 'html':
      case 'handlebars':
      case 'razor':
        return new htmlWorker()
      case 'typescript':
      case 'javascript':
        return new tsWorker()
      default:
        return new editorWorker()
    }
  },
}

loader.config({ monaco })

export const MONACO_THEME_LIGHT = 'thoth-light'
export const MONACO_THEME_DARK = 'thoth-dark'

const scrollbarColors = {
  'scrollbarSlider.background': '#ffb90066',
  'scrollbarSlider.hoverBackground': '#ffb90099',
  'scrollbarSlider.activeBackground': '#e6a700',
}

monaco.editor.defineTheme(MONACO_THEME_LIGHT, {
  base: 'vs',
  inherit: true,
  rules: [],
  colors: scrollbarColors,
})

monaco.editor.defineTheme(MONACO_THEME_DARK, {
  base: 'vs-dark',
  inherit: true,
  rules: [],
  colors: scrollbarColors,
})

export function resolveMonacoTheme(resolvedTheme: 'light' | 'dark') {
  return resolvedTheme === 'dark' ? MONACO_THEME_DARK : MONACO_THEME_LIGHT
}