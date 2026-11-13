import { useQuery } from '@tanstack/react-query';
import {
  transactionService,
  type TransactionQuery,
} from '../../../shared/services/transaction.service';
import type { Transaction } from '../../../shared/types/api';

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
  });
};

/**
 * The newest `limit` rows matching `query`, as one page (GET /transactions/page, newest first). For
 * a screen that wants "the latest few" — of a range, a category, or all history — rather than every
 * row a filter matches. An empty query is allowed here: one capped page is never the whole ledger.
 */
export const useTransactionPage = (
  query: TransactionQuery,
  limit: number,
  { enabled = true }: ReadOptions = {},
) => {
  return useQuery<Transaction[], Error>({
    queryKey: ['transactions', 'page', query, limit],
    queryFn: async () => (await transactionService.getPage(query, { limit })).items,
    enabled,
    retry: 1,
  });
};
