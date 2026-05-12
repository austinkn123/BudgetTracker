import { alpha, type Theme } from '@mui/material/styles';

/**
 * Semantic colors derived from the application's earthy MUI palette.
 * Use these to color charts by meaning rather than by sequence.
 */
export interface SemanticColors {
  /** Positive money in (income, net surplus). */
  income: string;
  /** Negative money out (expense). */
  expense: string;
  /** Warm warning hue used when actuals exceed the plan. Clay tone. */
  overspend: string;
  /** Neutral / supporting tone (mist). */
  neutral: string;
  /** Backdrop / paper-like tone (parchment). */
  surface: string;
  /** Strong text/structure tone (bark). */
  ink: string;
}

/**
 * Build the semantic-color map from the active theme.
 * Returns stable hex/rgba strings so they can be handed directly to chart components.
 */
export function getSemanticColors(theme: Theme): SemanticColors {
  return {
    income: theme.palette.primary.main,        // leaf
    expense: theme.palette.text.secondary,     // bark
    overspend: '#C97B4A',                      // clay (warm warning)
    neutral: theme.palette.secondary.main,     // mist
    surface: theme.palette.background.default, // parchment
    ink: theme.palette.text.primary,           // forest black
  };
}

/**
 * An ordered set of category colors for charts that render multiple series.
 * Derived from the theme (leaf, mist, bark) plus tints of each so the
 * sequence stays harmonious with the rest of the UI.
 */
export function getChartPalette(theme: Theme): string[] {
  const leaf = theme.palette.primary.main;
  const mist = theme.palette.secondary.main;
  const bark = theme.palette.text.secondary;
  const clay = '#C97B4A';

  return [
    leaf,
    mist,
    bark,
    clay,
    alpha(leaf, 0.65),
    alpha(mist, 0.65),
    alpha(bark, 0.65),
    alpha(clay, 0.65),
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
