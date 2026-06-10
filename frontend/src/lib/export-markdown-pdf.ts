import remarkGfm from 'remark-gfm'
import remarkParse from 'remark-parse'
import { unified } from 'unified'

export type ExportMarkdownPdfOptions = {
  title: string
  subtitle?: string
  markdown: string
  filename?: string
}

type MarkdownNode = {
  type: string
  value?: string
  depth?: number
  lang?: string
  ordered?: boolean
  start?: number
  checked?: boolean | null
  url?: string
  alt?: string
  align?: Array<'left' | 'right' | 'center' | null>
  children?: MarkdownNode[]
}

type JsPdfConstructor = typeof import('jspdf').jsPDF
type PdfDocument = InstanceType<JsPdfConstructor>
type FontStyle = 'normal' | 'bold' | 'italic' | 'bolditalic'
type Rgb = [number, number, number]

type InlineSegment = {
  text: string
  fontFamily: string
  fontSize: number
  fontStyle: FontStyle
  color: Rgb
  code?: boolean
  strike?: boolean
}

type LineSegment = InlineSegment & {
  width: number
}

type InlineOptions = {
  x?: number
  maxWidth?: number
  lineHeight?: number
}

type TextOptions = InlineOptions & {
  fontSize?: number
  fontStyle?: FontStyle
  color?: Rgb
  fontFamily?: string
}

const PAGE = {
  marginTop: 18,
  marginRight: 16,
  marginBottom: 18,
  marginLeft: 16,
}

const COLORS = {
  text: [17, 17, 17] as Rgb,
  muted: [82, 82, 91] as Rgb,
  border: [212, 212, 216] as Rgb,
  soft: [244, 244, 245] as Rgb,
  link: [29, 78, 216] as Rgb,
  code: [39, 39, 42] as Rgb,
  codeFill: [244, 244, 245] as Rgb,
  tableHeader: [244, 244, 245] as Rgb,
}

const FONT = {
  body: 10.5,
  small: 8.5,
  code: 8.5,
  h1: 17,
  h2: 14,
  h3: 12,
  title: 16,
  subtitle: 9,
}

const BASE_INLINE_STYLE: Omit<InlineSegment, 'text'> = {
  fontFamily: 'helvetica',
  fontSize: FONT.body,
  fontStyle: 'normal',
  color: COLORS.text,
}

export function sanitizePdfFilename(title: string): string {
  const normalized = title
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 80)

  return normalized || 'documento'
}

function createMarkdownTree(markdown: string): MarkdownNode {
  return unified().use(remarkParse).use(remarkGfm).parse(markdown) as MarkdownNode
}

function cleanParagraphText(text: string): string {
  return text
    .replace(/\r\n?/g, '\n')
    .split('\n')
    .map((line) => line.replace(/\s+/g, ' ').trim())
    .filter(Boolean)
    .join('\n')
}

function inlineText(node: MarkdownNode): string {
  switch (node.type) {
    case 'text':
    case 'inlineCode':
    case 'html':
      return node.value ?? ''
    case 'break':
      return '\n'
    case 'link': {
      const label = childrenText(node)
      if (!node.url || node.url === label) {
        return label
      }

      return `${label} (${node.url})`
    }
    case 'image':
      return node.alt ? `[Imagem: ${node.alt}]` : '[Imagem]'
    default:
      return childrenText(node)
  }
}

function childrenText(node: MarkdownNode): string {
  return (node.children ?? []).map(inlineText).join('')
}

function blockText(node: MarkdownNode): string {
  if (node.type === 'code') {
    return node.value ?? ''
  }

  if (node.type === 'table') {
    return getTableRows(node)
      .map((row) => row.map((cell) => cleanParagraphText(childrenText(cell))).join(' | '))
      .join('\n')
  }

  return cleanParagraphText(childrenText(node))
}

function withBold(style: Omit<InlineSegment, 'text'>): Omit<InlineSegment, 'text'> {
  return {
    ...style,
    fontStyle: style.fontStyle === 'italic' || style.fontStyle === 'bolditalic' ? 'bolditalic' : 'bold',
  }
}

