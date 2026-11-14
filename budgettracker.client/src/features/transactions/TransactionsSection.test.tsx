import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { transactionService, type TransactionQuery } from '../../shared/services/transaction.service';
import type { Category, PlaidConnectionView, Transaction } from '../../shared/types/api';
import { ALLY, CHASE } from '../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../test/renderWithQueryClient';
import TransactionsSection from './TransactionsSection';

const plaid = vi.hoisted(() => ({ connections: [] as PlaidConnectionView[] }));
const feed = vi.hoisted(() => ({
  queries: [] as TransactionQuery[],
  // One stable array per test: a fresh one each render would re-run the selection-pruning effect.
  transactions: [] as Transaction[],
}));
const catalog = vi.hoisted(() => ({ categories: [] as Category[] }));

vi.mock('../linked-accounts/hooks/usePlaidConnections', () => ({
  usePlaidConnections: () => ({ data: plaid.connections }),
}));
vi.mock('./hooks/useTransactionFeed', () => ({
  useTransactionFeed: (query: TransactionQuery) => {
    feed.queries.push(query);
    return {
      transactions: feed.transactions,
      isLoading: false,
      isError: false,
      hasNextPage: false,
      isFetchingNextPage: false,
      isFetchNextPageError: false,
      fetchNextPage: vi.fn(),
      refetch: vi.fn(),
    };
  },
}));
vi.mock('./hooks/useTransactions', () => ({
  useTransactions: () => ({ data: [], isLoading: false, error: null }),
}));
vi.mock('./hooks/useTransactionSummary', () => ({
  useTransactionSummary: () => ({ data: { uncategorizedCount: 0, pendingByAccount: [] } }),
}));
vi.mock('../categories/hooks/useCategories', () => ({
  useCategories: () => ({ data: catalog.categories, isLoading: false, error: null }),
}));
vi.mock('../budget-plans/hooks/useBudgetPlans', () => ({
  useBudgetPlans: () => ({ data: [], isLoading: false, error: null }),
}));
vi.mock('../dashboard/hooks/useBudgetAnalysis', () => ({
  useBudgetAnalysis: () => ({ data: undefined, isLoading: false, error: null }),
}));

// Radix Select relies on pointer-capture and scrollIntoView, which jsdom does not implement.
beforeAll(() => {
  Element.prototype.hasPointerCapture ??= () => false;
  Element.prototype.releasePointerCapture ??= () => undefined;
  Element.prototype.scrollIntoView ??= () => undefined;
});

beforeEach(() => {
  feed.queries = [];
  feed.transactions = [];
  catalog.categories = [];
});

const renderListView = async () => {
  const user = userEvent.setup();
  render(
    <TransactionsSection isLoading={false} setStatusMessage={vi.fn()} setStatusError={vi.fn()} />,
    { wrapper: createQueryWrapper(createTestQueryClient()) },
  );
  await user.click(screen.getByRole('radio', { name: 'List' }));
  return user;
};

const accountFilter = () => screen.queryByRole('combobox', { name: 'Filter by account' });

describe('TransactionsSection account filter', () => {
  it('is hidden when only one account is linked — it would have nothing to choose between', async () => {
    plaid.connections = [CHASE];
    await renderListView();

    expect(screen.getByRole('searchbox', { name: 'Search transactions' })).toBeInTheDocument();
    expect(accountFilter()).not.toBeInTheDocument();
  });

  it('is shown once two accounts are linked, defaulting to all accounts', async () => {
    plaid.connections = [CHASE, ALLY];
    await renderListView();

    expect(accountFilter()).toHaveTextContent('All accounts');
    expect(feed.queries.at(-1)).not.toHaveProperty('plaidAccountId');
  });

  it('narrows the feed to the chosen account', async () => {
    plaid.connections = [CHASE, ALLY];
    const user = await renderListView();

    await user.click(accountFilter()!);
    await user.click(await screen.findByRole('option', { name: 'Chase ••1234' }));

    await waitFor(() => expect(feed.queries.at(-1)).toEqual({ plaidAccountId: 'acc-chase-1' }));
    expect(accountFilter()).toHaveTextContent('Chase ••1234');
  });
});

const GROCERIES: Category = { id: 7, userId: 1, name: 'Groceries', categoryType: 'Expense' };

const txn = (id: number, payee: string, categoryId: number | null): Transaction => ({
  id,
  accountId: 1,
  transactionType: 'Expense',
  categoryId,
  amount: -12.5,
  occurredAt: '2026-11-02T00:00:00Z',
  payee,
  createdAt: '2026-11-02T00:00:00Z',
});

describe('TransactionsSection bulk category picker', () => {
  const bulkPicker = () =>
    screen.getByRole('combobox', { name: 'Set category for selected transactions' });

  const selectBoth = async () => {
    plaid.connections = [CHASE];
    catalog.categories = [GROCERIES];
    feed.transactions = [txn(1, 'Corner Shop', 7), txn(2, 'Bakery', 7)];
    const setCategoryBulk = vi.spyOn(transactionService, 'setCategoryBulk').mockResolvedValue(2);
    const user = await renderListView();
    await user.click(screen.getByRole('checkbox', { name: 'Select Corner Shop' }));
    await user.click(screen.getByRole('checkbox', { name: 'Select Bakery' }));
    return { user, setCategoryBulk };
  };

  it('shows its placeholder, not a value — it applies a choice rather than holding one', async () => {
    await selectBoth();

    expect(bulkPicker()).toHaveTextContent('Set category…');
  });

  it('clears the category on every selected row when "Uncategorized" is picked', async () => {
    const { user, setCategoryBulk } = await selectBoth();

    await user.click(bulkPicker());
    await user.click(await screen.findByRole('option', { name: 'Uncategorized' }));

    await waitFor(() => expect(setCategoryBulk).toHaveBeenCalledExactlyOnceWith([1, 2], null));
  });

  it('applies the picked category to every selected row', async () => {
    const { user, setCategoryBulk } = await selectBoth();

    await user.click(bulkPicker());
    await user.click(await screen.findByRole('option', { name: 'Groceries' }));

    await waitFor(() => expect(setCategoryBulk).toHaveBeenCalledExactlyOnceWith([1, 2], 7));
  });
});
