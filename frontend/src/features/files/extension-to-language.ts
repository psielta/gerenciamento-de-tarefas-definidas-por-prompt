const EXTENSION_LANGUAGE_MAP: Record<string, string> = {
  cs: 'csharp',
  css: 'css',
  go: 'go',
  html: 'html',
  htm: 'html',
  java: 'java',
  js: 'javascript',
  jsx: 'javascript',
  json: 'json',
  md: 'markdown',
  mjs: 'javascript',
  php: 'php',
  py: 'python',
  rb: 'ruby',
  rs: 'rust',
  scss: 'scss',
  sh: 'shell',
  sql: 'sql',
  svg: 'xml',
  toml: 'ini',
  ts: 'typescript',
  tsx: 'typescript',
  xml: 'xml',
  yaml: 'yaml',
  yml: 'yaml',
}

export function extensionToLanguage(extension: string | null | undefined) {
  if (!extension) {
    return 'plaintext'
  }

  const normalized = extension.replace(/^\./, '').toLocaleLowerCase()
  return EXTENSION_LANGUAGE_MAP[normalized] ?? 'plaintext'
}