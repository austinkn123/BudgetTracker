import { describe, expect, it } from 'vitest';
import type {
  CategoryMonthSpend,
  CategoryPerformance,
  PlanPerformance,
  Transaction,
} from '../../../shared/types/api';
import {
  categoryLabel,
  selectCategoryCards,
  selectDrifting,
  selectRecentActivity,
  selectTopSpend,
  selectWaterfall,
} from './selectors';

const names = new Map([
  [1, 'Groceries'],
  [2, 'Rent'],
  [3, 'Fun'],
]);

const perf = (
  categoryId: number | null,
  planned: number,
  actual: number,
): CategoryPerformance => ({ categoryId, planned, actual, overBy: actual - planned });

const txn = (id: number, categoryId: number | null, occurredAt: string): Transaction => ({
  id,
  accountId: 1,
  transactionType: 'Expense',
  categoryId: categoryId as number,
  amount: -10,
  occurredAt,
  createdAt: occurredAt,
});

const plan = (byCategory: CategoryPerformance[], income = 0, expenses = 0): PlanPerformance => ({
  plan: { id: 1, name: 'June', planMonth: '2026-06-01' },
  pacing: {
    daysElapsed: 15, daysInMonth: 30, daysPct: 0.5, spentPct: 0.5, projectedEnd: 0,
    pacingDelta: 0, status: 'OnTrack', plannedExpenses: 0, actualExpenses: 0,
    remaining: 0, perDiemToStay: 0,
  },
  byCategory,
  byBucket: [],
  income,
  expenses,
});

describe('categoryLabel', () => {
  it('resolves known ids, uncategorized nulls, and unknown ids', () => {
    expect(categoryLabel(names, 1)).toBe('Groceries');
    expect(categoryLabel(names, null)).toBe('Uncategorized');
    expect(categoryLabel(names, 99)).toBe('Category 99');
  });
});

describe('selectDrifting', () => {
  it('keeps only over-budget categories, worst first, capped', () => {
    const result = selectDrifting(
      [perf(1, 10, 30), perf(2, 10, 15), perf(3, 10, 5), perf(null, 0, 100)],
      names,
      3,
    );

    expect(result.map((d) => d.overBy)).toEqual([100, 20, 5]);
    expect(result.map((d) => d.name)).toEqual(['Uncategorized', 'Groceries', 'Rent']);
  });

  it('returns nothing when everything is under budget', () => {
    expect(selectDrifting([perf(1, 100, 20)], names, 3)).toEqual([]);
  });
});

describe('selectTopSpend', () => {
  it('folds everything past the limit into a single Other', () => {
    const result = selectTopSpend(
      [
        { categoryId: 1, amount: 50 },
        { categoryId: 2, amount: 40 },
        { categoryId: 3, amount: 30 },
        { categoryId: null, amount: 20 },
      ],
      names,
      2,
    );

    expect(result.map((s) => s.label)).toEqual(['Groceries', 'Rent', 'Other']);
    expect(result.at(-1)?.value).toBe(50); // 30 + 20
  });

  it('omits Other when nothing overflows, and drops zero-value slices', () => {
    const result = selectTopSpend(
      [{ categoryId: 1, amount: 50 }, { categoryId: 2, amount: 0 }],
      names,
      8,
    );

    expect(result).toHaveLength(1);
    expect(result[0].label).toBe('Groceries');
  });

  it('gives every slice a usable React key', () => {
    const result = selectTopSpend([{ categoryId: null, amount: 5 }], names, 8);
    expect(result[0].key).toBe('uncategorized');
  });
});

describe('selectWaterfall', () => {
  it('orders income, top expenses, Other, then net', () => {
    const result = selectWaterfall(
      plan([perf(1, 0, 60), perf(2, 0, 50), perf(3, 0, 10)], 500, 120),
      names,
      2,
    );

    expect(result.map((b) => b.label)).toEqual(['Income', 'Groceries', 'Rent', 'Other', 'Net']);
    expect(result.map((b) => b.kind)).toEqual(['income', 'expense', 'expense', 'expense', 'net']);
    expect(result[1].signed).toBe(-60); // expenses are negative-signed
  });

  it('renders a deficit as a positive bar height with a negative sign', () => {
    const net = selectWaterfall(plan([perf(1, 0, 300)], 100, 300), names, 5).at(-1)!;

    expect(net.signed).toBe(-200);
    expect(net.value).toBe(200);
  });

  it('renders a surplus as positive on both', () => {
    const net = selectWaterfall(plan([perf(1, 0, 100)], 300, 100), names, 5).at(-1)!;

    expect(net.signed).toBe(200);
    expect(net.value).toBe(200);
  });

  it('skips categories with no actual spend', () => {
    const result = selectWaterfall(plan([perf(1, 500, 0)], 100, 0), names, 5);
    expect(result.map((b) => b.label)).toEqual(['Income', 'Net']);
  });
});

describe('selectCategoryCards', () => {
  const trend: CategoryMonthSpend[] = [
    { categoryId: 1, month: '2026-06-01', income: 0, expenses: 30 },
    { categoryId: 1, month: '2026-04-01', income: 0, expenses: 10 },
    { categoryId: 1, month: '2026-05-01', income: 0, expenses: 20 },
  ];

  it('sorts cards by planned then actual, and orders sparklines oldest-first', () => {
    const result = selectCategoryCards(
      plan([perf(1, 100, 10), perf(2, 100, 90), perf(3, 50, 0)]),
      trend,
      [],
      names,
      10,
    );

    expect(result.map((c) => c.name)).toEqual(['Rent', 'Groceries', 'Fun']);
    expect(result.find((c) => c.name === 'Groceries')?.monthly).toEqual([10, 20, 30]);
  });

  it('attaches only its own transactions, newest first and capped', () => {
    const result = selectCategoryCards(
      plan([perf(1, 100, 10)]),
      [],
      [
        txn(1, 1, '2026-06-01'),
        txn(2, 1, '2026-06-05'),
        txn(3, 1, '2026-06-03'),
        txn(4, 2, '2026-06-09'),
      ],
      names,
      2,
    );

    expect(result[0].transactions.map((t) => t.id)).toEqual([2, 3]);
  });

  it('groups uncategorized transactions under the uncategorized card', () => {
    const result = selectCategoryCards(
      plan([perf(null, 0, 25)]),
      [],
      [txn(1, null, '2026-06-01')],
      names,
      10,
    );

    expect(result[0].name).toBe('Uncategorized');
    expect(result[0].transactions).toHaveLength(1);
  });
});

describe('selectRecentActivity', () => {
  it('returns the newest transactions first, capped', () => {
    const result = selectRecentActivity(
      [txn(1, 1, '2026-06-01'), txn(2, 1, '2026-06-09'), txn(3, 1, '2026-06-05')],
      2,
    );

    expect(result.map((t) => t.id)).toEqual([2, 3]);
  });

  it('does not mutate its input', () => {
    const input = [txn(1, 1, '2026-06-01'), txn(2, 1, '2026-06-09')];
    selectRecentActivity(input, 2);
    expect(input.map((t) => t.id)).toEqual([1, 2]);
  });
});
