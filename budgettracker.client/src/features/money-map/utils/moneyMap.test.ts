import { describe, expect, it } from 'vitest';
import type { MoneyMap, MoneyMapLine } from '../../../shared/types/api';
import { biggestOverrun, billsSetAside, groupLines, lineStatus, splitMonth, timeline } from './moneyMap';

const line = (over: Partial<MoneyMapLine>): MoneyMapLine => ({
  categoryId: 1,
  name: 'Line',
  bucket: 'Core',
  cadence: 'Monthly',
  planMonthly: 100,
  actualMonthly: 100,
  topPayees: [],
  notes: null,
  ...over,
});

const map = (lines: MoneyMapLine[]): MoneyMap => ({
  planName: 'Plan',
  from: '2025-11-01',
  to: '2026-10-30',
  months: 12,
  plannedIncomeMonthly: 6526,
  actualIncomeMonthly: 6600,
  lines,
  upcoming: [],
});

describe('splitMonth', () => {
  it('splits plan and actual into must-pay, flexible (with unplanned) and what is left to save', () => {
    const result = splitMonth(
      map([
        line({ bucket: 'Core', planMonthly: 4000, actualMonthly: 3900 }),
        line({ bucket: 'Buffer', planMonthly: 1000, actualMonthly: 1200 }),
        line({ bucket: null, planMonthly: 0, actualMonthly: 50 }),
      ]),
    );

    expect(result.plan).toEqual({ mustPay: 4000, flexible: 1000, saving: 1526 });
    expect(result.actual).toEqual({ mustPay: 3900, flexible: 1250, saving: 1376 });
  });
});

describe('lineStatus', () => {
  it.each([
    [line({ planMonthly: 400, actualMonthly: 522 }), 'warning', '$122 over'],
    [line({ planMonthly: 400, actualMonthly: 410 }), 'success', 'On track'],
    [line({ planMonthly: 3160, actualMonthly: 2913 }), 'success', '$247 under'],
    [line({ cadence: 'Annual', planMonthly: 157, actualMonthly: 0 }), 'info', 'Set aside monthly'],
    [line({ bucket: null, planMonthly: 0, actualMonthly: 17 }), 'warning', 'Not in the plan'],
  ])('%#', (input, tone, label) => {
    expect(lineStatus(input)).toEqual({ tone, label });
  });
});

describe('groupLines', () => {
  it('puts over-plan lines first within each group and drops empty groups', () => {
    const groups = groupLines([
      line({ name: 'Mortgage', planMonthly: 3160, actualMonthly: 3160 }),
      line({ name: 'Groceries', planMonthly: 425, actualMonthly: 401 }),
      line({ name: 'Eating out', bucket: 'Buffer', planMonthly: 400, actualMonthly: 522 }),
      line({ name: 'Shopping', bucket: 'Buffer', planMonthly: 225, actualMonthly: 296 }),
      line({ name: 'Travel', bucket: 'Buffer', planMonthly: 150, actualMonthly: 0 }),
      line({ name: 'Groceries spike', planMonthly: 50, actualMonthly: 90 }),
    ]);

    expect(groups.map((g) => [g.label, g.lines.map((l) => l.name)])).toEqual([
      ['Must-pay', ['Groceries spike', 'Mortgage', 'Groceries']],
      ['Flexible', ['Eating out', 'Shopping', 'Travel']],
    ]);
  });
});

describe('biggestOverrun / billsSetAside', () => {
  it('finds the line furthest over plan and the monthly set-aside for annual bills', () => {
    const lines = [
      line({ name: 'Eating out', planMonthly: 400, actualMonthly: 522 }),
      line({ name: 'Shopping', planMonthly: 225, actualMonthly: 296 }),
      line({ name: 'HOA', cadence: 'Annual', planMonthly: 76.33, actualMonthly: 78 }),
      line({ name: 'Car insurance', cadence: 'Annual', planMonthly: 157.17, actualMonthly: 235 }),
    ];

    expect(biggestOverrun(lines)?.name).toBe('Eating out');
    expect(billsSetAside(lines)).toBeCloseTo(233.5, 2);
  });
});

describe('timeline', () => {
  it('spans a year of months ahead and files each item under its month', () => {
    const months = timeline(
      [
        { date: '2026-11-15T00:00:00', label: 'Car registration', amount: 207, kind: 'Bill' },
        { date: '2027-10-29T00:00:00', label: 'Extra paycheck', amount: 3262.8, kind: 'ExtraPaycheck' },
      ],
      new Date(2026, 9, 30),
    );

    expect(months).toHaveLength(12);
    expect(months[0].month).toEqual(new Date(2026, 10, 1));
    expect(months[0].items.map((i) => i.label)).toEqual(['Car registration']);
    expect(months[11].items.map((i) => i.label)).toEqual(['Extra paycheck']);
  });
});
