import { useQuery } from '@tanstack/react-query';
import {
  transactionService,
  type TransactionQuery,
} from '../../../shared/services/transaction.service';
import type { Transaction } from '../../../shared/types/api';

/** Poll cadence so server-side auto-synced rows surface without a manual Refresh (BUD-6). */
const TRANSACTIONS_REFETCH_INTERVAL_MS = 60_000;

/**
 * The ledger. Pass a query to filter server-side — the calendar view wants everything for its
 * month tinting, while the review list asks for a narrowed slice.
 */
export const useTransactions = (query: TransactionQuery = {}) => {
  return useQuery<Transaction[], Error>({
    // Query is part of the key so each filter combination caches independently.
    queryKey: ['transactions', query],
    queryFn: () => transactionService.getFiltered(query),
    retry: 1,
    refetchInterval: TRANSACTIONS_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
  });
};
