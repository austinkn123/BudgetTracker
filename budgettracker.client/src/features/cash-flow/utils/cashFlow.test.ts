import { describe, expect, it } from 'vitest';
import {
  compareWindow,
  defaultGrouping,
  formatTick,
  percentChange,
  periodBounds,
  rangeWindow,
  savingsRate,
  selectTopIncome,
  toDateKey,
  type DateSpan,
  type DateWindow,
} from './cashFlow';

const d = (iso: string) => new Date(`${iso}T00:00:00`);
const keys = (span: DateSpan | DateWindow | null) =>
  span && { from: span.from && toDateKey(span.from), to: toDateKey(span.to) };

const TODAY = d('2026-10-20');
const CUSTOM = { from: d('2026-06-10'), to: d('2026-06-19') };

describe('rangeWindow', () => {
  it('ends every range today and starts multi-month ranges on the 1st', () => {
    expect(keys(rangeWindow('month', TODAY, CUSTOM))).toEqual({ from: '2026-10-01', to: '2026-10-20' });
    expect(keys(rangeWindow('3m', TODAY, CUSTOM))).toEqual({ from: '2026-08-01', to: '2026-10-20' });
    expect(keys(rangeWindow('ytd', TODAY, CUSTOM))).toEqual({ from: '2026-01-01', to: '2026-10-20' });
    expect(keys(rangeWindow('12m', TODAY, CUSTOM))).toEqual({ from: '2025-11-01', to: '2026-10-20' });
    expect(keys(rangeWindow('all', TODAY, CUSTOM))).toEqual({ from: null, to: '2026-10-20' });
    expect(keys(rangeWindow('custom', TODAY, CUSTOM))).toEqual({ from: '2026-06-10', to: '2026-06-19' });
  });
});

describe('defaultGrouping', () => {
  it('follows the range', () => {
    expect(defaultGrouping('month', rangeWindow('month', TODAY, CUSTOM))).toBe('Day');
    expect(defaultGrouping('3m', rangeWindow('3m', TODAY, CUSTOM))).toBe('Week');
    expect(defaultGrouping('ytd', rangeWindow('ytd', TODAY, CUSTOM))).toBe('Month');
    expect(defaultGrouping('12m', rangeWindow('12m', TODAY, CUSTOM))).toBe('Month');
    expect(defaultGrouping('all', rangeWindow('all', TODAY, CUSTOM))).toBe('Month');
  });

  it('sizes a custom range by its length, inclusive of both ends', () => {
    const custom = (from: string, to: string) => defaultGrouping('custom', { from: d(from), to: d(to) });
    expect(custom('2026-01-01', '2026-01-31')).toBe('Day'); // 31 days
    expect(custom('2026-01-01', '2026-02-01')).toBe('Week'); // 32 days
    expect(custom('2026-01-01', '2026-04-30')).toBe('Week'); // 120 days
    expect(custom('2026-01-01', '2026-05-01')).toBe('Month'); // 121 days
  });
});

describe('compareWindow', () => {
  it('compares this month with the same days of last month', () => {
    expect(keys(compareWindow('month', rangeWindow('month', TODAY, CUSTOM)))).toEqual({
      from: '2026-09-01',
      to: '2026-09-20',
    });
  });

  it('clamps at a shorter month end', () => {
    const today = d('2026-03-31');
    expect(keys(compareWindow('month', rangeWindow('month', today, CUSTOM)))).toEqual({
      from: '2026-02-01',
      to: '2026-02-28',
    });
  });

  it('compares 3m and 12m with the span before, and YTD with the same span last year', () => {
    expect(keys(compareWindow('3m', rangeWindow('3m', TODAY, CUSTOM)))).toEqual({
      from: '2026-05-01',
      to: '2026-07-20',
    });
    expect(keys(compareWindow('12m', rangeWindow('12m', TODAY, CUSTOM)))).toEqual({
      from: '2024-11-01',
      to: '2025-10-20',
    });
    expect(keys(compareWindow('ytd', rangeWindow('ytd', TODAY, CUSTOM)))).toEqual({
      from: '2025-01-01',
      to: '2025-10-20',
    });
  });

  it('compares a custom range with the equal-length span right before it', () => {
    expect(keys(compareWindow('custom', CUSTOM))).toEqual({ from: '2026-05-31', to: '2026-06-09' });
  });

  it('clamps to Feb 29 in a leap year', () => {
    const today = d('2028-03-31');
    expect(keys(compareWindow('month', rangeWindow('month', today, CUSTOM)))).toEqual({
      from: '2028-02-01',
      to: '2028-02-29',
    });
  });

  it('shifts a window ending on Feb 29 to Feb 28 of the prior year', () => {
    const today = d('2028-02-29');
    expect(keys(compareWindow('12m', rangeWindow('12m', today, CUSTOM)))).toEqual({
      from: '2026-03-01',
      to: '2027-02-28',
    });
    expect(keys(compareWindow('ytd', rangeWindow('ytd', today, CUSTOM)))).toEqual({
      from: '2027-01-01',
      to: '2027-02-28',
    });
  });

  it('clamps a 3m window ending May 31 to the end of February', () => {
    const today = d('2026-05-31');
    expect(keys(compareWindow('3m', rangeWindow('3m', today, CUSTOM)))).toEqual({
      from: '2025-12-01',
      to: '2026-02-28',
    });
  });

  it('compares YTD on Jan 1 with Jan 1 of last year', () => {
    const today = d('2026-01-01');
    const window = rangeWindow('ytd', today, CUSTOM);
    expect(keys(window)).toEqual({ from: '2026-01-01', to: '2026-01-01' });
    expect(keys(compareWindow('ytd', window))).toEqual({ from: '2025-01-01', to: '2025-01-01' });
  });

  it('counts whole calendar days across a DST change for a custom range', () => {
    // US clocks spring forward Mar 8 2026: still 31 days, so the compare span is 31 days too.
    const march = { from: d('2026-03-01'), to: d('2026-03-31') };
    expect(keys(compareWindow('custom', march))).toEqual({ from: '2026-01-29', to: '2026-02-28' });
  });

  it('has nothing to compare All with', () => {
    expect(compareWindow('all', rangeWindow('all', TODAY, CUSTOM))).toBeNull();
  });
});

