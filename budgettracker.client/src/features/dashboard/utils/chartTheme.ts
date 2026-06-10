import { alpha, type Theme } from '@mui/material/styles';

/**
 * Semantic colors derived from the BudgetTracker MUI theme (BUD-13 tokens).
 * Use these to color charts by meaning rather than by sequence.
 *
 * Every value resolves through `theme.palette`, which is built from
 * src/shared/theme/tokens.ts — so editing tokens.ts propagates here.
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
 * Build the semantic-color map from the active theme.
 * Returns stable hex/rgba strings so they can be handed directly to chart components.
 */
export function getSemanticColors(theme: Theme): SemanticColors {
  return {
    income: theme.palette.success.main,
    expense: theme.palette.text.secondary,
    overspend: theme.palette.warning.main,
    neutral: theme.palette.secondary.main,
    surface: theme.palette.background.default,
    ink: theme.palette.text.primary,
  };
}

/**
 * An ordered set of category colors for charts that render multiple series.
 * Derived from the theme palette (brand blue, warning amber, accent teal,
 * structural slate) plus 65% tints of each so the sequence stays harmonious
 * with the rest of the UI. Ordered so adjacent series differ in hue and
 * lightness. Success green is deliberately excluded — it is reserved for the
 * `income` semantic and would mislead in a categorical sequence.
 */
export function getChartPalette(theme: Theme): string[] {
  const brand = theme.palette.primary.main;
  const amber = theme.palette.warning.main;
  const teal = theme.palette.secondary.main;
  const slate = theme.palette.grey[600];

  return [
    brand,
    amber,
    teal,
    slate,
    alpha(brand, 0.65),
    alpha(amber, 0.65),
    alpha(teal, 0.65),
    alpha(slate, 0.65),
  ];
}

/**
 * Build a linear-gradient CSS string between two theme colors.
 * Useful for hero card backgrounds.
 */
export function buildGradient(
  fromColor: string,
  toColor: string,
  fromAlpha: number = 0.9,
  toAlpha: number = 0.5,
  angle: number = 160,
): string {
  return `linear-gradient(${angle}deg, ${alpha(fromColor, fromAlpha)} 0%, ${alpha(toColor, toAlpha)} 100%)`;
}

/**
 * Convenience wrapper that returns gradient + border tones in one call
 * for hero/featured card styling.
 */
export function getHeroSurface(theme: Theme): { background: string; border: string } {
  return {
    background: buildGradient(
      theme.palette.background.default,
      theme.palette.secondary.main,
      0.9,
      0.5,
    ),
    border: alpha(theme.palette.primary.main, 0.45),
  };
}
