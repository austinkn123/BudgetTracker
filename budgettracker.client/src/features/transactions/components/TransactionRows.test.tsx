import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { Category, Transaction } from '../../../shared/types/api';
import TransactionRows from './TransactionRows';

const GROCERIES: Category = {
  id: 7,
  userId: 1,
  name: 'Groceries',
  categoryType: 'Expense',
  plaidCategoryPrimary: 'FOOD_AND_DRINK',
};
const DINING: Category = { id: 9, userId: 1, name: 'Dining out', categoryType: 'Expense' };

const txn = (overrides: Partial<Transaction>): Transaction => ({
  id: 1,
  accountId: 1,
  transactionType: 'Expense',
  categoryId: null,
  amount: -12.5,
  occurredAt: '2026-11-02T00:00:00Z',
  payee: 'Corner Shop',
  createdAt: '2026-11-02T00:00:00Z',
  isImported: true,
  ...overrides,
});

const renderRow = (transaction: Transaction, onCategoryChange = vi.fn()) => {
  render(
    <TransactionRows
      transactions={[transaction]}
      categories={[GROCERIES, DINING]}
      maskByPlaidAccountId={new Map()}
      selectedIds={new Set()}
      onToggleSelected={vi.fn()}
      onCategoryChange={onCategoryChange}
      onNotesChange={vi.fn()}
      isBusy={false}
    />,
  );
  return onCategoryChange;
};

describe('TransactionRows: Plaid suggestion (BUD-9)', () => {
  it('offers a mapped suggestion as a one-click accept on an uncategorized row', async () => {
    const user = userEvent.setup();
    const onCategoryChange = renderRow(txn({ plaidCategoryPrimary: 'FOOD_AND_DRINK' }));

    await user.click(screen.getByRole('button', { name: 'Use Groceries' }));

    expect(onCategoryChange).toHaveBeenCalledExactlyOnceWith(1, 7);
  });

  it("shows Plaid's own label when no category claims the suggestion", () => {
    renderRow(txn({ plaidCategoryPrimary: 'GENERAL_MERCHANDISE' }));

    expect(screen.getByText('Plaid says General merchandise')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Use / })).not.toBeInTheDocument();
  });

  it('keeps the original suggestion visible after the user overrides it', () => {
    renderRow(txn({ plaidCategoryPrimary: 'FOOD_AND_DRINK', categoryId: DINING.id }));

    expect(screen.getByText('Plaid suggested Groceries')).toBeInTheDocument();
    // An override is final: no accept button pulling the row back to the suggestion.
    expect(screen.queryByRole('button', { name: 'Use Groceries' })).not.toBeInTheDocument();
  });

  it("keeps Plaid's own label visible after an override when no category claims it", () => {
    renderRow(txn({ plaidCategoryPrimary: 'GENERAL_MERCHANDISE', categoryId: DINING.id }));

    expect(screen.getByText('Plaid suggested General merchandise')).toBeInTheDocument();
  });

  it('says nothing extra once the user accepted the suggestion', () => {
    renderRow(txn({ plaidCategoryPrimary: 'FOOD_AND_DRINK', categoryId: GROCERIES.id }));

    expect(screen.queryByText(/Plaid sugg|Plaid says/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Use / })).not.toBeInTheDocument();
  });

  it('shows no Plaid line for a row Plaid gave no suggestion', () => {
    renderRow(txn({ plaidCategoryPrimary: null, categoryId: DINING.id }));

    expect(screen.queryByText(/Plaid/)).not.toBeInTheDocument();
  });
});
