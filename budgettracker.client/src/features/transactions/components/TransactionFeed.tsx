import type { ComponentProps } from 'react';
import { Button, Card, Skeleton, Spinner } from '../../../shared/components/ui';
import { useInViewCallback } from '../../../shared/hooks/useInViewCallback';
import type { Transaction } from '../../../shared/types/api';
import TransactionRows from './TransactionRows';

type RowProps = Omit<ComponentProps<typeof TransactionRows>, 'transactions'>;

export type TransactionFeedProps = {
  /** Every row loaded so far, newest first. */
  transactions: Transaction[];
  /** The first page has not arrived yet. */
  isLoading: boolean;
  /** The first page failed, so there is nothing to show. */
  isError: boolean;
  hasNextPage: boolean;
  isFetchingNextPage: boolean;
  isFetchNextPageError: boolean;
  fetchNextPage: () => void;
  onRetry: () => void;
  allSelected: boolean;
  onToggleAll: (selected: boolean) => void;
  /** A status chip or search is narrowing the feed, which changes how its end reads. */
  isFiltered: boolean;
  rowProps: RowProps;
};

const statusRowClass = 'flex items-center justify-center gap-2 px-4 py-4 text-sm text-ink-muted';

const plural = (n: number) => `${n} ${n === 1 ? 'transaction' : 'transactions'}`;

/**
 * The ledger as one newest-first list over all history. The next page loads as the bottom of the
 * list nears the viewport, and the same step is a button for anyone not scrolling.
 *
 * Until the last page is in, nobody knows the total, so the header counts what is loaded and
 * "Select" acts on those rows only — a bulk write never reaches a row you have not seen.
 */
const TransactionFeed = ({
  transactions,
  isLoading,
  isError,
  hasNextPage,
  isFetchingNextPage,
  isFetchNextPageError,
  fetchNextPage,
  onRetry,
  allSelected,
  onToggleAll,
  isFiltered,
  rowProps,
}: TransactionFeedProps) => {
  const sentinelRef = useInViewCallback(fetchNextPage, {
    // A failed page waits for the button; retrying on every scroll would hammer a broken server.
    enabled: hasNextPage && !isFetchingNextPage && !isFetchNextPageError,
  });

  const complete = !hasNextPage;
  const count = transactions.length;

  if (isLoading) {
    return (
      <Card padding="none">
        <div aria-busy="true" aria-label="Loading transactions" className="divide-y divide-border-subtle">
          {Array.from({ length: 6 }, (_, i) => (
            <div key={i} className="flex items-center gap-4 px-4 py-3">
              <Skeleton width={`${30 + ((i * 13) % 25)}%`} height={12} />
              <Skeleton width={72} height={12} className="ml-auto" />
            </div>
          ))}
        </div>
      </Card>
    );
  }

  if (isError && count === 0) {
    return (
      <Card padding="none">
        <div className={statusRowClass}>
          <span>Couldn&apos;t load your transactions.</span>
          <Button variant="ghost" size="sm" onClick={onRetry}>
            Try again
          </Button>
        </div>
      </Card>
    );
  }

  return (
    <Card padding="none">
      <div className="flex items-center justify-between border-b border-border px-4 py-2.5">
        <span className="text-xs font-medium uppercase tracking-wide text-ink-muted">
          {complete ? plural(count) : `${count} loaded`}
        </span>
        {count > 0 && (
          <Button variant="ghost" size="sm" onClick={() => onToggleAll(!allSelected)}>
            {allSelected ? 'Deselect all' : complete ? 'Select all' : 'Select loaded'}
          </Button>
        )}
      </div>

      <TransactionRows transactions={transactions} {...rowProps} />

      {count > 0 && (
        <div className="border-t border-border-subtle">
          {isFetchNextPageError ? (
            <div className={statusRowClass}>
              <span role="status">Couldn&apos;t load older transactions.</span>
              <Button variant="ghost" size="sm" onClick={onRetry}>
                Try again
              </Button>
            </div>
          ) : isFetchingNextPage ? (
            <p role="status" className={statusRowClass}>
              <Spinner size={16} />
              Loading older transactions…
            </p>
          ) : hasNextPage ? (
            <div ref={sentinelRef} className={statusRowClass}>
              <Button variant="ghost" size="sm" onClick={fetchNextPage}>
                Load older transactions
              </Button>
            </div>
          ) : (
            <p role="status" className={statusRowClass}>
              {isFiltered
                ? 'No older transactions match this view.'
                : 'That’s the start of your history.'}
            </p>
          )}
        </div>
      )}
    </Card>
  );
};

export default TransactionFeed;
