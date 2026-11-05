import { useMemo } from 'react';
import { useInfiniteQuery } from '@tanstack/react-query';
import {
  transactionService,
  type TransactionQuery,
} from '../../../shared/services/transaction.service';
import type { Transaction } from '../../../shared/types/api';
import { TRANSACTIONS_REFETCH_INTERVAL_MS } from './useTransactions';

export const FEED_PAGE_SIZE = 50;

/**
 * The whole ledger as a newest-first feed, one page at a time. Filters are part of the key, so
 * changing a chip or the search starts a fresh feed from the top.
 */
export const useTransactionFeed = (query: TransactionQuery, { enabled = true } = {}) => {
  const feed = useInfiniteQuery({
    queryKey: ['transactions', 'feed', query],
    queryFn: ({ pageParam }) =>
      transactionService.getPage(query, { cursor: pageParam, limit: FEED_PAGE_SIZE }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    enabled,
    retry: 1,
    refetchInterval: TRANSACTIONS_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
  });

  // A row whose date moves between page loads (pending to posted) could straddle a page boundary
  // and arrive twice; keep the first, so React keys stay unique.
  const transactions = useMemo<Transaction[]>(() => {
    const seen = new Set<number>();
    return (feed.data?.pages ?? []).flatMap((page) =>
      page.items.filter((t) => {
        if (seen.has(t.id)) return false;
        seen.add(t.id);
        return true;
      }),
    );
  }, [feed.data]);

  return { ...feed, transactions };
};
