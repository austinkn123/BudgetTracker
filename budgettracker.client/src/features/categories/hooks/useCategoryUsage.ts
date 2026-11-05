import { useQuery } from '@tanstack/react-query';
import { categoryService } from '../../../shared/services/category.service';
import type { CategoryUsage } from '../../../shared/types/api';
import { TRANSACTIONS_REFETCH_INTERVAL_MS } from '../../transactions/hooks/useTransactions';

/**
 * Per-category usage since a date. Derived from transactions, so it lives under the
 * 'transactions' key and refreshes whenever a categorize, note or sync invalidates the ledger.
 */
export const useCategoryUsage = (since: string) =>
  useQuery<CategoryUsage[], Error>({
    queryKey: ['transactions', 'category-usage', since],
    queryFn: () => categoryService.getUsage(since),
    retry: 1,
    refetchInterval: TRANSACTIONS_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
  });
