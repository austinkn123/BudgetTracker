import { describe, expect, it } from 'vitest';
import type { Transaction } from '../../../shared/types/api';
import {
  buildTransactionDaySummaries,
  groupUncategorizedByPayee,
  isInflowTransaction,
} from './transactionGroups';

const txn = (over: Partial<Transaction> = {}): Transaction => ({
  id: 1,
  accountId: 77,
  transactionType: 'Expense',
  categoryId: 10,
  amount: -50,
  occurredAt: '2026-04-07T00:00:00',
  createdAt: '2026-04-07T00:00:00',
  ...over,
});

describe('isInflowTransaction', () => {
  it('follows the sign of the amount, not the transaction type', () => {
    expect(isInflowTransaction(txn({ transactionType: 'Income', amount: 2823 }))).toBe(true);
    expect(isInflowTransaction(txn({ transactionType: 'Expense', amount: -50 }))).toBe(false);
    // A refund arrives as an Expense row with a positive amount.
    expect(isInflowTransaction(txn({ transactionType: 'Expense', amount: 20 }))).toBe(true);
    // Own-account transfers go either way.
    expect(isInflowTransaction(txn({ transactionType: 'Transfer', amount: 500 }))).toBe(true);
    expect(isInflowTransaction(txn({ transactionType: 'Transfer', amount: -500 }))).toBe(false);
    expect(isInflowTransaction(txn({ transactionType: 'Adjustment', amount: -5 }))).toBe(false);
  });
});

describe('buildTransactionDaySummaries', () => {
  it('counts a refund as money in for the day', () => {
    const day = buildTransactionDaySummaries([
      txn({ id: 1, amount: -120 }),
      txn({ id: 2, amount: 20 }),
    ]).get('2026-04-07')!;

    expect(day.incomeTotal).toBe(20);
    expect(day.outflowTotal).toBe(120);
    expect(day.netTotal).toBe(-100);
  });
});

describe('groupUncategorizedByPayee', () => {
  it('groups uncategorized rows by payee, case-insensitively, most rows first', () => {
    const groups = groupUncategorizedByPayee([
      txn({ id: 1, categoryId: null, payee: 'Target', amount: -20 }),
      txn({ id: 2, categoryId: null, payee: 'Amazon', amount: -30 }),
      txn({ id: 3, categoryId: null, payee: 'amazon', amount: 10 }),
      txn({ id: 4, categoryId: 10, payee: 'Amazon', amount: -99 }),
      txn({ id: 5, categoryId: null, amount: -5 }),
    ]);

    expect(groups.map((g) => [g.payee, g.ids, g.netTotal])).toEqual([
      ['Amazon', [2, 3], -20],
      ['Target', [1], -20],
      ['No payee', [5], -5],
    ]);
  });
});
