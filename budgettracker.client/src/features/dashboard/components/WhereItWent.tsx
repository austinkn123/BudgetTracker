import Card from '../../../shared/components/ui/Card';
import type { SpendSlice } from '../utils/selectors';
import { chartPalette, semanticColors } from '../utils/chartTheme';
import { currency } from '../../../shared/utils/format';

interface WhereItWentProps {
  /** Window-scoped totals, already ranked with a trailing "Other". */
  rows: SpendSlice[];
  /** Defaults describe spending; Cash Flow reuses the list for income sources. */
  title?: string;
  emptyTitle?: string;
  emptyDetail?: string;
  /** One colour for every row instead of the category palette — income is always success green. */
  color?: string;
  /** Headline total when it differs from the sum of `rows`; shares still divide by that sum. */
  total?: number;
}

const WhereItWent = ({
  rows,
  title = 'Where it went',
  emptyTitle = 'No expenses in this range',
  emptyDetail = 'Spending will break down here once it lands.',
  color: rowColor,
  total: headlineTotal,
}: WhereItWentProps) => {
  const max = rows.reduce((m, r) => Math.max(m, r.value), 0);
  const total = rows.reduce((sum, r) => sum + r.value, 0);

  return (
    <Card
      title={title}
      subtitle={
        (headlineTotal ?? total) > 0 ? `${currency.format(headlineTotal ?? total)} total` : undefined
      }
      fullHeight
    >
      {rows.length === 0 ? (
        <div className="flex min-h-[240px] flex-col items-center justify-center gap-1 text-center">
          <p className="text-sm font-medium text-ink">{emptyTitle}</p>
          <p className="text-xs text-ink-muted">{emptyDetail}</p>
        </div>
      ) : (
        <ul className="flex flex-col">
          {rows.map((row, idx) => {
            const pct = max > 0 ? (row.value / max) * 100 : 0;
            const share = total > 0 ? Math.round((row.value / total) * 100) : 0;
            const color =
              row.key === 'other'
                ? semanticColors.neutral
                : (rowColor ?? chartPalette[idx % chartPalette.length]);
            return (
              <li
                key={row.key}
                className="flex items-center gap-3 border-b border-border-subtle py-2.5 last:border-b-0"
              >
                <span
                  className="h-6 w-1 shrink-0 rounded-full"
                  style={{ backgroundColor: color }}
                  aria-hidden
                />
                <div className="min-w-0 flex-1">
                  <div className="flex items-baseline justify-between gap-2">
                    <p className="truncate text-[13px] font-medium text-ink" title={row.label}>
                      {row.label}
                    </p>
                    <span className="shrink-0 text-[13px] font-semibold numeric text-ink">
                      {currency.format(row.value)}
                    </span>
                  </div>
                  <div className="mt-1.5 flex items-center gap-2">
                    <div className="h-1 flex-1 overflow-hidden rounded-full bg-border-subtle">
                      <div
                        className="h-full origin-left animate-grow-x rounded-full transition-all duration-240 ease-out-soft"
                        style={{ width: `${pct}%`, backgroundColor: color, animationDelay: `${idx * 40}ms` }}
                      />
                    </div>
                    <span className="w-8 shrink-0 text-right text-2xs numeric text-ink-muted">
                      {share}%
                    </span>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
};

export default WhereItWent;
