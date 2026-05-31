import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

export function MarkdownContent({ content }: { content: string }) {
  return (
    <div className="markdown-chat text-sm text-[#172126]">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          p({ children }) {
            return <p className="mb-3 last:mb-0 leading-relaxed">{children}</p>
          },
          ul({ children }) {
            return <ul className="mb-3 list-disc space-y-1 pl-5 last:mb-0">{children}</ul>
          },
          ol({ children }) {
            return <ol className="mb-3 list-decimal space-y-1 pl-5 last:mb-0">{children}</ol>
          },
          li({ children }) {
            return <li className="leading-relaxed">{children}</li>
          },
          h1({ children }) {
            return <h1 className="mb-3 mt-4 text-base font-bold text-[#172126] first:mt-0">{children}</h1>
          },
          h2({ children }) {
            return <h2 className="mb-2 mt-4 text-sm font-bold text-[#172126] first:mt-0">{children}</h2>
          },
          h3({ children }) {
            return <h3 className="mb-2 mt-3 text-sm font-semibold text-[#172126] first:mt-0">{children}</h3>
          },
          blockquote({ children }) {
            return (
              <blockquote className="my-3 border-l-[3px] border-[#254632] pl-3 italic text-[#66746b]">
                {children}
              </blockquote>
            )
          },
          pre({ children }) {
            return (
              <pre className="my-3 max-w-full overflow-x-auto rounded-lg bg-[#0d1117] p-4 text-xs leading-relaxed">
                {children}
              </pre>
            )
          },
          code({ className, children }) {
            if (className) {
              return <code className="font-mono text-[#e6edf3]">{children}</code>
            }
            return (
              <code className="rounded bg-[#eef2eb] px-1.5 py-0.5 font-mono text-xs text-[#1a4731]">
                {children}
              </code>
            )
          },
          table({ children }) {
            return (
              <div className="my-3 overflow-x-auto rounded-lg border border-[#e8ede5]">
                <table className="w-full text-xs">{children}</table>
              </div>
            )
          },
          thead({ children }) {
            return <thead className="bg-[#f7f8f6]">{children}</thead>
          },
          th({ children }) {
            return (
              <th className="border-b border-[#e8ede5] px-3 py-2 text-left font-semibold text-[#172126]">
                {children}
              </th>
            )
          },
          td({ children }) {
            return (
              <td className="border-b border-[#e8ede5] px-3 py-2 text-[#374151] last:border-b-0">
                {children}
              </td>
            )
          },
          a({ children, href }) {
            return (
              <a
                href={href}
                className="text-[#254632] underline underline-offset-2 hover:text-[#172126]"
                target="_blank"
                rel="noopener noreferrer"
              >
                {children}
              </a>
            )
          },
          strong({ children }) {
            return <strong className="font-semibold text-[#172126]">{children}</strong>
          },
          em({ children }) {
            return <em className="italic">{children}</em>
          },
          hr() {
            return <hr className="my-4 border-[#e8ede5]" />
          },
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  )
}
