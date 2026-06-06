import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { RouterProvider } from '@tanstack/react-router'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ThemeProvider } from '@/components/theme/theme-provider'
import { ThemedToaster } from '@/components/theme/themed-toaster'
import { queryClient } from '@/query-client'
import { FileViewerProvider } from '@/features/files/file-viewer-provider'
import '@/features/files/monaco-setup'
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
            <RouterProvider router={router} />
            <ThemedToaster />
          </FileViewerProvider>
        </PromptHubProvider>
        <ReactQueryDevtools initialIsOpen={false} />
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
)
