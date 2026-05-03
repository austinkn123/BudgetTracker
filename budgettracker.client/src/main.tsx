import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { alpha, ThemeProvider, createTheme } from '@mui/material/styles'
import CssBaseline from '@mui/material/CssBaseline'
import './index.css'
import App from './App.tsx'

const palette = {
  forestBlack: '#100B00',
  leaf: '#85CB33',
  parchment: '#EFFFC8',
  mist: '#A5CBC3',
  bark: '#3B341F',
}

const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: palette.leaf, contrastText: palette.forestBlack },
    secondary: { main: palette.mist, contrastText: palette.forestBlack },
    success: { main: palette.leaf, contrastText: palette.forestBlack },
    warning: { main: palette.parchment, contrastText: palette.forestBlack },
    error: { main: palette.bark, contrastText: palette.parchment },
    background: {
      default: palette.parchment,
      paper: alpha('#ffffff', 0.82),
    },
    text: {
      primary: palette.forestBlack,
      secondary: palette.bark,
    },
    divider: alpha(palette.bark, 0.18),
  },
  typography: {
    fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
  },
  shape: { borderRadius: 12 },
  components: {
    MuiCard: {
      defaultProps: { elevation: 0 },
      styleOverrides: {
        root: {
          border: `1px solid ${alpha(palette.mist, 0.45)}`,
          backgroundImage: 'none',
          backgroundColor: alpha('#ffffff', 0.7),
        },
      },
    },
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          backgroundColor: palette.parchment,
          color: palette.forestBlack,
        },
        '::selection': {
          backgroundColor: alpha(palette.leaf, 0.3),
        },
      },
    },
  },
})

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
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={theme}>
          <CssBaseline />
          <App />
        </ThemeProvider>
      </QueryClientProvider>
    </BrowserRouter>
  </StrictMode>,
)
