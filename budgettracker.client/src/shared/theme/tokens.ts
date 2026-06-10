/**
 * BudgetTracker design tokens — the single source of truth for color values.
 *
 * Consumed by BOTH:
 *   - the MUI theme (src/shared/theme/theme.ts)
 *   - tailwind.config.ts (semantic Tailwind color utilities)
 *
 * Never hardcode these hex values anywhere else. If a component needs a color,
 * it must reference a MUI palette key (`primary.main`, `text.secondary`, ...)
 * or a semantic Tailwind class (`text-primary`, `bg-surface`, `border-border`, ...).
 *
 * Shade conventions per color:
 *   main         — the token itself (buttons, links, icons)
 *   light / dark — hover and emphasis variants
 *   subtle       — tinted background (banners, active nav states)
 *   contrastText — accessible text color on top of `main`
 */
export const colorTokens = {
  /** Brand blue. Working default #1E6FD9 — exact shade TBC with stakeholder (BUD-13). */
  primary: {
    main: '#1E6FD9',
    light: '#6EA3E8',
    dark: '#1656A8',
    subtle: '#E9F1FB',
    contrastText: '#FFFFFF',
  },
  /** Accent for highlights. Working default teal — TBC with stakeholder (BUD-13). */
  secondary: {
    main: '#14B8A6',
    light: '#5ED4C6',
    dark: '#0E8579',
    subtle: '#E7F8F6',
    contrastText: '#FFFFFF',
  },
  success: {
    main: '#2E7D32',
    light: '#6FBF73',
    dark: '#1B5E20',
    subtle: '#EAF5EB',
    contrastText: '#FFFFFF',
  },
  warning: {
    main: '#F59E0B',
    light: '#F8BA4B',
    dark: '#B45309',
    subtle: '#FEF3C7',
    contrastText: '#422006',
  },
  error: {
    main: '#D32F2F',
    light: '#E57373',
    dark: '#B71C1C',
    subtle: '#FDECEA',
    contrastText: '#FFFFFF',
  },
  info: {
    main: '#0288D1',
    light: '#4FC3F7',
    dark: '#01579B',
    subtle: '#E5F4FB',
    contrastText: '#FFFFFF',
  },
  /** Neutrals (slate scale — harmonizes with the brand blue). */
  neutral: {
    /** Page background. */
    background: '#F8FAFC',
    /** Card / elevated surface background. */
    surface: '#FFFFFF',
    /** Default border + divider color. */
    border: '#E2E8F0',
    /** Primary body text. */
    textPrimary: '#0F172A',
    /** Secondary / muted text. */
    textSecondary: '#64748B',
  },
  /** Full grey ramp backing MUI's `grey.*` palette (slate). */
  grey: {
    50: '#F8FAFC',
    100: '#F1F5F9',
    200: '#E2E8F0',
    300: '#CBD5E1',
    400: '#94A3B8',
    500: '#64748B',
    600: '#475569',
    700: '#334155',
    800: '#1E293B',
    900: '#0F172A',
  },
} as const;