function withItalic(style: Omit<InlineSegment, 'text'>): Omit<InlineSegment, 'text'> {
  return {
    ...style,
    fontStyle: style.fontStyle === 'bold' || style.fontStyle === 'bolditalic' ? 'bolditalic' : 'italic',
  }
}

function inlineSegments(
  nodes: MarkdownNode[],
  style: Omit<InlineSegment, 'text'> = BASE_INLINE_STYLE,
): InlineSegment[] {
  return nodes.flatMap((node) => inlineSegment(node, style))
}

function inlineSegment(
  node: MarkdownNode,
  style: Omit<InlineSegment, 'text'> = BASE_INLINE_STYLE,
): InlineSegment[] {
  switch (node.type) {
    case 'text':
    case 'html':
      return node.value ? [{ ...style, text: node.value }] : []
    case 'inlineCode':
      return node.value
        ? [
            {
              ...style,
              text: node.value,
              fontFamily: 'courier',
              fontSize: style.fontSize * 0.92,
              color: COLORS.code,
              code: true,
            },
          ]
        : []
    case 'break':
      return [{ ...style, text: '\n' }]
    case 'strong':
      return inlineSegments(node.children ?? [], withBold(style))
    case 'emphasis':
      return inlineSegments(node.children ?? [], withItalic(style))
    case 'delete':
      return inlineSegments(node.children ?? [], { ...style, strike: true })
    case 'link': {
      const linkStyle = { ...style, color: COLORS.link }
      const label = inlineSegments(node.children ?? [], linkStyle)
      if (!node.url) {
        return label
      }

      return [
        ...label,
        {
          ...style,
          text: ` (${node.url})`,
          color: COLORS.muted,
          fontSize: style.fontSize * 0.9,
        },
      ]
    }
    case 'image':
      return [{ ...style, text: node.alt ? `[Imagem: ${node.alt}]` : '[Imagem]' }]
    default:
      return inlineSegments(node.children ?? [], style)
  }
}

function getTableRows(table: MarkdownNode): MarkdownNode[][] {
  return (table.children ?? []).map((row) => row.children ?? [])
}

function getTableColumnCount(rows: MarkdownNode[][]): number {
  return Math.max(1, ...rows.map((row) => row.length))
}

class MarkdownPdfRenderer {
  private readonly doc: PdfDocument
  private readonly pageWidth: number
  private readonly pageHeight: number
  private y: number

  constructor(doc: PdfDocument) {
    this.doc = doc
    this.pageWidth = doc.internal.pageSize.getWidth()
    this.pageHeight = doc.internal.pageSize.getHeight()
    this.y = PAGE.marginTop
  }

  render(title: string, subtitle: string | undefined, markdown: string): void {
    this.renderHeader(title, subtitle)

    const tree = createMarkdownTree(markdown)
    const blocks = tree.children ?? []

    if (blocks.length === 0) {
      this.writeWrappedText('Sem conteudo.', {
        fontSize: FONT.body,
        color: COLORS.muted,
      })
    } else {
      this.renderBlocks(blocks)
    }

    this.renderPageNumbers()
  }

  private get contentWidth(): number {
    return this.pageWidth - PAGE.marginLeft - PAGE.marginRight
  }

  private renderHeader(title: string, subtitle?: string): void {
    this.writeWrappedText(title, {
      fontSize: FONT.title,
      fontStyle: 'bold',
      lineHeight: 7,
    })

    if (subtitle) {
      this.y += 1
      this.writeWrappedText(subtitle, {
        fontSize: FONT.subtitle,
        color: COLORS.muted,
        lineHeight: 4.2,
      })
    }

    this.y += 4
    this.ensureSpace(2)
    this.doc.setDrawColor(...COLORS.border)
    this.doc.line(PAGE.marginLeft, this.y, this.pageWidth - PAGE.marginRight, this.y)
    this.y += 8
  }

