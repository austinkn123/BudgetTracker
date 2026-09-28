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
  /** Brand indigo — the single saturated accent (BUD-20). */
  primary: {
    main: '#635BFF',
    light: '#8F8AFF',
    dark: '#4B44D4',
    subtle: '#EFEEFF',
    contrastText: '#FFFFFF',
  },
  /** Supporting cyan for secondary data series. Used sparingly. */
  secondary: {
    main: '#0BA5EC',
    light: '#5CC8F5',
    dark: '#0876AB',
    subtle: '#E6F6FE',
    contrastText: '#FFFFFF',
  },
  success: {
    main: '#0E9F6E',
    light: '#4FC79E',
    dark: '#05704B',
    subtle: '#E6F6F0',
    contrastText: '#FFFFFF',
  },
  warning: {
    main: '#C77700',
    light: '#E5A23D',
    dark: '#8A5300',
    subtle: '#FDF3E2',
    contrastText: '#FFFFFF',
  },
  error: {
    main: '#DF1B41',
    light: '#EE6C86',
    dark: '#A81231',
    subtle: '#FDEBEF',
    contrastText: '#FFFFFF',
  },
  info: {
    main: '#3F6AD8',
    light: '#7C9AE8',
    dark: '#2C4CA0',
    subtle: '#ECF1FC',
    contrastText: '#FFFFFF',
  },
  /**
   * Neutrals — a cool navy-slate ramp. Deep, slightly blue ink over a
   * near-white page reads sharper than pure grey-on-grey.
   */
  neutral: {
    /** Page background. */
    background: '#F6F9FC',
    /** Card / elevated surface background. */
    surface: '#FFFFFF',
    /** Internal dividers, table row rules. */
    borderSubtle: '#F0F4F8',
    /** Default border + divider color — always visible, 1px (BUD-20). */
    border: '#E3E8EE',
    /** Control hover, active input borders. */
    borderStrong: '#C9D2DD',
    /** Primary body text — deep navy, not black. */
    textPrimary: '#0A2540',
    /** Secondary / muted text. */
    textSecondary: '#5B6B7F',
  },
  /** Full neutral ramp (navy-slate). */
  grey: {
    50: '#F6F9FC',
    100: '#F0F4F8',
    200: '#E3E8EE',
    300: '#C9D2DD',
    400: '#9AA8B8',
    500: '#5B6B7F',
    600: '#425466',
    700: '#2E4155',
    800: '#1B3049',
    900: '#0A2540',
  },
} as const;

/**
 * Dark theme values (BUD-17). Same shape as `colorTokens` — `tailwind.config.ts`
 * emits both sets as `--bud-*` channel triplets, light on `:root` and dark on
 * `.dark`, so every semantic utility switches theme without a component rewrite.
 *
 * Two deliberate departures from a naive inversion:
 *
 * 1. `dark` shade flips role. In the light theme `*-dark` is the deep variant
 *    used as text on a `*-subtle` background (33 usages) and as a button hover
 *    fill (2 usages). Text-on-subtle is the dominant case, and on a dark ground
 *    that text has to be LIGHT — so here `dark` is a light tint. The two button
 *    hovers carry an explicit `dark:` override instead.
 *
 * 2. The `grey` ramp is NOT inverted and is therefore absent from this map.
 *    It exists for structural surfaces that are dark in both themes — the
 *    floating nav pill (`bg-grey-900/85`), chart gridlines. Flipping it would
 *    turn the nav white.
 */
export const darkColorTokens = {
  primary: {
    main: '#635BFF',
    light: '#8F8AFF',
    dark: '#A9A5FF',
    subtle: '#1E1F4D',
    contrastText: '#FFFFFF',
    /** Button fill hover. Dark UIs lift on hover; light UIs deepen. */
    hover: '#7D76FF',
  },
  secondary: {
    main: '#0BA5EC',
    light: '#5CC8F5',
    dark: '#7FD6F8',
    subtle: '#0C3247',
    contrastText: '#FFFFFF',
  },
  success: {
    main: '#0E9F6E',
    light: '#4FC79E',
    dark: '#6FD8B4',
    subtle: '#0D3A2E',
    contrastText: '#FFFFFF',
  },
  warning: {
    main: '#C77700',
    light: '#E5A23D',
    dark: '#F0BC6E',
    subtle: '#3A2D14',
    contrastText: '#FFFFFF',
  },
  error: {
    main: '#DF1B41',
    light: '#EE6C86',
    dark: '#F492A6',
    subtle: '#3D1622',
    contrastText: '#FFFFFF',
    /** Button fill hover. Dark UIs lift on hover; light UIs deepen. */
    hover: '#E8506D',
  },
  info: {
    main: '#3F6AD8',
    light: '#7C9AE8',
    dark: '#A3B8F0',
    subtle: '#16294F',
    contrastText: '#FFFFFF',
  },
  /**
   * Deep navy, not grey. The page sits BELOW the card surface so cards lift by
   * a tone step plus a border — a shadow does no work on a dark ground.
   */
  neutral: {
    background: '#06121E',
    surface: '#0D2033',
    borderSubtle: '#16304A',
    border: '#1E3D5A',
    borderStrong: '#2A5075',
    textPrimary: '#E9F0F7',
    textSecondary: '#8FA5BC',
    /** Shadow ink. Near-black on dark; the light theme uses its own text ink. */
    shadow: '#000000',
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
 * A live token reference — `rgb(var(--bud-primary) / 1)`.
 *
 * Unlike reading a hex off `colorTokens`, this follows the theme at paint time.
 * That is what lets SVG and inline-style consumers (charts, gauges, sparklines)
 * switch with the rest of the UI instead of freezing the light palette at module
 * load, since they cannot use Tailwind's `dark:` variant. (BUD-17)
 */
export const cssVar = (name: string, alpha: number = 1): string =>
  `rgb(var(--bud-${name}) / ${alpha})`;

/**
 * JS-side alpha compositing for consumers that can't use Tailwind classes
 * (chart fills, SVG attributes). Returns `rgb(r g b / a)` — deliberately no
 * `#`, so output strings pass the hex-literal audit.
 *
 * Accepts a hex OR an existing `cssVar()` reference: tinting a theme-aware
 * color must not collapse it back to a fixed one.
 */
export const withAlpha = (color: string, alpha: number): string => {
  const tokenRef = color.match(/^rgb\(var\((--bud-[a-z0-9-]+)\)/i);
  if (tokenRef) return `rgb(var(${tokenRef[1]}) / ${alpha})`;
  return `rgb(${hexToChannels(color)} / ${alpha})`;
};

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

/**
 * Layered shadows; `--bud-shadow` is the ink channel triplet. Two layers each —
 * a tight contact shadow plus a wide soft one — which is what gives surfaces
 * real lift instead of looking like flat outlined boxes.
 */
export const shadowTokens = {
  xs: '0 1px 1px 0 rgb(var(--bud-shadow) / 0.04), 0 2px 4px -1px rgb(var(--bud-shadow) / 0.06)',
  sm: '0 1px 2px 0 rgb(var(--bud-shadow) / 0.05), 0 6px 16px -4px rgb(var(--bud-shadow) / 0.10)',
  md: '0 2px 4px -1px rgb(var(--bud-shadow) / 0.06), 0 12px 28px -6px rgb(var(--bud-shadow) / 0.14)',
  lg: '0 4px 8px -2px rgb(var(--bud-shadow) / 0.08), 0 24px 48px -12px rgb(var(--bud-shadow) / 0.20)',
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
