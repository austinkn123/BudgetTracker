import { useMemo } from 'react';
import { format, parseISO } from 'date-fns';
import { X } from 'lucide-react';
import { Button, Card, Skeleton } from '../../../shared/components/ui';
import type { CashFlowGrouping, CashFlowPeriod, Category } from '../../../shared/types/api';
import { currency, signedAmount } from '../../../shared/utils/format';
import { cn } from '../../../shared/utils/cn';
import { useTransactions } from '../../transactions/hooks/useTransactions';
import { isInflowTransaction } from '../../transactions/utils/transactionGroups';
import { categoryLabel } from '../../dashboard/utils/selectors';
import { formatPeriod, toDateKey, type DateSpan } from '../utils/cashFlow';

/** The bar counts Income and Expense rows only — never transfers or balance adjustments. */
const BAR_TYPES = new Set(['Income', 'Expense']);

interface PeriodTransactionsProps {
  period: CashFlowPeriod;
  grouping: CashFlowGrouping;
  /** The bucket's dates, already clipped to the report window. */
  bounds: DateSpan;
  plaidAccountId: string | null;
  categoryId: number | null;
  search: string;
  categories: Category[];
  onClear: () => void;
}

/**
 * The transactions behind one bar, under the same filters as the report. Pending rows, transfers
 * and adjustments are left out here too, so the list adds up to the bar above it.
 * Mounted only while a bar is selected, so nothing is fetched until then.
 */
const PeriodTransactions = ({
  period,
  grouping,
  bounds,
  plaidAccountId,
  categoryId,
  search,
  categories,
  onClear,
}: PeriodTransactionsProps) => {
  const { data = [], isLoading, error } = useTransactions({
    from: toDateKey(bounds.from),
    to: toDateKey(bounds.to),
    plaidAccountId: plaidAccountId ?? undefined,
    categoryId: categoryId ?? undefined,
    search: search || undefined,
    isPending: false,
  });

  const rows = useMemo(
    () =>
      data
        .filter((t) => BAR_TYPES.has(t.transactionType))
        .sort((a, b) => b.occurredAt.localeCompare(a.occurredAt)),
    [data],
  );

  const categoryNames = useMemo(
    () => new Map(categories.map((c) => [c.id, c.name])),
    [categories],
  );

  return (
    <Card
      title={formatPeriod(period.start, grouping)}
      subtitle={
        <span className="numeric">
          {currency.format(period.moneyIn)} in · {currency.format(period.moneyOut)} out
        </span>
      }
      actions={
        <Button variant="ghost" size="sm" startIcon={<X size={14} />} onClick={onClear}>
          Clear
        </Button>
      }
      className="animate-rise-in"
    >
      {isLoading ? (
        <div className="space-y-3" aria-busy>
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} height={20} />
          ))}
        </div>
      ) : error ? (
        <p className="text-sm text-ink-muted">Couldn't load these transactions. Please try again.</p>
      ) : rows.length === 0 ? (
        <p className="text-sm text-ink-muted">
          Nothing settled in this period matches your filters.
        </p>
      ) : (
        <ul className="flex flex-col">
          {rows.map((t) => {
            const inflow = isInflowTransaction(t);
            const name = categoryLabel(categoryNames, t.categoryId);
            return (
              <li
                key={t.id}
                className="flex items-center justify-between gap-4 border-b border-border-subtle py-2.5 last:border-b-0"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-ink">{t.payee || name}</p>
                  <p className="text-xs text-ink-muted">
                    <span className="numeric">{format(parseISO(t.occurredAt), 'MMM d')}</span> · {name}
                  </p>
                </div>
                <span
                  className={cn(
                    'numeric shrink-0 whitespace-nowrap text-sm font-semibold',
                    inflow ? 'text-success-dark' : 'text-ink',
                  )}
                >
                  {signedAmount(t.amount, inflow)}
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
};

export default PeriodTransactions;
