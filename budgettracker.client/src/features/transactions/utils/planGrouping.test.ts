import { describe, expect, it } from 'vitest';
import type { BudgetPlan, BudgetPlanEntry, Transaction } from '../../../shared/types/api';
import { buildCategoryPlanIndex, groupByPlan, spendStatus } from './planGrouping';

const entry = (over: Partial<BudgetPlanEntry> = {}): BudgetPlanEntry => ({
  id: 1,
  budgetPlanId: 1,
  categoryId: 10,
  lineType: 'Expense',
  bucket: 'Core',
  cadence: 'Monthly',
  amount: 400,
  monthlyEquivalent: 400,
  isStressFactor: false,
  sortOrder: 10,
  createdAt: '2026-04-01T00:00:00Z',
  ...over,
});

const plan = (entries: BudgetPlanEntry[]): BudgetPlan => ({
  id: 1,
  userId: 1,
  name: 'Baseline Budget',
  planMonth: '2026-04-01',
  netIncomeMonthly: 5646,
  isActive: true,
  createdAt: '2026-04-01T00:00:00Z',
  entries,
});

const txn = (over: Partial<Transaction> = {}): Transaction => ({
  id: 1,
  accountId: 77,
  transactionType: 'Expense',
  categoryId: 10,
  amount: -50,
  occurredAt: '2026-04-07T00:00:00Z',
  createdAt: '2026-04-07T00:00:00Z',
  ...over,
});

const names = new Map([
  [10, 'Food'],
  [20, 'Home maintenance'],
  [30, 'Gas + Tolls'],
]);

describe('buildCategoryPlanIndex', () => {
  it('uses monthlyEquivalent, not amount, for annual lines', () => {
    // $2,004/yr is $167/month. Comparing spend against $2,004 would hide a real overspend.
    const { plannedByCategory } = buildCategoryPlanIndex([
      entry({ categoryId: 20, bucket: 'Buffer', cadence: 'Annual', amount: 2004, monthlyEquivalent: 167 }),
    ]);

    expect(plannedByCategory.get(20)).toBe(167);
  });

  it('sums multiple entries sharing a category', () => {
    const { plannedByCategory } = buildCategoryPlanIndex([
      entry({ id: 1, monthlyEquivalent: 400 }),
      entry({ id: 2, monthlyEquivalent: 50 }),
    ]);

    expect(plannedByCategory.get(10)).toBe(450);
  });

  it('ignores Income lines and entries with no category', () => {
    const { plannedByCategory } = buildCategoryPlanIndex([
      entry({ categoryId: 99, lineType: 'Income', monthlyEquivalent: 5646 }),
      entry({ categoryId: null, monthlyEquivalent: 100 }),
    ]);

    expect(plannedByCategory.size).toBe(0);
  });
});

