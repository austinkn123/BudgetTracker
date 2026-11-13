import { describe, expect, it } from 'vitest';
import type { BudgetPlan, Category, CategoryUsage } from '../../../shared/types/api';
import { rankCategories, usageSince } from './rankCategories';

const category = (id: number, name: string, categoryType = 'Expense'): Category => ({
  id,
  userId: 1,
  name,
  categoryType,
});

/** One server usage row: all-time count, plus signed in/out sums since the window start. */
const usage = (categoryId: number, transactionCount: number, inflowSince: number, outflowSince: number): CategoryUsage => ({
  categoryId,
  transactionCount,
  inflowSince,
  outflowSince,
});

const plan = (isActive: boolean, categoryIds: number[]): BudgetPlan =>
  ({ id: Math.random(), isActive, entries: categoryIds.map((categoryId) => ({ categoryId })) }) as unknown as BudgetPlan;

const categories = [
  category(1, 'Groceries'),
  category(2, 'Mortgage'),
  category(3, 'Refunds', 'Both'),
  category(4, 'Paycheck', 'Income'),
  category(5, 'Card payment', 'Transfer'),
  category(6, 'Gym'),
];

describe('rankCategories', () => {
  it('ranks spending by money out, netting refunds, with usage counted across all time', () => {
    const rows = rankCategories(
      categories,
      [
        // Groceries: -100 and a +20 refund in the window, plus a -999 row from before it.
        usage(1, 3, 20, -100),
        usage(2, 1, 0, -3000),
        usage(3, 1, 0, -50),
      ],
      [],
      'spending',
    );

    expect(rows.map((r) => [r.category.name, r.total, r.transactions])).toEqual([
      ['Mortgage', 3000, 1],
      ['Groceries', 80, 3],
      ['Refunds', 50, 1],
      ['Gym', 0, 0],
    ]);
  });

  it('keeps income and transfers to their own segments', () => {
    const rows = [usage(4, 1, 5000, 0), usage(5, 2, 800, -800), usage(1, 1, 0, -10)];

    expect(rankCategories(categories, rows, [], 'income').map((r) => [r.category.name, r.total])).toEqual([
      ['Paycheck', 5000],
    ]);
    // Transfers move both ways, so they rank by how much moved.
    expect(rankCategories(categories, rows, [], 'transfers').map((r) => [r.category.name, r.total])).toEqual([
      ['Card payment', 1600],
    ]);
  });

  it('counts a category used only before the window, with nothing to rank it by', () => {
    const rows = rankCategories(categories, [usage(6, 4, 0, 0)], [], 'spending');

    expect(rows.find((r) => r.category.name === 'Gym')).toMatchObject({ total: 0, transactions: 4 });
  });

  it('ignores usage for a category it does not list', () => {
    const rows = rankCategories(categories, [usage(99, 7, 0, -500)], [], 'spending');

    expect(rows.every((r) => r.transactions === 0 && r.total === 0)).toBe(true);
  });

  it('counts plan lines across every plan, but only the active plan decides "in plan"', () => {
    const rows = rankCategories(categories, [], [plan(true, [2]), plan(false, [1, 2])], 'spending');
    const byName = Object.fromEntries(rows.map((r) => [r.category.name, r]));

    expect(byName.Mortgage).toMatchObject({ planEntries: 2, inActivePlan: true });
    expect(byName.Groceries).toMatchObject({ planEntries: 1, inActivePlan: false });
    expect(byName.Gym).toMatchObject({ planEntries: 0, inActivePlan: false });
  });
});

describe('usageSince', () => {
  // The window used to be "occurredAt >= now minus 12 months", compared as instants. Rows sit at
  // midnight, so the first day that passed was the day after that instant's date.
  it('starts the day after the date 12 months back, once the day is under way', () => {
    expect(usageSince(new Date(2026, 10, 5, 14, 30))).toBe('2025-11-06');
  });

  it('includes that date itself at exactly midnight', () => {
    expect(usageSince(new Date(2026, 10, 5))).toBe('2025-11-05');
  });
});
