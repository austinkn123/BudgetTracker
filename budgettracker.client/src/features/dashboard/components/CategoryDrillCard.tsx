import { format, parseISO } from 'date-fns';
import { Card, Collapsible, Separator, Sparkline } from '../../../shared/components/ui';
import { withAlpha } from '../../../shared/theme/tokens';
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
  const over = data.planned > 0 && data.actual > data.planned;
  const barColor = over ? semanticColors.overspend : semanticColors.income;

  const sparkData = data.monthly;
  const hasSpark = sparkData.some((v) => v > 0);

  return (
    <Card padding="none" fullHeight className="flex flex-col" contentClassName="flex flex-1 flex-col">
      {/* Only the header toggles; the expanded list below stays non-interactive. */}
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={expanded}
        className="focus-ring flex-1 rounded-md p-4 text-left transition-colors duration-120 hover:bg-background"
      >
        <div className="flex items-baseline justify-between gap-2">
          <p className="truncate text-sm font-semibold text-ink">{data.name}</p>
          <span
            className={
              over ? 'text-xs font-semibold text-warning-dark' : 'text-xs font-semibold text-ink-muted'
            }
          >
            {data.planned > 0 ? `${Math.round((data.actual / data.planned) * 100)}%` : '—'}
          </span>
        </div>
        <p className="mt-1 text-sm text-ink-muted">
          {`${currency.format(data.actual)} / ${currency.format(data.planned)}`}
        </p>

        <div
          className="mt-2.5 h-2 overflow-hidden rounded-full"
          style={{ backgroundColor: withAlpha(semanticColors.neutral, 0.3) }}
        >
          <div
            className="h-full rounded-full transition-all duration-160 ease-out-soft"
            style={{ width: `${pct}%`, backgroundColor: barColor }}
          />
        </div>

        <div className="mt-3 h-10">
          {hasSpark ? (
            <Sparkline data={sparkData} height={40} color={barColor} />
          ) : (
            <span className="text-xs text-ink-muted">No 3-month history</span>
          )}
        </div>
      </button>

      <Collapsible open={expanded}>
        <Separator />
        <div className="px-4 py-2">
          {data.transactions.length === 0 ? (
            <span className="text-xs text-ink-muted">No transactions in this range</span>
          ) : (
            <ul className="flex flex-col">
              {data.transactions.map((t) => (
                <li key={t.id} className="flex items-center justify-between gap-3 py-1">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-ink">{t.payee || data.name}</p>
                    <span className="text-xs text-ink-muted">
                      {format(parseISO(t.occurredAt), 'MMM d, yyyy')}
                    </span>
                  </div>
                  <span
                    className={
                      t.transactionType === 'Income'
                        ? 'shrink-0 text-sm font-semibold text-success-dark'
                        : 'shrink-0 text-sm font-semibold text-ink-muted'
                    }
                  >
                    {t.transactionType === 'Income' ? '+' : '-'}
                    {currencyPrecise.format(Math.abs(t.amount))}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </Collapsible>
    </Card>
  );
};

export default CategoryDrillCard;
