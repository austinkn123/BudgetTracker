import { Fragment, useState } from 'react';
import { ChevronRight } from 'lucide-react';
import { format, parseISO } from 'date-fns';
import { Card, Sparkline } from '../../../shared/components/ui';
import { cn } from '../../../shared/utils/cn';
import type { CategoryCard } from '../utils/selectors';
import { semanticColors } from '../utils/chartTheme';

interface CategoryDrillGridProps {
  cards: CategoryCard[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

const currencyPrecise = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
});

/**
 * Category performance as a dense ledger (BUD-20).
 *
 * Replaces a grid of identical cards: at a dozen-plus categories the boxes
 * became visual noise and forced a hard scan. Rows align every figure on a
 * common baseline, so over-plan categories stand out at a glance.
 */
const CategoryDrillGrid = ({ cards }: CategoryDrillGridProps) => {
  const [expandedKey, setExpandedKey] = useState<string | null>(null);

  if (cards.length === 0) {
    return (
      <Card title="Category Drill-Down">
        <p className="text-sm text-ink-muted">
          Add expense line items to your plan to drill into category performance.
        </p>
      </Card>
    );
  }

  return (
    <Card
      title="Category Drill-Down"
      subtitle={`${cards.length} categories`}
      contentClassName="p-0"
    >
      <table className="w-full border-collapse text-sm tabular-nums">
        <thead>
          <tr className="border-b border-border-subtle bg-background/60">
            <th scope="col" className="px-6 py-2.5 text-left text-2xs font-semibold uppercase tracking-[0.07em] text-ink-muted">
              Category
            </th>
            <th scope="col" className="hidden px-3 py-2.5 text-right text-2xs font-semibold uppercase tracking-[0.07em] text-ink-muted sm:table-cell">
              Spent
            </th>
            <th scope="col" className="hidden px-3 py-2.5 text-right text-2xs font-semibold uppercase tracking-[0.07em] text-ink-muted sm:table-cell">
              Plan
            </th>
            <th scope="col" className="w-[26%] px-3 py-2.5 text-left text-2xs font-semibold uppercase tracking-[0.07em] text-ink-muted">
              Progress
            </th>
            <th scope="col" className="hidden w-24 px-3 py-2.5 text-left text-2xs font-semibold uppercase tracking-[0.07em] text-ink-muted lg:table-cell">
              3-mo
            </th>
            <th scope="col" className="w-10 px-3 py-2.5">
              <span className="sr-only">Expand</span>
            </th>
          </tr>
        </thead>

        <tbody>
          {cards.map((data) => {
            const pct = data.planned > 0 ? Math.min(100, (data.actual / data.planned) * 100) : 0;
            const rawPct = data.planned > 0 ? Math.round((data.actual / data.planned) * 100) : null;
            const over = data.planned > 0 && data.actual > data.planned;
            const barColor = over ? semanticColors.overspend : semanticColors.income;
            const expanded = expandedKey === data.key;
            const hasSpark = data.monthly.some((v) => v > 0);

            return (
              <Fragment key={data.key}>
                <tr
                  onClick={() => setExpandedKey((prev) => (prev === data.key ? null : data.key))}
                  className={cn(
                    'cursor-pointer border-b border-border-subtle transition-colors duration-120 hover:bg-primary/[0.03]',
                    expanded && 'bg-primary/[0.04]',
                  )}
                >
                  <td className="px-6 py-3">
                    <div className="flex items-center gap-2.5">
                      {/* Status tick — the only colour in the row until something drifts */}
                      <span
                        aria-hidden
                        className="h-6 w-0.5 shrink-0 rounded-full"
                        style={{ backgroundColor: barColor }}
                      />
                      <span className="truncate font-medium text-ink" title={data.name}>
                        {data.name}
                      </span>
                    </div>
                  </td>
                  <td className="hidden px-3 py-3 text-right font-semibold text-ink sm:table-cell">
                    {currency.format(data.actual)}
                  </td>
                  <td className="hidden px-3 py-3 text-right text-ink-muted sm:table-cell">
                    {currency.format(data.planned)}
                  </td>
                  <td className="px-3 py-3">
                    <div className="flex items-center gap-2.5">
                      <div className="h-1.5 min-w-10 flex-1 overflow-hidden rounded-full bg-border-subtle">
                        <div
                          className="h-full rounded-full transition-all duration-240 ease-out-soft"
                          style={{ width: `${pct}%`, backgroundColor: barColor }}
                        />
                      </div>
                      <span
                        className={cn(
                          'w-10 shrink-0 text-right text-xs font-semibold',
                          over ? 'text-warning-dark' : 'text-ink-muted',
                        )}
                      >
                        {rawPct === null ? '—' : `${rawPct}%`}
                      </span>
                    </div>
                  </td>
                  <td className="hidden px-3 py-3 lg:table-cell">
                    {hasSpark ? (
                      <Sparkline data={data.monthly} height={22} color={barColor} />
                    ) : (
                      <span className="text-2xs text-ink-muted/50">—</span>
                    )}
                  </td>
                  <td className="px-3 py-3">
                    <button
                      type="button"
                      aria-expanded={expanded}
                      aria-label={`${expanded ? 'Hide' : 'Show'} transactions for ${data.name}`}
                      onClick={(event) => {
                        // The row is clickable too; stop this from toggling twice.
                        event.stopPropagation();
                        setExpandedKey((prev) => (prev === data.key ? null : data.key));
                      }}
                      className="focus-ring rounded p-0.5"
                    >
                      <ChevronRight
                        size={15}
                        aria-hidden
                        className={cn(
                          'text-ink-muted/60 transition-transform duration-160',
                          expanded && 'rotate-90',
                        )}
                      />
                    </button>
                  </td>
                </tr>

                {expanded && (
                  <tr className="border-b border-border-subtle bg-background/60">
                    <td colSpan={6} className="px-6 py-3">
                      {data.transactions.length === 0 ? (
                        <span className="text-xs text-ink-muted">
                          No transactions in this range
                        </span>
                      ) : (
                        <ul className="flex flex-col gap-1.5">
                          {data.transactions.map((t) => (
                            <li key={t.id} className="flex items-center justify-between gap-4">
                              <span className="truncate text-[13px] text-ink">
                                {t.payee || data.name}
                                <span className="ml-2 text-2xs text-ink-muted">
                                  {format(parseISO(t.occurredAt), 'MMM d')}
                                </span>
                              </span>
                              <span
                                className={cn(
                                  'shrink-0 text-[13px] font-semibold',
                                  t.transactionType === 'Income'
                                    ? 'text-success-dark'
                                    : 'text-ink',
                                )}
                              >
                                {t.transactionType === 'Income' ? '+' : '−'}
                                {currencyPrecise.format(Math.abs(t.amount))}
                              </span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </td>
                  </tr>
                )}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </Card>
  );
};

export default CategoryDrillGrid;
