import { formatDistanceToNow, parseISO } from 'date-fns';
import { useMemo } from 'react';
import { Badge, Card } from '../../../shared/components/ui';
import { withAlpha } from '../../../shared/theme/tokens';
import type { Category, Transaction } from '../../../shared/types/api';
import { categoryLabel } from '../utils/selectors';
import { chartPalette, semanticColors } from '../utils/chartTheme';

interface RecentActivityFeedProps {
  /** Window-scoped transactions, already sorted newest-first and capped. */
  items: Transaction[];
  /** Full category list — drives names and the stable palette-by-index chip colors. */
  categories: Category[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
});

const RecentActivityFeed = ({ items, categories }: RecentActivityFeedProps) => {
  const categoryNames = useMemo(
    () => new Map(categories.map((c) => [c.id, c.name])),
    [categories],
  );

  // Stable color per category — index into the chart palette by position.
  const colorByCategory = useMemo(() => {
    const map = new Map<number, string>();
    categories.forEach((c, idx) => {
      map.set(c.id, chartPalette[idx % chartPalette.length]);
    });
    return map;
  }, [categories]);

  return (
    <Card title="Recent Activity" fullHeight>
      {items.length === 0 ? (
        <div className="flex min-h-[160px] items-center justify-center">
          <p className="text-sm text-ink-muted">No transactions yet</p>
        </div>
      ) : (
        <div className="flex flex-col gap-2.5">
          {items.map((t) => {
            const color =
              (t.categoryId != null ? colorByCategory.get(t.categoryId) : undefined) ??
              semanticColors.neutral;
            const name = categoryLabel(categoryNames, t.categoryId ?? null);
            const isIncome = t.transactionType === 'Income';
            return (
              <div key={t.id} className="grid grid-cols-[auto_1fr_auto] items-center gap-3 py-1">
                <Badge
                  label={name}
                  className="max-w-[140px] border-transparent text-ink"
                  style={{ backgroundColor: withAlpha(color, 0.18) }}
                />
                <div className="min-w-0">
                  <p className="truncate text-sm font-semibold text-ink">{t.payee || name}</p>
                  <span className="text-xs text-ink-muted">
                    {formatDistanceToNow(parseISO(t.occurredAt), { addSuffix: true })}
                  </span>
                </div>
                <span
                  className={
                    isIncome
                      ? 'whitespace-nowrap text-sm font-semibold text-success-dark'
                      : 'whitespace-nowrap text-sm font-semibold text-ink-muted'
                  }
                >
                  {isIncome ? '+' : '-'}
                  {currency.format(Math.abs(t.amount))}
                </span>
              </div>
            );
          })}
        </div>
      )}
    </Card>
  );
};

export default RecentActivityFeed;
