import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { BrowserRouter } from 'react-router-dom'
import { Provider as TooltipProvider } from '@radix-ui/react-tooltip'
// Self-hosted Inter (BUD-13) — no external font request, works offline.
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
// Note: react-day-picker's stylesheet is deliberately NOT imported — the
// Calendar component styles every part via the classNames API (BUD-20).
import './index.css'
import './auth/amplifyConfig'
import { AuthProvider } from './auth/AuthContext'
import { ThemeProvider } from './shared/theme/ThemeProvider'
import App from './App.tsx'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5,
      refetchOnWindowFocus: false,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <ThemeProvider>
        <AuthProvider>
          <QueryClientProvider client={queryClient}>
            <TooltipProvider delayDuration={200}>
              <App />
            </TooltipProvider>
            {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
          </QueryClientProvider>
        </AuthProvider>
      </ThemeProvider>
    </BrowserRouter>
  </StrictMode>,
)
