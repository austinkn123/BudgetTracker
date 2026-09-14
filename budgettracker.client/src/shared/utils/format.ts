/**
 * Money formatting for the whole app. This lived as a near-identical `Intl.NumberFormat` literal in
 * seven dashboard components; one definition keeps rounding and symbol placement consistent.
 *
 * Pair these with the `.numeric` class (see index.css) so figures use tabular lining numerals and
 * columns of money align on the decimal.
 */

/** Whole dollars — for headline figures and axis labels where cents are noise. */
export const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
});

/** Dollars and cents — for individual transaction amounts, where the exact figure matters. */
export const currencyPrecise = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/**
 * A transaction amount rendered with an explicit direction sign. Amounts are stored signed
 * (BUD-18), but the sign shown follows the inflow/outflow decision rather than the raw sign —
 * an Adjustment carries a meaningful sign of its own.
 */
export const signedAmount = (amount: number, inflow: boolean) =>
  `${inflow ? '+' : '-'}${currencyPrecise.format(Math.abs(amount))}`;

/** "62%" from 0.62. Percentages are always whole numbers in this UI. */
export const percent = (fraction: number) => `${Math.round(fraction * 100)}%`;
