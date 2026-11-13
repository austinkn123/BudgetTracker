import { useEffect, useRef } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { dataVersionService } from '../services/dataVersion.service';

export const DATA_VERSION_QUERY_KEY = ['data-version'] as const;

/** How often a focused tab asks whether anything changed. One tiny request, whatever is on screen. */
const CHECK_INTERVAL_MS = 20_000;

/** Top-level query keys holding budget data the server's data version covers. Webull is not one. */
const LIVE_PREFIXES = new Set([
  'transactions',
  'budget-analysis',
  'cash-flow',
  'money-map',
  'categories',
  'budgetPlans',
  'plaid',
]);

/** Whether a query reads budget data that a data-version change should refresh. */
export const isLiveDataQuery = (queryKey: readonly unknown[]): boolean => {
  if (!LIVE_PREFIXES.has(queryKey[0] as string)) return false;
  // The feed's infinite query is refreshed only through its head (one request; the head effect resets the feed).
  if (queryKey[0] === 'transactions' && queryKey[1] === 'feed') return queryKey[queryKey.length - 1] === 'head';
  return true;
};

/**
 * Keeps every on-screen budget figure fresh with one cheap check instead of per-query polling.
 * Mounted once, in the authenticated shell.
 *
 * The first version seen (or the cached one, on remount) is only a baseline. When a later check
 * returns a different version, every live budget query is invalidated: active ones refetch, the rest
 * refetch when next shown. A failed check changes nothing and shows nothing; the next one catches up.
 */
export const useDataVersionSync = (): void => {
  const queryClient = useQueryClient();
  const baseline = useRef<number | null>(null);

  const { data } = useQuery({
    queryKey: DATA_VERSION_QUERY_KEY,
    queryFn: dataVersionService.getVersion,
    refetchInterval: CHECK_INTERVAL_MS,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: 'always',
    staleTime: 0,
    retry: false,
  });

  // On error TanStack keeps the last good data, so this only runs on a new successful version.
  const version = data?.version;
  useEffect(() => {
    if (version === undefined) return;
    if (baseline.current === null) {
      baseline.current = version;
      return;
    }
    if (version === baseline.current) return;
    baseline.current = version;
    void queryClient.invalidateQueries({ predicate: (query) => isLiveDataQuery(query.queryKey) });
  }, [version, queryClient]);
};
