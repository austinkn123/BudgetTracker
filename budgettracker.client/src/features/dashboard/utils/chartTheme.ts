import { colorTokens, cssVar, withAlpha } from '../../../shared/theme/tokens';

/**
 * Semantic colors for charts (BUD-13 tokens, de-themed in BUD-20).
 * Use these to color charts by meaning rather than by sequence.
 *
 * Module-level consts holding CSS-var references — no theme context needed,
 * they follow the active theme on their own, and referential stability comes
 * free (the old getter-per-render pattern broke useMemo deps downstream).
 */
export interface SemanticColors {
  /** Positive money in (income, net surplus). Success green. */
  income: string;
  /** Negative money out (expense). Muted slate so it reads as neutral fact, not failure. */
  expense: string;
  /** Used when actuals exceed the plan (net deficit, over-budget). Warning amber. */
  overspend: string;
  /** Neutral / supporting accent (secondary teal). */
  neutral: string;
  /** Backdrop / page-background tone. */
  surface: string;
  /** Strong text/structure tone (primary text slate). */
  ink: string;
}

/**
 * Token REFERENCES, not resolved hex (BUD-17). Charts render into SVG and inline
 * styles where Tailwind's `dark:` variant cannot reach, so resolving a hex here
 * would freeze the light palette — gridlines drawn from `ink` would vanish into
 * a dark surface. `cssVar` defers the lookup to paint time instead.
 */
export const semanticColors: SemanticColors = {
  income: cssVar('success'),
  expense: cssVar('ink-muted'),
  overspend: cssVar('warning'),
  neutral: cssVar('secondary'),
  surface: cssVar('background'),
  ink: cssVar('ink'),
};

/**
 * An ordered set of category colors for charts that render multiple series.
 * Brand blue, warning amber, accent teal, muted ink, plus 65% tints of each so
 * the sequence stays harmonious. Success green is deliberately excluded — it is
 * reserved for the `income` semantic.
 *
 * The fourth slot is `ink-muted` rather than a fixed grey: the grey ramp does
 * not invert between themes, so a structural grey would go dim on dark.
 */
export const chartPalette: readonly string[] = [
  cssVar('primary'),
  cssVar('warning'),
  cssVar('secondary'),
  cssVar('ink-muted'),
  cssVar('primary', 0.65),
  cssVar('warning', 0.65),
  cssVar('secondary', 0.65),
  cssVar('ink-muted', 0.65),
];

/**
 * Build a linear-gradient CSS string between two token colors.
 * Useful for hero card backgrounds.
 */
export const buildGradient = (
  fromColor: string,
  toColor: string,
  fromAlpha: number = 0.9,
  toAlpha: number = 0.5,
  angle: number = 160,
): string =>
  `linear-gradient(${angle}deg, ${withAlpha(fromColor, fromAlpha)} 0%, ${withAlpha(toColor, toAlpha)} 100%)`;

/**
 * Hero surface (BUD-20): deep navy with a barely-there indigo wash. The old
 * mint-green gradient was the least on-brand element in the app — a dark hero
 * against light cards is what gives the dashboard a focal point.
 */
export const heroSurface: { background: string; border: string } = {
  background: `linear-gradient(135deg, ${colorTokens.grey[900]} 0%, ${colorTokens.grey[800]} 55%, ${withAlpha(colorTokens.primary.dark, 0.85)} 100%)`,
  border: 'transparent',
};
