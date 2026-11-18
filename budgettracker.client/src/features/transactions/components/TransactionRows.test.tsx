import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { Category, Transaction } from '../../../shared/types/api';
import TransactionRows from './TransactionRows';

const GROCERIES: Category = { id: 1, userId: 3, name: 'Groceries', categoryType: 'Expense', plaidCategoryPrimary: 'FOOD_AND_DRINK' };
const DINING: Category = { id: 2, userId: 3, name: 'Dining', categoryType: 'Expense' };

const tx = (categoryId: number | null): Transaction => ({
  id: 7,
  accountId: 1,
  transactionType: 'Expense',
  categoryId,
  amount: -12,
  occurredAt: '2026-11-01T00:00:00',
  createdAt: '2026-11-01T00:00:00',
  payee: 'Corner Cafe',
  plaidCategoryPrimary: 'FOOD_AND_DRINK',
});

const renderRow = (transaction: Transaction) =>
  render(
    <TransactionRows
      transactions={[transaction]}
      categories={[GROCERIES, DINING]}
      maskByPlaidAccountId={new Map()}
      selectedIds={new Set()}
      onToggleSelected={vi.fn()}
      onCategoryChange={vi.fn()}
      onNotesChange={vi.fn()}
      isBusy={false}
    />,
  );

describe('TransactionRows: Plaid suggestion (BUD-9)', () => {
  it('offers the suggested category on an uncategorized row', () => {
    renderRow(tx(null));
    expect(screen.getByRole('button', { name: 'Use Groceries' })).toBeInTheDocument();
  });

  it("keeps Plaid's original suggestion visible after it is overridden", () => {
    renderRow(tx(DINING.id));
    expect(screen.queryByRole('button', { name: 'Use Groceries' })).not.toBeInTheDocument();
    expect(screen.getByText('Plaid suggested Groceries')).toBeInTheDocument();
  });

  it('says nothing extra when the suggestion was accepted', () => {
    renderRow(tx(GROCERIES.id));
    expect(screen.queryByText(/Plaid suggested/)).not.toBeInTheDocument();
  });
});
