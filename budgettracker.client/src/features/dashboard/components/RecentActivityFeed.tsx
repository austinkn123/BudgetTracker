import { formatDistanceToNow, parseISO } from 'date-fns';
import { useMemo } from 'react';
import { Card } from '../../../shared/components/ui';
import { cn } from '../../../shared/utils/cn';
import { withAlpha } from '../../../shared/theme/tokens';
import type { Category, Transaction } from '../../../shared/types/api';
import { categoryLabel } from '../utils/selectors';
import { chartPalette, semanticColors } from '../utils/chartTheme';

interface RecentActivityFeedProps {
  /** Window-scoped transactions, already sorted newest-first and capped. */
  items: Transaction[];
  /** Full category list — drives names and the stable palette-by-index dot colors. */
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
    <Card title="Recent Activity" subtitle={items.length > 0 ? `Last ${items.length}` : undefined} fullHeight>
      {items.length === 0 ? (
        <div className="flex min-h-[160px] flex-col items-center justify-center gap-1 text-center">
          <p className="text-sm font-medium text-ink">No transactions yet</p>
          <p className="text-xs text-ink-muted">New activity shows up here as it syncs.</p>
        </div>
      ) : (
        <ul className="flex flex-col">
          {items.map((t) => {
            const color =
              (t.categoryId != null ? colorByCategory.get(t.categoryId) : undefined) ??
              semanticColors.neutral;
            const name = categoryLabel(categoryNames, t.categoryId ?? null);
            const isIncome = t.transactionType === 'Income';
            return (
              <li
                key={t.id}
                className="flex items-center gap-3 border-b border-border-subtle py-2.5 last:border-b-0"
              >
                {/* Category swatch */}
                <span
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-[11px] font-semibold uppercase"
                  style={{ backgroundColor: withAlpha(color, 0.14), color }}
                  aria-hidden
                >
                  {name.slice(0, 2)}
                </span>

                <div className="min-w-0 flex-1">
                  <p className="truncate text-[13px] font-medium text-ink">{t.payee || name}</p>
                  <span className="text-2xs text-ink-muted">
                    {name} · {formatDistanceToNow(parseISO(t.occurredAt), { addSuffix: true })}
                  </span>
                </div>

                <span
                  className={cn(
                    'shrink-0 whitespace-nowrap text-[13px] font-semibold tabular-nums',
                    isIncome ? 'text-success-dark' : 'text-ink',
                  )}
                >
                  {isIncome ? '+' : '−'}
                  {currency.format(Math.abs(t.amount))}
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
};

export default RecentActivityFeed;
