import { useEffect, useMemo } from 'react';
import { useInfiniteQuery, useQuery, useQueryClient, type InfiniteData } from '@tanstack/react-query';
import {
  transactionService,
  type TransactionQuery,
} from '../../../shared/services/transaction.service';
import type { Transaction, TransactionPage } from '../../../shared/types/api';
export const FEED_PAGE_SIZE = 50;

/** How long a just-seeded head counts as fresh, so enabling it does not repeat the feed's own read. */
const HEAD_STALE_MS = 60_000;

/** Same rows, same order, same content, same continuation: nothing for the feed to show. */
const samePage = (a: TransactionPage, b: TransactionPage) =>
  a.nextCursor === b.nextCursor && JSON.stringify(a.items) === JSON.stringify(b.items);

/**
 * The whole ledger as a newest-first feed, one page at a time. Filters are part of the key, so
 * changing a chip or the search starts a fresh feed from the top.
 *
 * Background freshness costs one request, however deep the feed is scrolled. TanStack would
 * refetch an infinite query by re-reading every loaded page in turn, so the feed itself is never
 * refreshed in the background. A separate "head" query reads only the first page, and is the one
 * the app-wide data-version check (useDataVersionSync) invalidates when the server's data changes;
 * when the head differs from the feed's first page, the feed is reset to it — loaded older pages
 * are dropped and re-read as the user scrolls back down. An explicit invalidation of the ledger
 * (the user's own edit, a sync) still re-reads every loaded page, so their place in the list holds.
 */
export const useTransactionFeed = (query: TransactionQuery, { enabled = true } = {}) => {
  const queryClient = useQueryClient();
  const feedKey = useMemo(() => ['transactions', 'feed', query] as const, [query]);
  const headKey = useMemo(() => [...feedKey, 'head'] as const, [feedKey]);

  const feed = useInfiniteQuery({
    queryKey: feedKey,
    queryFn: async ({ pageParam }) => {
      const page = await transactionService.getPage(query, { cursor: pageParam, limit: FEED_PAGE_SIZE });
      // The feed's own first-page read is the freshest head there is; seeding it keeps the head
      // query from repeating the request the moment it is enabled.
      if (pageParam === undefined) queryClient.setQueryData(headKey, page);
      return page;
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    enabled,
    retry: 1,
    // Only the head query below is refreshed in the background; an invalidation still refetches the feed.
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });

  const head = useQuery({
    queryKey: headKey,
    queryFn: () => transactionService.getPage(query, { limit: FEED_PAGE_SIZE }),
    enabled: enabled && feed.isSuccess,
    retry: 1,
    // No timer and no focus refetch: the head is refreshed by the data-version check, which
    // invalidates it (and only it) when anything changed. The stale window keeps a just-seeded head
    // from being re-read the moment it is enabled.
    staleTime: HEAD_STALE_MS,
    refetchOnWindowFocus: false,
  });

  const firstPage = feed.data?.pages[0];
  useEffect(() => {
    // Mid-fetch the feed's pages are about to be replaced; the next settle re-checks.
    if (!head.data || !firstPage || feed.isFetching) return;
    if (samePage(head.data, firstPage)) return;
    queryClient.setQueryData<InfiniteData<TransactionPage, string | undefined>>(feedKey, {
      pages: [head.data],
      pageParams: [undefined],
    });
  }, [head.data, firstPage, feed.isFetching, feedKey, queryClient]);

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