  private renderBlocks(blocks: MarkdownNode[], indent = 0): void {
    for (const block of blocks) {
      this.renderBlock(block, indent)
    }
  }

  private renderBlock(block: MarkdownNode, indent = 0): void {
    switch (block.type) {
      case 'heading':
        this.renderHeading(block, indent)
        break
      case 'paragraph':
        this.renderParagraph(block, indent)
        break
      case 'list':
        this.renderList(block, indent)
        break
      case 'code':
        this.renderCode(block, indent)
        break
      case 'blockquote':
        this.renderQuote(block, indent)
        break
      case 'thematicBreak':
        this.renderRule(indent)
        break
      case 'table':
        this.renderTable(block, indent)
        break
      default:
        this.renderFallback(block, indent)
        break
    }
  }

  private renderHeading(block: MarkdownNode, indent: number): void {
    const depth = block.depth ?? 3
    const fontSize = depth === 1 ? FONT.h1 : depth === 2 ? FONT.h2 : FONT.h3
    const lineHeight = depth === 1 ? 7.4 : depth === 2 ? 6.3 : 5.4
    const segments = inlineSegments(block.children ?? [], {
      ...BASE_INLINE_STYLE,
      fontSize,
      fontStyle: 'bold',
    })

    if (segments.length === 0) {
      return
    }

    this.y += depth === 1 ? 3 : 2
    this.writeInlineSegments(segments, {
      x: PAGE.marginLeft + indent,
      maxWidth: this.contentWidth - indent,
      lineHeight,
    })

    if (depth === 2) {
      this.y += 1
      this.ensureSpace(2)
      this.doc.setDrawColor(...COLORS.border)
      this.doc.line(PAGE.marginLeft + indent, this.y, this.pageWidth - PAGE.marginRight, this.y)
      this.y += 4
    } else {
      this.y += 3
    }
  }

  private renderParagraph(block: MarkdownNode, indent: number): void {
    const segments = inlineSegments(block.children ?? [])
    if (segments.length === 0) {
      return
    }

    this.writeInlineSegments(segments, {
      x: PAGE.marginLeft + indent,
      maxWidth: this.contentWidth - indent,
      lineHeight: 5.2,
    })
    this.y += 3
  }

  private renderList(block: MarkdownNode, indent: number): void {
    const items = block.children ?? []
    const ordered = block.ordered === true
    const start = block.start ?? 1

    this.y += 1

    items.forEach((item, index) => {
      const marker = ordered ? `${start + index}. ` : '- '
      const taskPrefix = item.checked === true ? '[x] ' : item.checked === false ? '[ ] ' : ''
      const children = item.children ?? []
      const firstContent = children.find((child) => child.type !== 'list')
      const remainingContent = children.filter((child) => child.type !== 'list' && child !== firstContent)
      const nestedLists = children.filter((child) => child.type === 'list')
      const x = PAGE.marginLeft + indent
      const maxWidth = this.contentWidth - indent

      if (!firstContent) {
        this.writeWrappedText(`${marker}${taskPrefix}`.trim(), { x, maxWidth, lineHeight: 5.1 })
      } else if (firstContent.type === 'paragraph') {
        this.writeInlineSegments(
          [
            { ...BASE_INLINE_STYLE, text: `${marker}${taskPrefix}` },
            ...inlineSegments(firstContent.children ?? []),
          ],
          { x, maxWidth, lineHeight: 5.1 },
        )
      } else {
        this.writeWrappedText(`${marker}${taskPrefix}`.trim(), { x, maxWidth, lineHeight: 5.1 })
        this.renderBlock(firstContent, indent + 7)
      }

      for (const child of remainingContent) {
        this.renderBlock(child, indent + 7)
      }

      for (const child of nestedLists) {
        this.renderList(child, indent + 7)
      }

      this.y += 1.5
    })

    this.y += 2
  }

