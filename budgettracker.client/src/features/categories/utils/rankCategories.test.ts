import { describe, expect, it } from 'vitest';
import type { BudgetPlan, Category, Transaction } from '../../../shared/types/api';
import { rankCategories } from './rankCategories';

const category = (id: number, name: string, categoryType = 'Expense'): Category => ({
  id,
  userId: 1,
  name,
  categoryType,
});

const tx = (categoryId: number | null, amount: number, occurredAt = '2026-10-15'): Transaction =>
  ({ id: Math.random(), accountId: 1, transactionType: 'Expense', categoryId, amount, occurredAt, createdAt: occurredAt }) as Transaction;

const plan = (isActive: boolean, categoryIds: number[]): BudgetPlan =>
  ({ id: Math.random(), isActive, entries: categoryIds.map((categoryId) => ({ categoryId })) }) as unknown as BudgetPlan;

const SINCE = new Date('2025-11-01');
const categories = [
  category(1, 'Groceries'),
  category(2, 'Mortgage'),
  category(3, 'Refunds', 'Both'),
  category(4, 'Paycheck', 'Income'),
  category(5, 'Card payment', 'Transfer'),
  category(6, 'Gym'),
];

describe('rankCategories', () => {
  it('ranks spending by money out, netting refunds and skipping rows before the window', () => {
    const rows = rankCategories(
      categories,
      [
        tx(1, -100),
        tx(1, 20), // a refund lowers Groceries
        tx(2, -3000),
        tx(3, -50),
        tx(1, -999, '2025-10-31'), // outside the 12 months, but still counts as usage
        tx(null, -40),
      ],
      [],
      'spending',
      SINCE,
    );

    expect(rows.map((r) => [r.category.name, r.total, r.transactions])).toEqual([
      ['Mortgage', 3000, 1],
      ['Groceries', 80, 3],
      ['Refunds', 50, 1],
      ['Gym', 0, 0],
    ]);
  });

  it('keeps income and transfers to their own segments', () => {
    const transactions = [tx(4, 5000), tx(5, -800), tx(5, 800), tx(1, -10)];

    expect(rankCategories(categories, transactions, [], 'income', SINCE).map((r) => [r.category.name, r.total])).toEqual([
      ['Paycheck', 5000],
    ]);
    // Transfers move both ways, so they rank by how much moved.
    expect(rankCategories(categories, transactions, [], 'transfers', SINCE).map((r) => [r.category.name, r.total])).toEqual([
      ['Card payment', 1600],
    ]);
  });

  it('counts plan lines across every plan, but only the active plan decides "in plan"', () => {
    const rows = rankCategories(categories, [], [plan(true, [2]), plan(false, [1, 2])], 'spending', SINCE);
    const byName = Object.fromEntries(rows.map((r) => [r.category.name, r]));

    expect(byName.Mortgage).toMatchObject({ planEntries: 2, inActivePlan: true });
    expect(byName.Groceries).toMatchObject({ planEntries: 1, inActivePlan: false });
    expect(byName.Gym).toMatchObject({ planEntries: 0, inActivePlan: false });
  });
});
