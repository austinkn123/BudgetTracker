/**
 * BudgetTracker design tokens — the single source of truth (BUD-13, BUD-20).
 *
 * Consumed by tailwind.config.ts, which both builds semantic utilities from
 * these values and emits them as `:root` CSS custom properties (channel
 * triplets, so `bg-primary/10` opacity modifiers work).
 *
 * Never hardcode these hex values anywhere else. If a component needs a color,
 * it must use a semantic Tailwind class (`text-primary`, `bg-surface`,
 * `border-border`, ...) or, for JS-side consumers like charts, import from here.
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
    /** Internal dividers, table row rules. */
    borderSubtle: '#F1F5F9',
    /** Default border + divider color — always visible, 1px (BUD-20). */
    border: '#E2E8F0',
    /** Control hover, active input borders. */
    borderStrong: '#CBD5E1',
    /** Primary body text. */
    textPrimary: '#0F172A',
    /** Secondary / muted text. */
    textSecondary: '#64748B',
  },
  /** Full grey ramp (slate). */
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

/* ------------------------------------------------------------------ */
/* Color helpers (BUD-20)                                              */
/* ------------------------------------------------------------------ */

/**
 * '#1E6FD9' -> '30 111 217'. Channel triplets let Tailwind compose colors as
 * `rgb(var(--bud-primary) / <alpha-value>)`, enabling `bg-primary/10` etc.
 */
export const hexToChannels = (hex: string): string => {
  const value = hex.replace('#', '');
  const full =
    value.length === 3
      ? value
          .split('')
          .map((c) => c + c)
          .join('')
      : value;
  const int = Number.parseInt(full, 16);
  return `${(int >> 16) & 255} ${(int >> 8) & 255} ${int & 255}`;
};

/**
 * JS-side alpha compositing for consumers that can't use Tailwind classes
 * (chart fills, SVG attributes). Returns `rgb(r g b / a)` — deliberately no
 * `#`, so output strings pass the hex-literal audit.
 */
export const withAlpha = (hex: string, alpha: number): string =>
  `rgb(${hexToChannels(hex)} / ${alpha})`;

/* ------------------------------------------------------------------ */
/* Non-color scales (BUD-20, Stripe-leaning)                           */
/* ------------------------------------------------------------------ */

/** Radius scale. Deliberately clamped at 12px — anything larger reads soft. */
export const radiusTokens = {
  sm: '4px',
  DEFAULT: '6px',
  md: '8px',
  lg: '10px',
  xl: '12px',
  '2xl': '12px',
  '3xl': '12px',
  full: '9999px',
} as const;

/** Layered micro-shadows; `--bud-shadow` is the ink channel triplet. */
export const shadowTokens = {
  xs: '0 1px 2px 0 rgb(var(--bud-shadow) / 0.06)',
  sm: '0 1px 2px 0 rgb(var(--bud-shadow) / 0.06), 0 4px 12px -2px rgb(var(--bud-shadow) / 0.04)',
  md: '0 1px 2px 0 rgb(var(--bud-shadow) / 0.06), 0 8px 24px -4px rgb(var(--bud-shadow) / 0.08)',
  lg: '0 2px 4px 0 rgb(var(--bud-shadow) / 0.06), 0 16px 40px -8px rgb(var(--bud-shadow) / 0.12)',
  none: 'none',
} as const;

/** Motion: 120ms color, 160ms transform, 240ms overlays. */
export const motionTokens = {
  duration: { 120: '120ms', 160: '160ms', 240: '240ms' },
  easing: {
    'out-soft': 'cubic-bezier(0.16, 1, 0.3, 1)',
    'in-soft': 'cubic-bezier(0.4, 0, 1, 1)',
  },
} as const;

/** Extra font sizes beyond Tailwind's defaults (mutable tuples — Tailwind's
 * `fontSize` type rejects readonly arrays). */
export const fontSizeTokens: Record<
  string,
  [fontSize: string, configuration: { lineHeight: string }]
> = {
  '2xs': ['0.6875rem', { lineHeight: '1rem' }], // 11px — overline/meta
  body: ['0.9375rem', { lineHeight: '1.5rem' }], // 15px — default body copy
};
