import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { RouterProvider } from '@tanstack/react-router'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ThemeProvider } from '@/components/theme/theme-provider'
import { ThemedToaster } from '@/components/theme/themed-toaster'
import { queryClient } from '@/query-client'
import { FileViewerProvider } from '@/features/files/file-viewer-provider'
import { GitHistoryProvider } from '@/features/files/git-history-provider'
import { PromptHubProvider } from '@/realtime/prompt-hub'
import { router } from '@/router'
import '@fontsource-variable/geist'
import '@fontsource-variable/jetbrains-mono'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <PromptHubProvider>
          <FileViewerProvider>
            <GitHistoryProvider>
              <RouterProvider router={router} />
              <ThemedToaster />
            </GitHistoryProvider>
          </FileViewerProvider>
        </PromptHubProvider>
        {/* bottom-left para nao cobrir o botao flutuante global de novo prompt */}
        <ReactQueryDevtools initialIsOpen={false} buttonPosition="bottom-left" />
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
)
