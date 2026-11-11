import { act, renderHook, waitFor } from '@testing-library/react';
import { focusManager } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { transactionService } from '../../../shared/services/transaction.service';
import type { Transaction, TransactionPage } from '../../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { FEED_PAGE_SIZE, useTransactionFeed } from './useTransactionFeed';
import { TRANSACTIONS_REFETCH_INTERVAL_MS } from './useTransactions';

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
  const hook = renderFeed();
  await waitFor(() => expect(hook.result.current.transactions).toHaveLength(2));
  await act(() => hook.result.current.fetchNextPage());
  await waitFor(() => expect(hook.result.current.transactions).toHaveLength(3));
  getPage.mockClear();
  return hook;
};

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

  it('re-reads only the first page on focus, and keeps the loaded pages when nothing changed', async () => {
    serve(() => FIRST);
    const { result } = await loadTwoPages();

    refocus();

    await waitFor(() => expect(getPage).toHaveBeenCalledTimes(1));
    expect(getPage).toHaveBeenCalledWith({}, { cursor: undefined, limit: FEED_PAGE_SIZE });
    expect(result.current.transactions.map((t) => t.id)).toEqual([3, 2, 1]);
    expect(result.current.hasNextPage).toBe(false);
  });

  it('resets to the fresh first page when it changed', async () => {
    let head = FIRST;
    serve(() => head);
    const { result } = await loadTwoPages();

    head = FIRST_AFTER_SYNC;
    refocus();

    await waitFor(() => expect(result.current.transactions.map((t) => t.id)).toEqual([4, 3]));
    expect(getPage).toHaveBeenCalledTimes(1);
    // Scrolling on continues from the fresh page's own cursor.
    expect(result.current.hasNextPage).toBe(true);
  });

  it('polls with one request per interval, however many pages are loaded', async () => {
    // Faked before mount so the poll timer is armed on the fake clock; time still flows for waitFor.
    vi.useFakeTimers({ shouldAdvanceTime: true });
    serve(() => FIRST);
    await loadTwoPages();

    await act(() => vi.advanceTimersByTimeAsync(TRANSACTIONS_REFETCH_INTERVAL_MS));

    expect(getPage).toHaveBeenCalledTimes(1);
    expect(getPage).toHaveBeenCalledWith({}, { cursor: undefined, limit: FEED_PAGE_SIZE });
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