describe('groupByPlan', () => {
  it('splits categories into their planned buckets', () => {
    const result = groupByPlan(
      [txn({ id: 1, categoryId: 10, amount: -120 })],
      plan([
        entry({ categoryId: 10, bucket: 'Core', monthlyEquivalent: 400 }),
        entry({ id: 2, categoryId: 20, bucket: 'Buffer', monthlyEquivalent: 167 }),
      ]),
      names,
    );

    const core = result.buckets.find((b) => b.bucket === 'Core')!;
    const buffer = result.buckets.find((b) => b.bucket === 'Buffer')!;

    expect(core.categories.map((c) => c.categoryId)).toEqual([10]);
    expect(buffer.categories.map((c) => c.categoryId)).toEqual([20]);
    expect(core.planned).toBe(400);
    expect(core.actual).toBe(120);
  });

  it('drops unplanned spend into Buffer, matching the engine', () => {
    // The philosophy: you commit to Core, everything else eats Buffer.
    const result = groupByPlan(
      [txn({ id: 1, categoryId: 30, amount: -75 })],
      plan([entry({ categoryId: 10, bucket: 'Core', monthlyEquivalent: 400 })]),
      names,
    );

    const buffer = result.buckets.find((b) => b.bucket === 'Buffer')!;
    expect(buffer.categories.map((c) => c.categoryId)).toEqual([30]);
    expect(buffer.categories[0].planned).toBe(0);
    expect(buffer.categories[0].overBy).toBe(75);
  });

  it('pins uncategorised spend outside the buckets', () => {
    const result = groupByPlan(
      [txn({ id: 1, categoryId: null, amount: -30 }), txn({ id: 2, categoryId: null, amount: -20 })],
      plan([entry()]),
      names,
    );

    expect(result.uncategorized).not.toBeNull();
    expect(result.uncategorized!.actual).toBe(50);
    expect(result.uncategorized!.transactions).toHaveLength(2);
    // It must not be silently folded into Buffer, or the work becomes invisible.
    const buffer = result.buckets.find((b) => b.bucket === 'Buffer')!;
    expect(buffer.actual).toBe(0);
  });

  it('is null for uncategorised when there is none', () => {
    const result = groupByPlan([txn()], plan([entry()]), names);
    expect(result.uncategorized).toBeNull();
  });

  it('accumulates magnitudes because amounts arrive signed', () => {
    const result = groupByPlan(
      [txn({ id: 1, amount: -120 }), txn({ id: 2, amount: -80 })],
      plan([entry({ monthlyEquivalent: 400 })]),
      names,
    );

    expect(result.buckets.find((b) => b.bucket === 'Core')!.actual).toBe(200);
  });

  it('counts only Expense rows toward actuals', () => {
    const result = groupByPlan(
      [txn({ id: 1, amount: -120 }), txn({ id: 2, transactionType: 'Income', amount: 2823 })],
      plan([entry({ monthlyEquivalent: 400 })]),
      names,
    );

    expect(result.buckets.find((b) => b.bucket === 'Core')!.actual).toBe(120);
  });

  it('keeps a planned category with no spend', () => {
    const result = groupByPlan([], plan([entry({ monthlyEquivalent: 400 })]), names);

    const core = result.buckets.find((b) => b.bucket === 'Core')!;
    expect(core.categories).toHaveLength(1);
    expect(core.categories[0].actual).toBe(0);
    expect(core.categories[0].overBy).toBe(-400);
  });

  it('sorts worst overspend first', () => {
    const result = groupByPlan(
      [txn({ id: 1, categoryId: 10, amount: -500 }), txn({ id: 2, categoryId: 30, amount: -60 })],
      plan([
        entry({ categoryId: 10, bucket: 'Core', monthlyEquivalent: 400 }),
        entry({ id: 2, categoryId: 30, bucket: 'Core', monthlyEquivalent: 50 }),
      ]),
      names,
    );

    const core = result.buckets.find((b) => b.bucket === 'Core')!;
    expect(core.categories.map((c) => c.categoryId)).toEqual([10, 30]);
  });

  it('prefers the server figure for planned when supplied', () => {
    const result = groupByPlan([txn({ amount: -100 })], plan([entry({ monthlyEquivalent: 400 })]), names, [
      { categoryId: 10, planned: 425, actual: 100, overBy: -325 },
    ]);

    expect(result.buckets.find((b) => b.bucket === 'Core')!.categories[0].planned).toBe(425);
  });

  it('always returns exactly two buckets, even with no plan', () => {
    const result = groupByPlan([], null, names);
    expect(result.buckets.map((b) => b.bucket)).toEqual(['Core', 'Buffer']);
  });
});

describe('spendStatus', () => {
  it('reads neutral when nothing is planned', () => {
    // Unbudgeted spend is Buffer spend, which is allowed — not a failure state.
    expect(spendStatus(0, 500)).toBe('none');
  });

  it('walks the four-step ramp', () => {
    expect(spendStatus(100, 100)).toBe('on-pace');
    expect(spendStatus(100, 110)).toBe('warn-low');
    expect(spendStatus(100, 135)).toBe('warn-high');
    expect(spendStatus(100, 200)).toBe('over');
  });

  it('treats exactly on budget as on-pace, not over', () => {
    expect(spendStatus(400, 400)).toBe('on-pace');
    expect(spendStatus(400, 400.01)).toBe('warn-low');
  });
});
