import { useMemo } from 'react';
import { format } from 'date-fns';
import { useLatestTransactions, useTransactions } from '../../transactions/hooks/useTransactions';
import type { DateRangeKey } from './useDateRange';

/**
 * "All time" on the dashboard reads the newest rows, not the ledger. Recent activity shows 8 and
 * each category card 10, so 200 (the server's page cap) fills both for anyone but a category that
 * has gone quiet for hundreds of transactions.
 */
export const ALL_TIME_LATEST_LIMIT = 200;

/**
 * The transactions behind the dashboard's range-scoped lists (recent activity, category drills).
 *
 * A bounded range is read server-side by calendar date, inclusive, as the client filter it
 * replaces was. "All time" has no real bound — its 1970–9999 window would be the whole ledger —
 * so it reads the newest page instead.
 */
export const useWindowTransactions = (range: DateRangeKey, start: Date, end: Date) => {
  const bounded = range !== 'all';
  const query = useMemo(
    () => ({ from: format(start, 'yyyy-MM-dd'), to: format(end, 'yyyy-MM-dd') }),
    [start, end],
  );

  const windowed = useTransactions(query, { enabled: bounded });
  const latest = useLatestTransactions(ALL_TIME_LATEST_LIMIT, { enabled: !bounded });

  return bounded ? windowed : latest;
};
