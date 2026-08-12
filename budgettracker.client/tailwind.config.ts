import type { Config } from 'tailwindcss';
import defaultTheme from 'tailwindcss/defaultTheme';
import plugin from 'tailwindcss/plugin';
import {
  colorTokens,
  fontSizeTokens,
  hexToChannels,
  motionTokens,
  radiusTokens,
  shadowTokens,
} from './src/shared/theme/tokens';

/**
 * Tailwind config (BUD-13 tokens, BUD-20 Radix/Stripe redesign).
 *
 * Color utilities are SEMANTIC ONLY and sourced from src/shared/theme/tokens.ts.
 * Colors are emitted as `:root` CSS custom properties (channel triplets) and
 * mapped through `rgb(var(--bud-*) / <alpha-value>)`, so opacity modifiers
 * (`bg-primary/10`, `border-success/[0.32]`) work everywhere — including the
 * chart/SVG components. This is also the dark-mode seam (BUD-17): a `.dark`
 * block only has to redefine the variables.
 *
 * Raw palette classes (`text-gray-900`, `bg-red-50`, ...) are magic numbers;
 * do not reintroduce them.
 */

/** Build `rgb(var(--bud-x) / <alpha-value>)` refs + the matching :root vars. */
const semanticScales = ['primary', 'secondary', 'success', 'warning', 'error', 'info'] as const;
const shades = ['light', 'dark', 'subtle'] as const;

const cssVars: Record<string, string> = {};
const varRef = (name: string, hex: string): string => {
  cssVars[`--bud-${name}`] = hexToChannels(hex);
  return `rgb(var(--bud-${name}) / <alpha-value>)`;
};

const semanticColors = Object.fromEntries(
  semanticScales.map((scale) => [
    scale,
    {
      DEFAULT: varRef(scale, colorTokens[scale].main),
      ...Object.fromEntries(
        shades.map((shade) => [shade, varRef(`${scale}-${shade}`, colorTokens[scale][shade])]),
      ),
    },
  ]),
);

// Shadow color channel (ink) — consumed by shadowTokens.
cssVars['--bud-shadow'] = hexToChannels(colorTokens.neutral.textPrimary);

export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    // Full overrides (not extend) — deliberately clamped scales (BUD-20).
    borderRadius: radiusTokens,
    boxShadow: shadowTokens,
    extend: {
      colors: {
        ...semanticColors,
        /** Page background — matches tokens.neutral.background. */
        background: varRef('background', colorTokens.neutral.background),
        /** Card / surface background. */
        surface: varRef('surface', colorTokens.neutral.surface),
        /** Border scale: subtle (internal rules) / DEFAULT / strong (hover). */
        border: {
          subtle: varRef('border-subtle', colorTokens.neutral.borderSubtle),
          DEFAULT: varRef('border', colorTokens.neutral.border),
          strong: varRef('border-strong', colorTokens.neutral.borderStrong),
        },
        /** Text colors. */
        ink: {
          DEFAULT: varRef('ink', colorTokens.neutral.textPrimary),
          muted: varRef('ink-muted', colorTokens.neutral.textSecondary),
        },
      },
      fontFamily: {
        sans: ['Inter', ...defaultTheme.fontFamily.sans],
      },
      fontSize: fontSizeTokens,
      transitionDuration: motionTokens.duration,
      transitionTimingFunction: motionTokens.easing,
      keyframes: {
        'fade-in': { from: { opacity: '0' }, to: { opacity: '1' } },
        'fade-out': { from: { opacity: '1' }, to: { opacity: '0' } },
        'zoom-in': {
          from: { opacity: '0', transform: 'scale(0.97)' },
          to: { opacity: '1', transform: 'scale(1)' },
        },
        'slide-in-left': {
          from: { transform: 'translateX(-100%)' },
          to: { transform: 'translateX(0)' },
        },
        'slide-out-left': {
          from: { transform: 'translateX(0)' },
          to: { transform: 'translateX(-100%)' },
        },
        'collapse-down': {
          from: { height: '0' },
          to: { height: 'var(--radix-collapsible-content-height)' },
        },
        'collapse-up': {
          from: { height: 'var(--radix-collapsible-content-height)' },
          to: { height: '0' },
        },
      },
      animation: {
        'fade-in': 'fade-in 240ms cubic-bezier(0.16, 1, 0.3, 1)',
        'fade-out': 'fade-out 160ms cubic-bezier(0.4, 0, 1, 1)',
        'zoom-in': 'zoom-in 240ms cubic-bezier(0.16, 1, 0.3, 1)',
        'slide-in-left': 'slide-in-left 240ms cubic-bezier(0.16, 1, 0.3, 1)',
        'slide-out-left': 'slide-out-left 160ms cubic-bezier(0.4, 0, 1, 1)',
        'collapse-down': 'collapse-down 240ms cubic-bezier(0.16, 1, 0.3, 1)',
        'collapse-up': 'collapse-up 160ms cubic-bezier(0.4, 0, 1, 1)',
      },
    },
  },
  plugins: [
    // Emit the token-derived CSS custom properties. tokens.ts stays the single
    // source of truth; this plugin is the only place the vars are defined.
    plugin(({ addBase }) => {
      addBase({ ':root': cssVars });
    }),
  ],
} satisfies Config;
