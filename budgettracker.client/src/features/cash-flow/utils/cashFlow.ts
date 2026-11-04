import {
  addDays,
  differenceInCalendarDays,
  endOfMonth,
  endOfYear,
  format,
  max,
  min,
  parseISO,
  startOfMonth,
  startOfYear,
  subDays,
  subMonths,
} from 'date-fns';
import type { CashFlowGrouping, CashFlowTotals, IncomeSource } from '../../../shared/types/api';
import type { SpendSlice } from '../../dashboard/utils/selectors';

export type CashFlowRangeKey = 'month' | '3m' | 'ytd' | '12m' | 'all' | 'custom';

/** Inclusive calendar-date window. A null `from` means "since the beginning" (All). */
export interface DateWindow {
  from: Date | null;
  to: Date;
}

export interface DateSpan {
  from: Date;
  to: Date;
}

/** Calendar dates go to the server as `yyyy-MM-dd`, never as instants (see transactionFilters.ts). */
export const toDateKey = (date: Date) => format(date, 'yyyy-MM-dd');

/** The window a range covers, ending today. Multi-month ranges start on the 1st so buckets are whole. */
export const rangeWindow = (range: CashFlowRangeKey, today: Date, custom: DateSpan): DateWindow => {
  switch (range) {
    case 'month':
      return { from: startOfMonth(today), to: today };
    case '3m':
      return { from: startOfMonth(subMonths(today, 2)), to: today };
    case 'ytd':
      return { from: startOfYear(today), to: today };
    case '12m':
      return { from: startOfMonth(subMonths(today, 11)), to: today };
    case 'all':
      return { from: null, to: today };
    case 'custom':
      return custom;
  }
};

const spanDays = (span: DateSpan) => differenceInCalendarDays(span.to, span.from) + 1;

/** The grouping a range opens with, until the person picks one. */
export const defaultGrouping = (range: CashFlowRangeKey, bounds: DateWindow): CashFlowGrouping => {
  switch (range) {
    case 'month':
      return 'Day';
    case '3m':
      return 'Week';
    case 'custom': {
      if (!bounds.from) return 'Month';
      const days = spanDays({ from: bounds.from, to: bounds.to });
      if (days <= 31) return 'Day';
      if (days <= 120) return 'Week';
      return 'Month';
    }
    default:
      return 'Month';
  }
};

/**
 * The like-for-like window to compare against. Fixed ranges shift back by their own length in
 * months, so a month-to-date compares with the same days of last month (clamped at month end:
 * Mar 1–31 compares with Feb 1–28). Custom compares with the equal-length span right before it.
 */
export const compareWindow = (range: CashFlowRangeKey, bounds: DateWindow): DateSpan | null => {
  const { from, to } = bounds;
  if (!from) return null;
  const shift = (months: number) => ({ from: subMonths(from, months), to: subMonths(to, months) });

  switch (range) {
    case 'month':
      return shift(1);
    case '3m':
      return shift(3);
    case 'ytd':
    case '12m':
      return shift(12);
    case 'custom': {
      const days = spanDays({ from, to });
      return { from: subDays(from, days), to: subDays(from, 1) };
    }
    default:
      return null;
  }
};

const bucketEnd = (start: Date, grouping: CashFlowGrouping): Date => {
  switch (grouping) {
    case 'Day':
      return start;
    case 'Week':
      return addDays(start, 6);
    case 'Month':
      return endOfMonth(start);
    case 'Year':
      return endOfYear(start);
  }
};

/**
 * The dates one bucket covers, clipped to the window. The first week can start before the window
 * and the last month runs past today; the bar only counts what is inside, so the drill-down must too.
 */
export const periodBounds = (
  start: string,
  grouping: CashFlowGrouping,
  bounds: DateWindow,
): DateSpan => {
  const bucketStart = parseISO(start);
  return {
    from: bounds.from ? max([bucketStart, bounds.from]) : bucketStart,
    to: min([bucketEnd(bucketStart, grouping), bounds.to]),
  };
};

const TICK_FORMAT: Record<CashFlowGrouping, string> = {
  Day: 'MMM d',
  Week: 'MMM d',
  Month: 'MMM ’yy',
  Year: 'yyyy',
};

const LABEL_FORMAT: Record<CashFlowGrouping, string> = {
  Day: 'EEE, MMM d, yyyy',
  Week: "'Week of' MMM d, yyyy",
  Month: 'MMMM yyyy',
  Year: 'yyyy',
};

/** Axis tick: "Oct 3", "Oct ’26", "2026". */
export const formatTick = (start: string, grouping: CashFlowGrouping) =>
  format(parseISO(start), TICK_FORMAT[grouping]);

/** Tooltip and drill-down heading: "Week of Oct 3, 2026", "October 2026". */
export const formatPeriod = (start: string, grouping: CashFlowGrouping) =>
  format(parseISO(start), LABEL_FORMAT[grouping]);

/** Relative change, or null when there is nothing to compare against. */
export const percentChange = (current: number, previous: number): number | null =>
  previous === 0 ? null : (current - previous) / Math.abs(previous);

/** Share of money in that was kept. Null when nothing came in — a rate of nothing is no rate. */
export const savingsRate = (totals: CashFlowTotals): number | null =>
  totals.moneyIn > 0 ? totals.net / totals.moneyIn : null;

/** Largest income sources, with everything past `limit` folded into "Other" — as selectTopSpend does. */
export const selectTopIncome = (sources: IncomeSource[], limit: number): SpendSlice[] => {
  const ranked = sources.filter((s) => s.amount > 0).sort((a, b) => b.amount - a.amount);
  const top: SpendSlice[] = ranked
    .slice(0, limit)
    // Prefixed so a source literally named "Other" can't collide with the fold-in row.
    .map((s) => ({ key: `source:${s.source}`, label: s.source, value: s.amount }));
  const otherTotal = ranked.slice(limit).reduce((sum, s) => sum + s.amount, 0);
  if (otherTotal > 0) top.push({ key: 'other', label: 'Other', value: otherTotal });
  return top;
};
