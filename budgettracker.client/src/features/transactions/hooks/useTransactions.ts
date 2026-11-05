import { useQuery } from '@tanstack/react-query';
import {
  transactionService,
  type TransactionQuery,
} from '../../../shared/services/transaction.service';
import type { Transaction } from '../../../shared/types/api';

/** Poll cadence so server-side auto-synced rows surface without a manual Refresh (BUD-6). */
export const TRANSACTIONS_REFETCH_INTERVAL_MS = 60_000;

type ReadOptions = {
  /** Off while the view that reads it is not on screen. */
  enabled?: boolean;
};

/**
 * A bounded slice of the ledger, filtered server-side: a month, a date range, the backlog. Every
 * caller must pass at least one filter — the server refuses the whole ledger in one response. For
 * the open-ended history use `useTransactionFeed`.
 */
export const useTransactions = (query: TransactionQuery, { enabled = true }: ReadOptions = {}) => {
  return useQuery<Transaction[], Error>({
    // Query is part of the key so each filter combination caches independently.
    queryKey: ['transactions', query],
    queryFn: () => transactionService.getFiltered(query),
    enabled,
    retry: 1,
    refetchInterval: TRANSACTIONS_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
  });
};

/**
 * The newest `limit` rows across all history, as one page. For a screen that wants "the latest"
 * with no natural date bound, rather than the ledger.
 */
export const useLatestTransactions = (limit: number, { enabled = true }: ReadOptions = {}) => {
  return useQuery<Transaction[], Error>({
    queryKey: ['transactions', 'latest', limit],
    queryFn: async () => (await transactionService.getPage({}, { limit })).items,
    enabled,
    retry: 1,
    refetchInterval: TRANSACTIONS_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
  });
};
