import { createTheme } from '@mui/material/styles';
import { colorTokens } from './tokens';

/**
 * Expose the `subtle` shade on every palette color so components can reference
 * it through sx paths like `bgcolor: 'primary.subtle'` instead of hex values.
 */
declare module '@mui/material/styles' {
  interface PaletteColor {
    subtle: string;
  }
  interface SimplePaletteColorOptions {
    subtle?: string;
  }
}

/**
 * BudgetTracker MUI theme (BUD-13).
 *
 * All color values come from src/shared/theme/tokens.ts — the same token file
 * tailwind.config.ts consumes, so the two systems can never diverge.
 */
export const budgetTrackerTheme = createTheme({
  palette: {
    mode: 'light',
    primary: { ...colorTokens.primary },
    secondary: { ...colorTokens.secondary },
    success: { ...colorTokens.success },
    warning: { ...colorTokens.warning },
    error: { ...colorTokens.error },
    info: { ...colorTokens.info },
    grey: { ...colorTokens.grey },
    background: {
      default: colorTokens.neutral.background,
      paper: colorTokens.neutral.surface,
    },
    text: {
      primary: colorTokens.neutral.textPrimary,
      secondary: colorTokens.neutral.textSecondary,
    },
    divider: colorTokens.neutral.border,
  },

  /**
   * Breakpoints deliberately mirror Tailwind's defaults (BUD-14).
   *
   * MUI ships lg: 1200 while Tailwind's `lg:` is 1024, so `sx={{ display: { lg: ... } }}`
   * and `className="lg:..."` used to fire at different widths. Aligning them here is the
   * same single-source-of-truth fix BUD-13 applied to color.
   */
  breakpoints: {
    values: { xs: 0, sm: 640, md: 768, lg: 1024, xl: 1280 },
  },

  typography: {
    // Inter is self-hosted via @fontsource/inter (imported in main.tsx).
    fontFamily:
      '"Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", "Helvetica Neue", Arial, sans-serif',
    h1: { fontSize: '2.25rem', fontWeight: 700, lineHeight: 1.2 },
    h2: { fontSize: '1.875rem', fontWeight: 700, lineHeight: 1.25 },
    h3: { fontSize: '1.5rem', fontWeight: 600, lineHeight: 1.3 },
    h4: { fontSize: '1.25rem', fontWeight: 600, lineHeight: 1.4 },
    h5: { fontSize: '1.125rem', fontWeight: 600, lineHeight: 1.4 },
    h6: { fontSize: '1rem', fontWeight: 600, lineHeight: 1.5 },
    subtitle1: { fontSize: '1rem', fontWeight: 600, lineHeight: 1.5 },
    subtitle2: { fontSize: '0.875rem', fontWeight: 600, lineHeight: 1.45 },
    body1: { fontSize: '1rem', fontWeight: 400, lineHeight: 1.5 },
    body2: { fontSize: '0.875rem', fontWeight: 400, lineHeight: 1.5 },
    caption: { fontSize: '0.75rem', fontWeight: 400, lineHeight: 1.4 },
    overline: {
      fontSize: '0.75rem',
      fontWeight: 600,
      lineHeight: 1.5,
      letterSpacing: '0.08em',
      textTransform: 'uppercase',
    },
    button: { fontWeight: 600, textTransform: 'none' },
  },

  /**
   * Spacing base unit: 8px (MUI default, stated explicitly per BUD-13).
   * `theme.spacing(1)` === '8px'; sx shorthands (p: 2, gap: 1, ...) are 8px multiples.
   * Tailwind's default spacing scale is 4px-based, so Tailwind `p-2` === MUI `p: 1`.
   */
  spacing: 8,

  shape: {
    // Matches the pre-BUD-13 app-wide radius; revisit when radius tokens are formalized.
    borderRadius: 12,
  },

  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          fontWeight: 600,
        },
      },
    },
  },
});
