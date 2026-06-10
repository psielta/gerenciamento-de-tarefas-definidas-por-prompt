import { afterEach, describe, expect, it, vi } from 'vitest'
import { exportMarkdownPdf, sanitizePdfFilename } from './export-markdown-pdf'

const mocks = vi.hoisted(() => {
  const addPageMock = vi.fn()
  const getTextWidthMock = vi.fn((value: string) => String(value).length * 1.8)
  const lineMock = vi.fn()
  const rectMock = vi.fn()
  const saveMock = vi.fn()
  const setDrawColorMock = vi.fn()
  const setFillColorMock = vi.fn()
  const setFontMock = vi.fn()
  const setFontSizeMock = vi.fn()
  const setPageMock = vi.fn()
  const setPropertiesMock = vi.fn()
  const setTextColorMock = vi.fn()
  const splitTextToSizeMock = vi.fn((value: string) => String(value).split('\n'))
  const textMock = vi.fn()

  const jsPDFMock = vi.fn(function JsPdfConstructor() {
    return {
      addPage: addPageMock,
      getNumberOfPages: vi.fn(() => 1),
      getTextWidth: getTextWidthMock,
      internal: {
        pageSize: {
          getHeight: () => 297,
          getWidth: () => 210,
        },
      },
      line: lineMock,
      rect: rectMock,
      save: saveMock,
      setDrawColor: setDrawColorMock,
      setFillColor: setFillColorMock,
      setFont: setFontMock,
      setFontSize: setFontSizeMock,
      setPage: setPageMock,
      setProperties: setPropertiesMock,
      setTextColor: setTextColorMock,
      splitTextToSize: splitTextToSizeMock,
      text: textMock,
    }
  })

  return {
    jsPDFMock,
    getTextWidthMock,
    rectMock,
    saveMock,
    setPropertiesMock,
    textMock,
  }
})

vi.mock('jspdf', () => ({
  jsPDF: mocks.jsPDFMock,
}))

describe('sanitizePdfFilename', () => {
  it('normalizes accents and spaces', () => {
    expect(sanitizePdfFilename('Planejar Refatora\u00e7\u00e3o do M\u00f3dulo X')).toBe(
      'planejar-refatoracao-do-modulo-x',
    )
  })

  it('falls back when title is empty', () => {
    expect(sanitizePdfFilename('   ')).toBe('documento')
  })
})

describe('exportMarkdownPdf', () => {
  afterEach(() => {
    vi.clearAllMocks()
  })

  it('renders markdown directly with jsPDF and saves the file', async () => {
    await exportMarkdownPdf({
      title: 'Meu Prompt',
      subtitle: 'TASK-12',
      markdown: '# Titulo\n\nConteudo\n\n- item',
      filename: 'meu-prompt',
    })

    expect(mocks.jsPDFMock).toHaveBeenCalledWith({ unit: 'mm', format: 'a4', orientation: 'portrait' })
    expect(mocks.setPropertiesMock).toHaveBeenCalledWith({ title: 'Meu Prompt' })
    expect(mocks.saveMock).toHaveBeenCalledWith('meu-prompt.pdf')

    const writtenText = mocks.textMock.mock.calls
      .map(([value]) => String(value))
      .join(' ')
      .replace(/\s+/g, ' ')

    expect(writtenText).toContain('Meu Prompt')
    expect(writtenText).toContain('TASK-12')
    expect(writtenText).toContain('Titulo')
    expect(writtenText).toContain('Conteudo')
    expect(writtenText).toContain('-')
    expect(writtenText).toContain('item')
  })

  it('keeps tables, inline code, and multiline code blocks in the PDF output', async () => {
    await exportMarkdownPdf({
      title: 'Prompt tecnico',
      markdown: [
        'Texto com `inline` e **forte**.',
        '',
        '| Campo | Valor |',
        '| --- | --- |',
        '| id | `123` |',
        '',
        '```ts',
        'const item = {',
        '  name: "A"',
        '}',
        '```',
      ].join('\n'),
      filename: 'prompt-tecnico',
    })

    const writtenText = mocks.textMock.mock.calls
      .map(([value]) => String(value))
      .join('\n')
      .replace(/[ \t]+\n/g, '\n')

    expect(writtenText).toContain('Texto')
    expect(writtenText).toContain('inline')
    expect(writtenText).toContain('forte')
    expect(writtenText).toContain('Campo')
    expect(writtenText).toContain('Valor')
    expect(writtenText).toContain('id')
    expect(writtenText).toContain('123')
    expect(writtenText).toContain('ts')
    expect(writtenText).toContain('const item = {')
    expect(writtenText).toContain('  name: "A"')
    expect(writtenText).toContain('}')
    expect(mocks.rectMock).toHaveBeenCalled()
  })
})
