import { ChevronDown } from 'lucide-react';
import { format, parseISO } from 'date-fns';
import { Collapsible, Sparkline } from '../../../shared/components/ui';
import { cn } from '../../../shared/utils/cn';
import type { CategoryCard } from '../utils/selectors';
import { semanticColors } from '../utils/chartTheme';

interface CategoryDrillCardProps {
  data: CategoryCard;
  expanded: boolean;
  onToggle: () => void;
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

const CategoryDrillCard = ({ data, expanded, onToggle }: CategoryDrillCardProps) => {
  const pct = data.planned > 0 ? Math.min(100, (data.actual / data.planned) * 100) : 0;
  const rawPct = data.planned > 0 ? Math.round((data.actual / data.planned) * 100) : null;
  const over = data.planned > 0 && data.actual > data.planned;
  const barColor = over ? semanticColors.overspend : semanticColors.income;

  const sparkData = data.monthly;
  const hasSpark = sparkData.some((v) => v > 0);

  return (
    <div
      className={cn(
        'flex h-full flex-col overflow-hidden rounded-xl bg-surface shadow-sm ring-1 transition-shadow duration-160 ease-out-soft hover:shadow-md',
        over ? 'ring-warning/25' : 'ring-ink/[0.06]',
      )}
    >
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={expanded}
        className="focus-ring flex-1 px-4 pb-4 pt-3.5 text-left"
      >
        {/* Label row */}
        <div className="flex items-start justify-between gap-2">
          <p className="truncate text-[13px] font-medium text-ink-muted" title={data.name}>
            {data.name}
          </p>
          <ChevronDown
            size={14}
            className={cn(
              'mt-0.5 shrink-0 text-ink-muted/60 transition-transform duration-160',
              expanded && 'rotate-180',
            )}
          />
        </div>

        {/* Primary figure — the number is the hero, the plan is context */}
        <div className="mt-1.5 flex items-baseline gap-1.5">
          <span className="text-xl font-semibold tabular-nums tracking-[-0.02em] text-ink">
            {currency.format(data.actual)}
          </span>
          <span className="text-xs tabular-nums text-ink-muted">
            / {currency.format(data.planned)}
          </span>
        </div>

        {/* Meter + percentage */}
        <div className="mt-3 flex items-center gap-2.5">
          <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-border-subtle">
            <div
              className="h-full rounded-full transition-all duration-240 ease-out-soft"
              style={{ width: `${pct}%`, backgroundColor: barColor }}
            />
          </div>
          <span
            className={cn(
              'w-9 shrink-0 text-right text-[11px] font-semibold tabular-nums',
              over ? 'text-warning-dark' : 'text-ink-muted',
            )}
          >
            {rawPct === null ? '—' : `${rawPct}%`}
          </span>
        </div>

        {/* Trend */}
        <div className="mt-3 h-8">
          {hasSpark ? (
            <Sparkline data={sparkData} height={32} color={barColor} />
          ) : (
            <span className="text-2xs uppercase tracking-[0.06em] text-ink-muted/60">
              No 3-month history
            </span>
          )}
        </div>
      </button>

      <Collapsible open={expanded}>
        <div className="border-t border-border-subtle bg-background/50 px-4 py-3">
          {data.transactions.length === 0 ? (
            <span className="text-xs text-ink-muted">No transactions in this range</span>
          ) : (
            <ul className="flex flex-col gap-2">
              {data.transactions.map((t) => (
                <li key={t.id} className="flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <p className="truncate text-[13px] font-medium text-ink">
                      {t.payee || data.name}
                    </p>
                    <span className="text-2xs text-ink-muted">
                      {format(parseISO(t.occurredAt), 'MMM d')}
                    </span>
                  </div>
                  <span
                    className={cn(
                      'shrink-0 text-[13px] font-semibold tabular-nums',
                      t.transactionType === 'Income' ? 'text-success-dark' : 'text-ink',
                    )}
                  >
                    {t.transactionType === 'Income' ? '+' : '−'}
                    {currencyPrecise.format(Math.abs(t.amount))}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </Collapsible>
    </div>
  );
};

export default CategoryDrillCard;
