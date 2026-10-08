import { useQuery } from '@tanstack/react-query';
import { plaidService } from '../../../shared/services/plaid.service';
import type { PlaidConnectionView } from '../../../shared/types/api';

/** Shared cache key for every active Plaid institution. Invalidate this after link/sync/disconnect. */
export const PLAID_CONNECTIONS_QUERY_KEY = ['plaid', 'connections'] as const;

/** Poll cadence so server-side auto-sync (webhook + sync-on-open) surfaces without a manual Refresh (BUD-6). */
const CONNECTIONS_REFETCH_INTERVAL_MS = 60_000;

/**
 * Read-only list of linked institutions. The Settings page and the Transactions page both read
 * through this hook so they share one cache entry.
 */
export const usePlaidConnections = () =>
  useQuery<PlaidConnectionView[], Error>({
    queryKey: PLAID_CONNECTIONS_QUERY_KEY,
    queryFn: plaidService.getConnections,
    staleTime: 30_000,
    refetchInterval: CONNECTIONS_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
  });
