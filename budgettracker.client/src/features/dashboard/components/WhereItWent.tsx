import Card from '../../../shared/components/ui/Card';
import type { SpendSlice } from '../utils/selectors';
import { chartPalette, semanticColors } from '../utils/chartTheme';

interface WhereItWentProps {
  /** Window-scoped expense totals, already ranked with a trailing "Other". */
  rows: SpendSlice[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

const WhereItWent = ({ rows }: WhereItWentProps) => {
  const max = rows.reduce((m, r) => Math.max(m, r.value), 0);
  const total = rows.reduce((sum, r) => sum + r.value, 0);

  return (
    <Card
      title="Where It Went"
      subtitle={total > 0 ? `${currency.format(total)} total` : undefined}
      fullHeight
    >
      {rows.length === 0 ? (
        <div className="flex min-h-[240px] flex-col items-center justify-center gap-1 text-center">
          <p className="text-sm font-medium text-ink">No expenses in this range</p>
          <p className="text-xs text-ink-muted">Spending will break down here once it lands.</p>
        </div>
      ) : (
        <ul className="flex flex-col">
          {rows.map((row, idx) => {
            const pct = max > 0 ? (row.value / max) * 100 : 0;
            const share = total > 0 ? Math.round((row.value / total) * 100) : 0;
            const color =
              row.key === 'other' ? semanticColors.neutral : chartPalette[idx % chartPalette.length];
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
                    <span className="shrink-0 text-[13px] font-semibold tabular-nums text-ink">
                      {currency.format(row.value)}
                    </span>
                  </div>
                  <div className="mt-1.5 flex items-center gap-2">
                    <div className="h-1 flex-1 overflow-hidden rounded-full bg-border-subtle">
                      <div
                        className="h-full rounded-full transition-all duration-240 ease-out-soft"
                        style={{ width: `${pct}%`, backgroundColor: color }}
                      />
                    </div>
                    <span className="w-8 shrink-0 text-right text-2xs tabular-nums text-ink-muted">
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