  private renderCode(block: MarkdownNode, indent: number): void {
    const code = block.value ?? ''
    if (!code) {
      return
    }

    const x = PAGE.marginLeft + indent
    const maxWidth = this.contentWidth - indent
    const textWidth = maxWidth - 5
    const lines = code
      .replace(/\r\n?/g, '\n')
      .split('\n')
      .flatMap((line) => this.splitCodeLine(line || ' ', textWidth))

    this.y += 2

    if (block.lang) {
      this.writeWrappedText(block.lang, {
        x,
        maxWidth,
        fontFamily: 'courier',
        fontSize: FONT.small,
        color: COLORS.muted,
        lineHeight: 4,
      })
    }

    for (const line of lines) {
      this.ensureSpace(5)
      this.doc.setFillColor(...COLORS.codeFill)
      this.doc.rect(x, this.y - 3.5, maxWidth, 5, 'F')
      this.setTextStyle(FONT.code, 'normal', COLORS.code, 'courier')
      this.doc.text(line, x + 2.5, this.y)
      this.y += 4.4
    }

    this.y += 4
  }

  private renderQuote(block: MarkdownNode, indent: number): void {
    const text = cleanParagraphText((block.children ?? []).map(blockText).filter(Boolean).join('\n'))
    if (!text) {
      return
    }

    const x = PAGE.marginLeft + indent
    const textX = x + 4
    const maxWidth = this.contentWidth - indent - 4
    const segments: InlineSegment[] = [
      {
        ...BASE_INLINE_STYLE,
        text,
        color: COLORS.muted,
        fontStyle: 'italic',
      },
    ]
    const lines = this.wrapInlineSegments(segments, maxWidth)

    this.y += 1
    for (const line of lines) {
      this.ensureSpace(5.2)
      this.doc.setDrawColor(...COLORS.muted)
      this.doc.line(x, this.y - 3.7, x, this.y + 1.2)
      this.renderInlineLine(line, textX, this.y, 5.2)
      this.y += 5.2
    }

    this.y += 4
  }

  private renderRule(indent: number): void {
    this.y += 2
    this.ensureSpace(2)
    this.doc.setDrawColor(...COLORS.border)
    this.doc.line(PAGE.marginLeft + indent, this.y, this.pageWidth - PAGE.marginRight, this.y)
    this.y += 6
  }

  private renderTable(block: MarkdownNode, indent: number): void {
    const rows = getTableRows(block)
    if (rows.length === 0) {
      return
    }

    const x = PAGE.marginLeft + indent
    const maxWidth = this.contentWidth - indent
    const columnCount = getTableColumnCount(rows)
    const columnWidth = maxWidth / columnCount
    const paddingX = 2
    const paddingY = 1.8
    const lineHeight = 4.1
    const tableFontSize = 8.5

    this.y += 2

    rows.forEach((row, rowIndex) => {
      const isHeader = rowIndex === 0
      const cellLines = Array.from({ length: columnCount }, (_, columnIndex) => {
        const cell = row[columnIndex]
        const text = cell ? cleanParagraphText(childrenText(cell)) : ''
        return this.wrapPlainText(text || ' ', columnWidth - paddingX * 2, {
          fontFamily: 'helvetica',
          fontSize: tableFontSize,
          fontStyle: isHeader ? 'bold' : 'normal',
        })
      })
      const rowHeight = Math.max(1, ...cellLines.map((lines) => lines.length)) * lineHeight + paddingY * 2

      this.ensureSpace(rowHeight)

      for (let columnIndex = 0; columnIndex < columnCount; columnIndex += 1) {
        const cellX = x + columnIndex * columnWidth
        const lines = cellLines[columnIndex] ?? [' ']

        if (isHeader) {
          this.doc.setFillColor(...COLORS.tableHeader)
          this.doc.rect(cellX, this.y, columnWidth, rowHeight, 'F')
        }

        this.doc.setDrawColor(...COLORS.border)
        this.doc.rect(cellX, this.y, columnWidth, rowHeight, 'S')
        this.setTextStyle(tableFontSize, isHeader ? 'bold' : 'normal', COLORS.text)

        lines.forEach((line, lineIndex) => {
          this.doc.text(line, cellX + paddingX, this.y + paddingY + lineHeight * (lineIndex + 1) - 1)
        })
      }

      this.y += rowHeight
    })

    this.y += 4
  }

