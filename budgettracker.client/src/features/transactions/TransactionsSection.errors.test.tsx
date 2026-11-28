import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { format } from 'date-fns';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { BudgetPlan } from '../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../test/renderWithQueryClient';
import TransactionsSection from './TransactionsSection';

/** What each mocked read returns this test: a settled success, or a failure with nothing cached. */
const reads = vi.hoisted(() => {
  const read = () => ({
    data: undefined as unknown,
    isError: false,
    isFetching: false,
    isLoading: false,
    error: null as Error | null,
    refetch: vi.fn(),
  });
  return { transactions: read(), analysis: read(), plans: read(), categories: read() };
});

const fail = (r: (typeof reads)['transactions']) => {
  r.data = undefined;
  r.isError = true;
  r.error = new Error('500');
};

vi.mock('../linked-accounts/hooks/usePlaidConnections', () => ({
  usePlaidConnections: () => ({ data: [] }),
}));
vi.mock('./hooks/useTransactionFeed', () => ({
  useTransactionFeed: () => ({
    transactions: [],
    isLoading: false,
    isError: false,
    hasNextPage: false,
    isFetchingNextPage: false,
    isFetchNextPageError: false,
    fetchNextPage: vi.fn(),
    refetch: vi.fn(),
  }),
}));
vi.mock('./hooks/useTransactions', () => ({ useTransactions: () => reads.transactions }));
vi.mock('./hooks/useTransactionSummary', () => ({
  useTransactionSummary: () => ({ data: { uncategorizedCount: 0, pendingByAccount: [] } }),
}));
vi.mock('../categories/hooks/useCategories', () => ({ useCategories: () => reads.categories }));
vi.mock('../budget-plans/hooks/useBudgetPlans', () => ({ useBudgetPlans: () => reads.plans }));
vi.mock('../dashboard/hooks/useBudgetAnalysis', () => ({ useBudgetAnalysis: () => reads.analysis }));

beforeAll(() => {
  Element.prototype.hasPointerCapture ??= () => false;
  Element.prototype.releasePointerCapture ??= () => undefined;
  Element.prototype.scrollIntoView ??= () => undefined;
});

const PLAN: BudgetPlan = {
  id: 1,
  name: 'My plan',
  isActive: true,
  planMonth: '2020-01-01T00:00:00',
  lines: [{ id: 1, categoryId: 7, amount: 400, bucket: 'Core' }],
} as unknown as BudgetPlan;

beforeEach(() => {
  for (const r of Object.values(reads)) {
    r.data = undefined;
    r.isError = false;
    r.isFetching = false;
    r.error = null;
    r.refetch = vi.fn();
  }
  reads.transactions.data = [];
  reads.plans.data = [PLAN];
  reads.categories.data = [{ id: 7, userId: 1, name: 'Groceries', categoryType: 'Expense' }];
});

const renderSection = () =>
  render(<TransactionsSection isLoading={false} setStatusMessage={vi.fn()} setStatusError={vi.fn()} />, {
    wrapper: createQueryWrapper(createTestQueryClient()),
  });

describe('TransactionsSection when a read fails', () => {
  it('shows an error in the Plan view instead of totalling the month at $0', async () => {
    fail(reads.transactions);
    renderSection();

    const alert = screen.getByText("Couldn't load this month's transactions").closest('[role="alert"]') as HTMLElement;
    expect(alert).toHaveTextContent("Couldn't load this month's transactions");
    // An empty successful read renders each bucket as "$0" and "Nothing in Core this month".
    expect(document.body.textContent).not.toMatch(/\$0/);
    expect(screen.queryByText('Nothing in Core this month.')).not.toBeInTheDocument();

    await userEvent.click(within(alert).getByRole('button', { name: 'Try again' }));
    expect(reads.transactions.refetch).toHaveBeenCalledOnce();
  });

  it('shows an error when the plans fail, since rows would otherwise be grouped against no plan', () => {
    fail(reads.plans);
    renderSection();

    expect(screen.getByText("Couldn't load this month's transactions")).toBeInTheDocument();
  });

  it('only refetches the reads that failed', async () => {
    fail(reads.plans);
    renderSection();

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(reads.plans.refetch).toHaveBeenCalledOnce();
    expect(reads.transactions.refetch).not.toHaveBeenCalled();
    expect(reads.categories.refetch).not.toHaveBeenCalled();
  });

  it('says the tracking failed rather than that no plan governs the month', async () => {
    fail(reads.analysis);
    renderSection();

    const month = format(new Date(), 'MMMM');
    expect(screen.getByText(`Couldn't load how ${month} is tracking`)).toBeInTheDocument();
    expect(screen.queryByText(/No budget plan governs/)).not.toBeInTheDocument();

    const header = screen.getByText(`Couldn't load how ${month} is tracking`).closest('[role="alert"]')!;
    await userEvent.click(within(header as HTMLElement).getByRole('button', { name: 'Try again' }));
    expect(reads.analysis.refetch).toHaveBeenCalledOnce();
  });

  it('still says no plan governs the month when the analysis loads with none', () => {
    reads.analysis.data = { planMonth: null };
    renderSection();

    expect(screen.getByText(/No budget plan governs/)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows an error in the Calendar view instead of an empty month', async () => {
    fail(reads.transactions);
    renderSection();

    await userEvent.click(screen.getByRole('radio', { name: 'Calendar' }));

    expect(screen.getByText("Couldn't load this month's transactions")).toBeInTheDocument();
  });
});
