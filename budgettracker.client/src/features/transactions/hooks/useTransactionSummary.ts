import { useQuery } from '@tanstack/react-query';
import { transactionService } from '../../../shared/services/transaction.service';
import type { TransactionSummary } from '../../../shared/types/api';
import { TRANSACTIONS_REFETCH_INTERVAL_MS } from './useTransactions';

/**
 * Ledger-wide figures (the uncategorized backlog, pending sums per account) computed server-side.
 * Keyed under 'transactions' so every write that invalidates the ledger refreshes these too.
 */
export const useTransactionSummary = () =>
  useQuery<TransactionSummary, Error>({
    queryKey: ['transactions', 'summary'],
    queryFn: transactionService.getSummary,
    retry: 1,
    refetchInterval: TRANSACTIONS_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
  });
