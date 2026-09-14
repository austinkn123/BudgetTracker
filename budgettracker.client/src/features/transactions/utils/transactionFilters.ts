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

/** Build the full query for the review list: status chip + search text + visible month. */
export const buildListQuery = (
  status: TransactionStatusFilter,
  search: string,
  monthStart: Date,
  monthEnd: Date,
): TransactionQuery => ({
  ...statusToQuery(status),
  ...(search.trim() ? { search: search.trim() } : {}),
  from: monthStart.toISOString(),
  to: monthEnd.toISOString(),
});
