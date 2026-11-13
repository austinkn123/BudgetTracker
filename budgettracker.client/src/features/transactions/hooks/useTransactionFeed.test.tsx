import { act, renderHook, waitFor } from '@testing-library/react';
import { focusManager, type QueryClient } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { transactionService } from '../../../shared/services/transaction.service';
import type { Transaction, TransactionPage } from '../../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { FEED_PAGE_SIZE, useTransactionFeed } from './useTransactionFeed';
import { isLiveDataQuery } from '../../../shared/hooks/useDataVersionSync';

vi.mock('../../../shared/services/transaction.service', () => ({
  transactionService: { getFiltered: vi.fn(), getPage: vi.fn() },
}));

const getPage = vi.mocked(transactionService.getPage);

const tx = (id: number): Transaction => ({
  id,
  accountId: 1,
  transactionType: 'Expense',
  categoryId: 1,
  amount: -10,
  occurredAt: '2026-11-01T00:00:00Z',
  createdAt: '2026-11-01T00:00:00Z',
});

const FIRST: TransactionPage = { items: [tx(3), tx(2)], nextCursor: 'c1' };
const SECOND: TransactionPage = { items: [tx(1)], nextCursor: null };
/** The first page after a sync landed a new row on top. */
const FIRST_AFTER_SYNC: TransactionPage = { items: [tx(4), tx(3)], nextCursor: 'c1b' };

/** Serves the ledger page by page; `head` is whatever the first page currently is. */
const serve = (head: () => TransactionPage) =>
  getPage.mockImplementation(async (_query, page = {}) => (page.cursor ? SECOND : head()));

const renderFeed = (queryClient = createTestQueryClient()) =>
  renderHook(() => useTransactionFeed({}), { wrapper: createQueryWrapper(queryClient) });

/** A feed scrolled two pages deep. */
const loadTwoPages = async () => {
  const queryClient = createTestQueryClient();
  const hook = renderFeed(queryClient);
  await waitFor(() => expect(hook.result.current.transactions).toHaveLength(2));
  await act(() => hook.result.current.fetchNextPage());
  await waitFor(() => expect(hook.result.current.transactions).toHaveLength(3));
  getPage.mockClear();
  return { ...hook, queryClient };
};

/** What useDataVersionSync does when the server's data version moves. */
const dataVersionChanged = (queryClient: QueryClient) =>
  act(() => queryClient.invalidateQueries({ predicate: (q) => isLiveDataQuery(q.queryKey) }));

const refocus = () =>
  act(() => {
    focusManager.setFocused(false);
    focusManager.setFocused(true);
  });

beforeEach(() => {
  getPage.mockReset();
});

afterEach(() => {
  focusManager.setFocused(undefined);
  vi.useRealTimers();
});

describe('useTransactionFeed background refresh', () => {
  it('opens the feed with one request', async () => {
    serve(() => FIRST);

    const { result } = renderFeed();

    await waitFor(() => expect(result.current.transactions).toHaveLength(2));
    // Give a second (head) read the chance to fire, if one were going to.
    await act(async () => {});
    expect(getPage).toHaveBeenCalledTimes(1);
    expect(getPage).toHaveBeenCalledWith({}, { cursor: undefined, limit: FEED_PAGE_SIZE });
  });

  it('does not re-read anything on focus; the data-version check owns that', async () => {
    serve(() => FIRST);
    const { result } = await loadTwoPages();

    refocus();
    await act(async () => {});

    expect(getPage).not.toHaveBeenCalled();
    expect(result.current.transactions.map((t) => t.id)).toEqual([3, 2, 1]);
  });

  it('does not poll on a timer', async () => {
    // Faked before mount so any poll timer would be armed on the fake clock; time still flows for waitFor.
    vi.useFakeTimers({ shouldAdvanceTime: true });
    serve(() => FIRST);
    await loadTwoPages();

    await act(() => vi.advanceTimersByTimeAsync(10 * 60_000));

    expect(getPage).not.toHaveBeenCalled();
  });

  it('re-reads only the first page on a data-version change, and keeps the loaded pages when nothing changed', async () => {
    serve(() => FIRST);
    const { result, queryClient } = await loadTwoPages();

    await dataVersionChanged(queryClient);

    await waitFor(() => expect(getPage).toHaveBeenCalledTimes(1));
    expect(getPage).toHaveBeenCalledWith({}, { limit: FEED_PAGE_SIZE });
    expect(result.current.transactions.map((t) => t.id)).toEqual([3, 2, 1]);
    expect(result.current.hasNextPage).toBe(false);
  });

  it('resets to the fresh first page on a data-version change when page 1 differs', async () => {
    let head = FIRST;
    serve(() => head);
    const { result, queryClient } = await loadTwoPages();

    head = FIRST_AFTER_SYNC;
    await dataVersionChanged(queryClient);

    await waitFor(() => expect(result.current.transactions.map((t) => t.id)).toEqual([4, 3]));
    expect(getPage).toHaveBeenCalledTimes(1);
    // Scrolling on continues from the fresh page's own cursor.
    expect(result.current.hasNextPage).toBe(true);
  });

  it('still re-reads every loaded page when the ledger is invalidated after an edit', async () => {
    serve(() => FIRST);
    const queryClient = createTestQueryClient();
    const { result } = renderFeed(queryClient);
    await waitFor(() => expect(result.current.transactions).toHaveLength(2));
    await act(() => result.current.fetchNextPage());
    await waitFor(() => expect(result.current.transactions).toHaveLength(3));
    getPage.mockClear();

    await act(() => queryClient.invalidateQueries({ queryKey: ['transactions'] }));

    // The user's own edit can sit on any loaded page, so their place is kept and every page re-read.
    await waitFor(() => expect(getPage).toHaveBeenCalledWith({}, { cursor: 'c1', limit: FEED_PAGE_SIZE }));
    expect(result.current.transactions.map((t) => t.id)).toEqual([3, 2, 1]);
  });
});
