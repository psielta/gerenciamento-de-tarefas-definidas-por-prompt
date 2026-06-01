const DEFAULT_DATE_FORMAT = 'ddMMyy'
const TOKEN_PATTERN = /\{([^{}]+)\}/g
const TASK_NUMBER_PATTERN = /^[A-Za-z0-9_-]{1,64}$/
const LITERAL_PATTERN = /^[A-Za-z0-9_-]*$/
const DATE_TOKENS = new Set(['dd', 'MM', 'yy', 'yyyy'])

export function formatTaskNumberPreview(pattern: string, sequence: number, date: Date) {
  return pattern.replace(TOKEN_PATTERN, (_, rawToken: string) => {
    if (rawToken === 'N') {
      return String(sequence)
    }

    if (rawToken.startsWith('N:')) {
      const format = rawToken.slice(2)
      return String(sequence).padStart(format.length, '0')
    }

    if (rawToken === 'Date') {
      return formatDate(date, DEFAULT_DATE_FORMAT)
    }

    if (rawToken.startsWith('Date:')) {
      return formatDate(date, rawToken.slice(5))
    }

    return `{${rawToken}}`
  })
}

export function validateTaskNumberPattern(pattern: string | null | undefined): string[] {
  if (!pattern?.trim()) {
    return []
  }

  const errors: string[] = []
  if (pattern.length > 100) {
    errors.push('Use no maximo 100 caracteres.')
  }

  const tokens = Array.from(pattern.matchAll(TOKEN_PATTERN), (match) => match[1])
  if (!tokens.some((token) => token === 'N' || token.startsWith('N:'))) {
    errors.push('Inclua {N}.')
  }
  if (!tokens.some((token) => token === 'Date' || token.startsWith('Date:'))) {
    errors.push('Inclua {Date}.')
  }

  let cursor = 0
  for (const match of pattern.matchAll(TOKEN_PATTERN)) {
    const literal = pattern.slice(cursor, match.index)
    if (!LITERAL_PATTERN.test(literal)) {
      errors.push('Texto literal pode conter apenas letras, numeros, _ e -.')
      break
    }
    validateToken(match[1], errors)
    cursor = (match.index ?? 0) + match[0].length
  }

  const tail = pattern.slice(cursor)
  if (!LITERAL_PATTERN.test(tail)) {
    errors.push('Texto literal pode conter apenas letras, numeros, _ e -.')
  }

  if (errors.length === 0) {
    const preview = formatTaskNumberPreview(pattern, 1, new Date(Date.UTC(2026, 4, 28)))
    if (!TASK_NUMBER_PATTERN.test(preview)) {
      errors.push('O numero gerado deve ter ate 64 caracteres URL-safe.')
    }
  }

  return Array.from(new Set(errors))
}

function validateToken(token: string, errors: string[]) {
  if (token === 'N' || token === 'Date') {
    return
  }

  if (token.startsWith('N:')) {
    if (!/^0+$/.test(token.slice(2))) {
      errors.push('Use {N} ou preenchimento como {N:000}.')
    }
    return
  }

  if (token.startsWith('Date:')) {
    if (!isValidDateFormat(token.slice(5))) {
      errors.push('Use apenas dd, MM, yy e yyyy no formato de data.')
    }
    return
  }

  errors.push(`Token desconhecido {${token}}.`)
}

function isValidDateFormat(format: string) {
  if (!format || format.startsWith('-') || format.endsWith('-') || format.includes('--')) {
    return false
  }

  let index = 0
  while (index < format.length) {
    if (format[index] === '-') {
      index += 1
      continue
    }

    const start = index
    const runCharacter = format[index]
    if (runCharacter !== 'd' && runCharacter !== 'M' && runCharacter !== 'y') {
      return false
    }

    while (index < format.length && format[index] === runCharacter) {
      index += 1
    }

    if (!DATE_TOKENS.has(format.slice(start, index))) {
      return false
    }
  }

  return true
}

function formatDate(date: Date, format: string) {
  const day = String(date.getUTCDate()).padStart(2, '0')
  const month = String(date.getUTCMonth() + 1).padStart(2, '0')
  const year = String(date.getUTCFullYear())
  return format
    .replaceAll('yyyy', year)
    .replaceAll('yy', year.slice(-2))
    .replaceAll('MM', month)
    .replaceAll('dd', day)
}
