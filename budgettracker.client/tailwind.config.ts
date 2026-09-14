import type { Config } from 'tailwindcss';
import defaultTheme from 'tailwindcss/defaultTheme';
import plugin from 'tailwindcss/plugin';
import {
  colorTokens,
  darkColorTokens,
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

const lightVars: Record<string, string> = {};
const darkVars: Record<string, string> = {};

/** Register a token in BOTH themes and return the utility's var reference. */
const varRef = (name: string, lightHex: string, darkHex: string): string => {
  lightVars[`--bud-${name}`] = hexToChannels(lightHex);
  darkVars[`--bud-${name}`] = hexToChannels(darkHex);
  return `rgb(var(--bud-${name}) / <alpha-value>)`;
};

const semanticColors = Object.fromEntries(
  semanticScales.map((scale) => [
    scale,
    {
      DEFAULT: varRef(scale, colorTokens[scale].main, darkColorTokens[scale].main),
      ...Object.fromEntries(
        shades.map((shade) => [
          shade,
          varRef(`${scale}-${shade}`, colorTokens[scale][shade], darkColorTokens[scale][shade]),
        ]),
      ),
    },
  ]),
);

/**
 * Button fill hover. The `*-dark` shade cannot serve here: in the dark theme it
 * flips to a light text tint (see darkColorTokens). Light deepens, dark lifts.
 */
const hoverColors = {
  'primary-hover': varRef('primary-hover', colorTokens.primary.dark, darkColorTokens.primary.hover),
  'error-hover': varRef('error-hover', colorTokens.error.dark, darkColorTokens.error.hover),
};

// Shadow color channel — consumed by shadowTokens. Near-black on dark, where
// a navy-tinted shadow would read as a smudge rather than depth.
lightVars['--bud-shadow'] = hexToChannels(colorTokens.neutral.textPrimary);
darkVars['--bud-shadow'] = hexToChannels(darkColorTokens.neutral.shadow);

export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  // Class strategy, not media: the Appearance control has an explicit
  // light/dark/system choice, and "system" is resolved in JS (see theme.ts).
  darkMode: 'class',
  theme: {
    // Full overrides (not extend) — deliberately clamped scales (BUD-20).
    borderRadius: radiusTokens,
    boxShadow: shadowTokens,
    extend: {
      colors: {
        ...semanticColors,
        ...hoverColors,
        /** Page background — matches tokens.neutral.background. */
        background: varRef('background', colorTokens.neutral.background, darkColorTokens.neutral.background),
        /** Card / surface background. */
        surface: varRef('surface', colorTokens.neutral.surface, darkColorTokens.neutral.surface),
        /** Border scale: subtle (internal rules) / DEFAULT / strong (hover). */
        border: {
          subtle: varRef('border-subtle', colorTokens.neutral.borderSubtle, darkColorTokens.neutral.borderSubtle),
          DEFAULT: varRef('border', colorTokens.neutral.border, darkColorTokens.neutral.border),
          strong: varRef('border-strong', colorTokens.neutral.borderStrong, darkColorTokens.neutral.borderStrong),
        },
        /** Text colors. */
        ink: {
          DEFAULT: varRef('ink', colorTokens.neutral.textPrimary, darkColorTokens.neutral.textPrimary),
          muted: varRef('ink-muted', colorTokens.neutral.textSecondary, darkColorTokens.neutral.textSecondary),
        },
        /**
         * Neutral ramp. Structural surfaces only (the dark nav rail, chart
         * gridlines) — for text and borders prefer the semantic tokens above.
         */
        grey: Object.fromEntries(
          Object.entries(colorTokens.grey).map(([step, hex]) => [
            step,
            varRef(`grey-${step}`, hex, hex),
          ]),
        ),
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
      addBase({
        ':root': { ...lightVars, 'color-scheme': 'light' },
        '.dark': { ...darkVars, 'color-scheme': 'dark' },
      });
    }),
  ],
} satisfies Config;
