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
 * (BUD-18); callers pass the direction (`isInflowTransaction`) so a zero net can still read "+".
 */
export const signedAmount = (amount: number, inflow: boolean) =>
  `${inflow ? '+' : '-'}${currencyPrecise.format(Math.abs(amount))}`;

/** A whole-dollar roll-up with its direction: "+$1,200", "−$80". */
export const signedCurrency = (amount: number) =>
  `${amount < 0 ? '−' : '+'}${currency.format(Math.abs(amount))}`;

/** "62%" from 0.62. Percentages are always whole numbers in this UI. */
export const percent = (fraction: number) => `${Math.round(fraction * 100)}%`;

/**
 * How far past plan something is. Whole dollars normally, but cents under $10: rounding
 * $0.40 over down to "$0 over" says over and nothing at once.
 */
export const overAmount = (amount: number) =>
  Math.abs(amount) < 10 ? currencyPrecise.format(amount) : currency.format(amount);

/**
 * An amount in the currency it was reported in, with that currency's own minor units. For
 * figures that aren't ours to convert (a brokerage account's balance).
 *
 * Per-unit prices pass `maxFractionDigits` so a sub-cent price (a crypto quote) keeps its digits
 * instead of rounding to "$0.00".
 */
export const inCurrency = (amount: number, currencyCode: string, maxFractionDigits?: number) => {
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currencyCode,
      maximumFractionDigits: maxFractionDigits,
    }).format(amount);
  } catch {
    // Intl throws RangeError on a code it doesn't know; show the figure rather than take the page down.
    return `${currencyCode} ${amount.toFixed(2)}`;
  }
};

/**
 * A per-unit price (a share's average cost, a sale price). From $1 up, the currency's own minor
 * units: $531.38, not $531.378333. Below $1, up to 6 decimals, so a sub-cent coin doesn't read
 * as $0.00.
 */
export const unitPrice = (amount: number, currencyCode: string) =>
  inCurrency(amount, currencyCode, Math.abs(amount) < 1 ? 6 : undefined);

/** "Oct 22, 2026, 2:04 PM" — an ISO instant in the viewer's own time zone. */
export const localDateTime = (iso: string) =>
  new Date(iso).toLocaleString('en-US', { dateStyle: 'medium', timeStyle: 'short' });

/** "2:04 PM" — the time of an ISO instant in the viewer's own time zone. */
export const localTime = (iso: string) =>
  new Date(iso).toLocaleTimeString('en-US', { timeStyle: 'short' });

const percentUnits = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/**
 * "59.09%" from 59.09. Takes percent units, not a fraction, and keeps 2 dp: unlike `percent`,
 * a column of these can be checked to add up to 100.
 */
export const percentPrecise = (value: number) => `${percentUnits.format(value)}%`;

/** A gain or loss with its direction: "+$1,495.00", "−$42.10". Zero carries no sign. */
export const withSign = (value: number, format: (magnitude: number) => string) =>
  `${value > 0 ? '+' : value < 0 ? '−' : ''}${format(Math.abs(value))}`;

const units = new Intl.NumberFormat('en-US', { maximumFractionDigits: 8 });

/** A holding's size: "20" shares, "0.05" of a coin. */
export const quantity = (value: number) => units.format(value);
