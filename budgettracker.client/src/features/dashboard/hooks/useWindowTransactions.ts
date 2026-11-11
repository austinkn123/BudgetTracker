import { format } from 'date-fns';
import type { TransactionQuery } from '../../../shared/services/transaction.service';
import { useTransactionPage } from '../../transactions/hooks/useTransactions';
import type { DateRangeKey } from './useDateRange';

/**
 * The dashboard range as a server filter. A bounded range is read by calendar date, inclusive.
 * "All time" has no real bound — its 1970–9999 window would only restate "everything" — so it
 * sends no dates at all.
 */
export const windowFilter = (range: DateRangeKey, start: Date, end: Date): TransactionQuery =>
  range === 'all' ? {} : { from: format(start, 'yyyy-MM-dd'), to: format(end, 'yyyy-MM-dd') };

/**
 * The newest `limit` rows in the dashboard range, for recent activity. Only what is shown is read:
 * one capped page, newest first, whatever the range.
 */
export const useWindowTransactions = (range: DateRangeKey, start: Date, end: Date, limit: number) =>
  useTransactionPage(windowFilter(range, start, end), limit);

/**
 * One category's newest `limit` rows in the dashboard range, for its drill card. Each card reads
 * its own rows, so a quiet category still finds its last purchases however far back they sit —
 * a shared "newest N" page would show it empty beside a non-zero spend. A null category is the
 * uncategorized card.
 */
export const useCategoryDrillTransactions = (
  categoryId: number | null,
  rangeFilter: TransactionQuery,
  limit: number,
  { enabled = true }: { enabled?: boolean } = {},
) =>
  useTransactionPage(
    categoryId === null ? { ...rangeFilter, uncategorized: true } : { ...rangeFilter, categoryId },
    limit,
    { enabled },
  );