  private renderFallback(block: MarkdownNode, indent: number): void {
    const text = blockText(block)
    if (!text) {
      return
    }

    this.writeWrappedText(text, {
      x: PAGE.marginLeft + indent,
      maxWidth: this.contentWidth - indent,
      fontSize: FONT.body,
      lineHeight: 5.2,
    })
    this.y += 3
  }

  private writeWrappedText(text: string, options: TextOptions = {}): void {
    this.writeInlineSegments(
      [
        {
          ...BASE_INLINE_STYLE,
          text,
          fontFamily: options.fontFamily ?? 'helvetica',
          fontSize: options.fontSize ?? FONT.body,
          fontStyle: options.fontStyle ?? 'normal',
          color: options.color ?? COLORS.text,
        },
      ],
      options,
    )
  }

  private writeInlineSegments(segments: InlineSegment[], options: InlineOptions = {}): void {
    const x = options.x ?? PAGE.marginLeft
    const maxWidth = options.maxWidth ?? this.contentWidth
    const lineHeight = options.lineHeight ?? 5
    const lines = this.wrapInlineSegments(segments, maxWidth)

    for (const line of lines) {
      this.ensureSpace(lineHeight)
      this.renderInlineLine(line, x, this.y, lineHeight)
      this.y += lineHeight
    }
  }

  private wrapInlineSegments(segments: InlineSegment[], maxWidth: number): LineSegment[][] {
    const lines: LineSegment[][] = []
    let currentLine: LineSegment[] = []
    let currentWidth = 0

    const flushLine = () => {
      while (currentLine.length > 0 && currentLine[currentLine.length - 1].text === ' ') {
        const removed = currentLine.pop()
        currentWidth -= removed?.width ?? 0
      }

      lines.push(currentLine)
      currentLine = []
      currentWidth = 0
    }

    const appendSegment = (segment: InlineSegment) => {
      if (segment.text === '\n') {
        flushLine()
        return
      }

      const candidates = this.segmentWidth(segment) > maxWidth
        ? this.splitLongInlineSegment(segment, maxWidth)
        : [segment]

      for (const candidate of candidates) {
        const width = this.segmentWidth(candidate)

        if (candidate.text === ' ' && currentLine.length === 0) {
          continue
        }

        if (currentLine.length > 0 && currentWidth + width > maxWidth) {
          flushLine()
        }

        if (candidate.text === ' ' && currentLine.length === 0) {
          continue
        }

        currentLine.push({ ...candidate, width })
        currentWidth += width
      }
    }

    for (const segment of this.tokenizeInlineSegments(segments)) {
      appendSegment(segment)
    }

    if (currentLine.length > 0 || lines.length === 0) {
      flushLine()
    }

    return lines
  }

  private tokenizeInlineSegments(segments: InlineSegment[]): InlineSegment[] {
    const tokens: InlineSegment[] = []

    for (const segment of segments) {
      const text = segment.text.replace(/\r\n?/g, '\n')

      if (segment.code) {
        for (const part of text.split(/(\n)/)) {
          if (!part) {
            continue
          }

          tokens.push({ ...segment, text: part })
        }
        continue
      }

      for (const part of text.split(/(\n|\s+)/)) {
        if (!part) {
          continue
        }

        if (part === '\n') {
          tokens.push({ ...segment, text: '\n' })
        } else if (/^\s+$/.test(part)) {
          tokens.push({ ...segment, text: ' ' })
        } else {
          tokens.push({ ...segment, text: part })
        }
      }
    }

    return tokens
  }

  private splitLongInlineSegment(segment: InlineSegment, maxWidth: number): InlineSegment[] {
    const pieces: InlineSegment[] = []
    let buffer = ''

    for (const char of segment.text) {
      const candidate = `${buffer}${char}`
      if (buffer && this.segmentWidth({ ...segment, text: candidate }) > maxWidth) {
        pieces.push({ ...segment, text: buffer })
        buffer = char
      } else {
        buffer = candidate
      }
    }

    if (buffer) {
      pieces.push({ ...segment, text: buffer })
    }

    return pieces
  }

