import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { TransactionQuery } from '../../shared/services/transaction.service';
import type { PlaidConnectionView } from '../../shared/types/api';
import { ALLY, CHASE } from '../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../test/renderWithQueryClient';
import TransactionsSection from './TransactionsSection';

const plaid = vi.hoisted(() => ({ connections: [] as PlaidConnectionView[] }));
const feed = vi.hoisted(() => ({ queries: [] as TransactionQuery[] }));

vi.mock('../linked-accounts/hooks/usePlaidConnections', () => ({
  usePlaidConnections: () => ({ data: plaid.connections }),
}));
vi.mock('./hooks/useTransactionFeed', () => ({
  useTransactionFeed: (query: TransactionQuery) => {
    feed.queries.push(query);
    return {
      transactions: [],
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
  useCategories: () => ({ data: [], isLoading: false, error: null }),
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
