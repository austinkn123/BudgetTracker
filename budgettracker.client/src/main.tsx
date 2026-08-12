import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { BrowserRouter } from 'react-router-dom'
import { ThemeProvider } from '@mui/material/styles'
import { Provider as TooltipProvider } from '@radix-ui/react-tooltip'
// Self-hosted Inter (BUD-13) — no external font request, works offline.
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
// react-day-picker base styles must load BEFORE index.css so Tailwind
// utilities (equal specificity, later in the sheet) win ties (BUD-20).
import 'react-day-picker/style.css'
import './index.css'
import './auth/amplifyConfig'
import { AuthProvider } from './auth/AuthContext'
import { budgetTrackerTheme } from './shared/theme/theme'
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
      <AuthProvider>
        <QueryClientProvider client={queryClient}>
          {/* ThemeProvider remains until MUI teardown (BUD-20 Phase 6). */}
          <ThemeProvider theme={budgetTrackerTheme}>
            <TooltipProvider delayDuration={200}>
              <App />
            </TooltipProvider>
          </ThemeProvider>
          {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
        </QueryClientProvider>
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
)