  private renderInlineLine(line: LineSegment[], x: number, y: number, lineHeight: number): void {
    let cursorX = x

    for (const segment of line) {
      if (!segment.text) {
        continue
      }

      if (segment.code) {
        this.doc.setFillColor(...COLORS.codeFill)
        this.doc.rect(cursorX - 0.7, y - lineHeight + 1.1, segment.width + 1.4, lineHeight - 1.1, 'F')
      }

      this.setTextStyle(segment.fontSize, segment.fontStyle, segment.color, segment.fontFamily)
      this.doc.text(segment.text, cursorX, y)

      if (segment.strike) {
        this.doc.setDrawColor(...segment.color)
        this.doc.line(cursorX, y - lineHeight / 3, cursorX + segment.width, y - lineHeight / 3)
      }

      cursorX += segment.width
    }
  }

  private splitCodeLine(text: string, maxWidth: number): string[] {
    this.setTextStyle(FONT.code, 'normal', COLORS.code, 'courier')

    if (this.doc.getTextWidth(text) <= maxWidth) {
      return [text]
    }

    const lines: string[] = []
    let buffer = ''

    for (const char of text) {
      const candidate = `${buffer}${char}`
      if (buffer && this.doc.getTextWidth(candidate) > maxWidth) {
        lines.push(buffer)
        buffer = char
      } else {
        buffer = candidate
      }
    }

    if (buffer) {
      lines.push(buffer)
    }

    return lines.length > 0 ? lines : [' ']
  }

  private wrapPlainText(
    text: string,
    maxWidth: number,
    style: { fontFamily: string; fontSize: number; fontStyle: FontStyle },
  ): string[] {
    this.setTextStyle(style.fontSize, style.fontStyle, COLORS.text, style.fontFamily)

    return text.split('\n').flatMap((line) => {
      const wrapped = this.doc.splitTextToSize(line || ' ', maxWidth) as string[]
      return wrapped.length > 0 ? wrapped : [' ']
    })
  }

  private segmentWidth(segment: InlineSegment): number {
    this.setTextStyle(segment.fontSize, segment.fontStyle, segment.color, segment.fontFamily)
    return this.doc.getTextWidth(segment.text)
  }

  private ensureSpace(requiredHeight: number): void {
    if (this.y + requiredHeight <= this.pageHeight - PAGE.marginBottom) {
      return
    }

    this.doc.addPage('a4', 'portrait')
    this.y = PAGE.marginTop
  }

  private setTextStyle(
    fontSize: number,
    fontStyle: FontStyle,
    color: Rgb,
    fontFamily = 'helvetica',
  ): void {
    this.doc.setFont(fontFamily, fontStyle)
    this.doc.setFontSize(fontSize)
    this.doc.setTextColor(...color)
  }

  private renderPageNumbers(): void {
    const pageCount = this.doc.getNumberOfPages()

    for (let page = 1; page <= pageCount; page += 1) {
      this.doc.setPage(page)
      this.setTextStyle(FONT.small, 'normal', COLORS.muted)
      this.doc.text(
        `${page}/${pageCount}`,
        this.pageWidth - PAGE.marginRight,
        this.pageHeight - 8,
        { align: 'right' },
      )
    }
  }
}

export async function exportMarkdownPdf({
  title,
  subtitle,
  markdown,
  filename,
}: ExportMarkdownPdfOptions): Promise<void> {
  const outputName = `${filename ?? sanitizePdfFilename(title)}.pdf`
  const { jsPDF } = await import('jspdf')
  const doc = new jsPDF({ unit: 'mm', format: 'a4', orientation: 'portrait' })

  doc.setProperties({ title })

  const renderer = new MarkdownPdfRenderer(doc)
  renderer.render(title, subtitle, markdown)
  doc.save(outputName)
}
