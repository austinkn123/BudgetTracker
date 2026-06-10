import type { Config } from 'tailwindcss';
import defaultTheme from 'tailwindcss/defaultTheme';
import { colorTokens } from './src/shared/theme/tokens';

/**
 * Tailwind config (BUD-13).
 *
 * Color utilities are SEMANTIC ONLY and sourced from the same token file as
 * the MUI theme (src/shared/theme/tokens.ts) — no divergence possible.
 * Use `text-primary`, `bg-surface`, `border-border`, `text-ink-muted`, etc.
 * Raw palette classes (`text-gray-900`, `bg-red-50`, ...) are considered
 * magic numbers; do not reintroduce them.
 */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  // Scope Tailwind utilities under #root so they win over MUI CssBaseline /
  // emotion styles without a specificity war (per BUD-13 technical notes).
  important: '#root',
  theme: {
    extend: {
      colors: {
        primary: {
          DEFAULT: colorTokens.primary.main,
          light: colorTokens.primary.light,
          dark: colorTokens.primary.dark,
          subtle: colorTokens.primary.subtle,
        },
        secondary: {
          DEFAULT: colorTokens.secondary.main,
          light: colorTokens.secondary.light,
          dark: colorTokens.secondary.dark,
          subtle: colorTokens.secondary.subtle,
        },
        success: {
          DEFAULT: colorTokens.success.main,
          light: colorTokens.success.light,
          dark: colorTokens.success.dark,
          subtle: colorTokens.success.subtle,
        },
        warning: {
          DEFAULT: colorTokens.warning.main,
          light: colorTokens.warning.light,
          dark: colorTokens.warning.dark,
          subtle: colorTokens.warning.subtle,
        },
        error: {
          DEFAULT: colorTokens.error.main,
          light: colorTokens.error.light,
          dark: colorTokens.error.dark,
          subtle: colorTokens.error.subtle,
        },
        info: {
          DEFAULT: colorTokens.info.main,
          light: colorTokens.info.light,
          dark: colorTokens.info.dark,
          subtle: colorTokens.info.subtle,
        },
        /** Page background — matches MUI palette.background.default. */
        background: colorTokens.neutral.background,
        /** Card / surface background — matches MUI palette.background.paper. */
        surface: colorTokens.neutral.surface,
        /** Border + divider — matches MUI palette.divider. */
        border: colorTokens.neutral.border,
        /** Text colors — match MUI palette.text.primary / text.secondary. */
        ink: {
          DEFAULT: colorTokens.neutral.textPrimary,
          muted: colorTokens.neutral.textSecondary,
        },
      },
      fontFamily: {
        sans: ['Inter', ...defaultTheme.fontFamily.sans],
      },
    },
  },
  plugins: [],
} satisfies Config;
