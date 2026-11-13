import { useQuery } from '@tanstack/react-query';
import { plaidService } from '../../../shared/services/plaid.service';
import type { PlaidConnectionView } from '../../../shared/types/api';

/** Shared cache key for every active Plaid institution. Invalidate this after link/sync/disconnect. */
export const PLAID_CONNECTIONS_QUERY_KEY = ['plaid', 'connections'] as const;

/**
 * Read-only list of linked institutions. The Settings page and the Transactions page both read
 * through this hook so they share one cache entry. Server-side auto-sync (webhook, sync-on-open) surfaces
 * through the app-wide data-version check (useDataVersionSync), not a poll of its own.
 */
export const usePlaidConnections = () =>
  useQuery<PlaidConnectionView[], Error>({
    queryKey: PLAID_CONNECTIONS_QUERY_KEY,
    queryFn: plaidService.getConnections,
    staleTime: 30_000,
  });
