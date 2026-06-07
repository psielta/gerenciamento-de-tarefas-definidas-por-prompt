import ReactMarkdown from 'react-markdown'
import rehypeSanitize from 'rehype-sanitize'
import remarkGfm from 'remark-gfm'
import { cn } from '@/lib/utils'

type MarkdownPreviewProps = {
  children: string
  className?: string
}

export function MarkdownPreview({ children, className }: MarkdownPreviewProps) {
  return (
    <div className={cn('markdown-preview max-h-36 text-sm text-muted-foreground', className)}>
      <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeSanitize]}>
        {children}
      </ReactMarkdown>
    </div>
  )
}