describe('periodBounds', () => {
  const bounds = { from: d('2026-08-01'), to: TODAY };

  it('covers a single day', () => {
    expect(keys(periodBounds('2026-08-14', 'Day', bounds))).toEqual({ from: '2026-08-14', to: '2026-08-14' });
  });

  it('covers Sunday to Saturday for a week, clipped to the window', () => {
    expect(keys(periodBounds('2026-08-09', 'Week', bounds))).toEqual({ from: '2026-08-09', to: '2026-08-15' });
    // Aug 1 2026 is a Saturday, so its week starts in July.
    expect(keys(periodBounds('2026-07-26', 'Week', bounds))).toEqual({ from: '2026-08-01', to: '2026-08-01' });
  });

  it('clips a week spanning New Year to the window, and spans a DST change in seven days', () => {
    const winter = { from: d('2026-01-01'), to: d('2026-01-10') };
    expect(keys(periodBounds('2025-12-28', 'Week', winter))).toEqual({ from: '2026-01-01', to: '2026-01-03' });
    const autumn = { from: d('2026-10-01'), to: d('2026-11-30') };
    expect(keys(periodBounds('2026-11-01', 'Week', autumn))).toEqual({ from: '2026-11-01', to: '2026-11-07' });
  });

  it('ends the current month and year today, not at their calendar end', () => {
    expect(keys(periodBounds('2026-09-01', 'Month', bounds))).toEqual({ from: '2026-09-01', to: '2026-09-30' });
    expect(keys(periodBounds('2026-10-01', 'Month', bounds))).toEqual({ from: '2026-10-01', to: '2026-10-20' });
    expect(keys(periodBounds('2026-01-01', 'Year', { from: null, to: TODAY }))).toEqual({
      from: '2026-01-01',
      to: '2026-10-20',
    });
  });
});

describe('formatTick', () => {
  it('formats by grouping', () => {
    expect(formatTick('2026-10-03', 'Day')).toBe('Oct 3');
    expect(formatTick('2026-10-04', 'Week')).toBe('Oct 4');
    expect(formatTick('2026-10-01', 'Month')).toBe('Oct ’26');
    expect(formatTick('2026-01-01', 'Year')).toBe('2026');
  });

  it('accepts a full ISO timestamp as well as a bare date', () => {
    expect(formatTick('2026-10-03T00:00:00', 'Day')).toBe('Oct 3');
  });
});

describe('percentChange and savingsRate', () => {
  it('measures change against the magnitude of the previous value', () => {
    expect(percentChange(120, 100)).toBeCloseTo(0.2);
    expect(percentChange(50, -100)).toBeCloseTo(1.5);
    expect(percentChange(10, 0)).toBeNull();
  });

  it('has no savings rate when nothing came in', () => {
    expect(savingsRate({ moneyIn: 5000, moneyOut: 4000, net: 1000 })).toBeCloseTo(0.2);
    expect(savingsRate({ moneyIn: 0, moneyOut: 40, net: -40 })).toBeNull();
    // A clawed-back paycheck can leave money in negative; dividing by it would invert the sign.
    expect(savingsRate({ moneyIn: -200, moneyOut: 40, net: -240 })).toBeNull();
  });
});

describe('selectTopIncome', () => {
  it('keeps the top sources and folds the rest into Other', () => {
    const rows = selectTopIncome(
      [
        { source: 'Gusto', amount: 5000 },
        { source: 'Venmo', amount: 40 },
        { source: 'Interest', amount: 3 },
        { source: 'Refund', amount: 0 },
      ],
      1,
    );
    expect(rows.map((r) => [r.label, r.value])).toEqual([
      ['Gusto', 5000],
      ['Other', 43],
    ]);
  });
});
