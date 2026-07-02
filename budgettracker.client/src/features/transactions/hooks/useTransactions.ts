import { useQuery } from '@tanstack/react-query';
import { transactionService } from '../../../shared/services/transaction.service';
import type { Transaction } from '../../../shared/types/api';

/** Poll cadence so server-side auto-synced rows surface without a manual Refresh (BUD-6). */
const TRANSACTIONS_REFETCH_INTERVAL_MS = 60_000;

export const useTransactions = () => {
  return useQuery<Transaction[], Error>({
    queryKey: ['transactions'],
    queryFn: () => transactionService.getCurrentUserTransactions(),
    retry: 1,
    refetchInterval: TRANSACTIONS_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
  });
};
