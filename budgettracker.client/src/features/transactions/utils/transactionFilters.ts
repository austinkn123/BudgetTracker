import { addDays, format, startOfMonth, startOfWeek } from 'date-fns';
import type { TransactionQuery } from '../../../shared/services/transaction.service';

export type TransactionStatusFilter = 'all' | 'uncategorized' | 'pending';

/**
 * Translate a status chip into the server-side filter. Kept separate from the component so the
 * mapping is unit-testable — a wrong flag here silently shows the user the wrong ledger slice.
 */
export const statusToQuery = (status: TransactionStatusFilter): TransactionQuery => {
  switch (status) {
    case 'uncategorized':
      return { uncategorized: true };
    case 'pending':
      return { isPending: true };
    case 'all':
    default:
      return {};
  }
};

/**
 * Build the full query for a month: status chip + search text + month bounds.
 *
 * Bounds are sent as calendar dates (`yyyy-MM-dd`), never `toISOString()`. `OccurredAt` is stored
 * as a timezone-free calendar date at midnight, so an instant would shift the window for anyone
 * outside UTC — in UTC−5 an April query serialised to `2026-04-01T05:00:00Z`, dropping an April 1
 * row and letting a May 1 row in. A bare date has no such offset to lose.
 */
export const buildListQuery = (
  status: TransactionStatusFilter,
  search: string,
  monthStart: Date,
  monthEnd: Date,
): TransactionQuery => ({
  ...statusToQuery(status),
  ...(search.trim() ? { search: search.trim() } : {}),
  from: format(monthStart, 'yyyy-MM-dd'),
  to: format(monthEnd, 'yyyy-MM-dd'),
});

/**
 * The List feed's filters: status chip + search, and no dates. The feed is the whole history,
 * newest first, so the month switcher has no say in it.
 */
export const buildFeedQuery = (
  status: TransactionStatusFilter,
  search: string,
): TransactionQuery => ({
  ...statusToQuery(status),
  ...(search.trim() ? { search: search.trim() } : {}),
});

/** The Calendar's grid is always six weeks (`fixedWeeks`), Sunday first, as react-day-picker draws it. */
const CALENDAR_GRID_DAYS = 42;

/**
 * Every day the calendar grid shows for the month containing `day`, outside days included, so a
 * selected outside day still has its transactions. Calendar-date bounds, as in `buildListQuery`.
 */
export const buildCalendarQuery = (day: Date): TransactionQuery => {
  const gridStart = startOfWeek(startOfMonth(day));
  return {
    from: format(gridStart, 'yyyy-MM-dd'),
    to: format(addDays(gridStart, CALENDAR_GRID_DAYS - 1), 'yyyy-MM-dd'),
  };
};
