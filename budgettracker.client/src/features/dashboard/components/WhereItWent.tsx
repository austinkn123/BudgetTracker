import Card from '../../../shared/components/ui/Card';
import { withAlpha } from '../../../shared/theme/tokens';
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

  return (
    <Card title="Where It Went" fullHeight>
      {rows.length === 0 ? (
        <div className="flex min-h-[240px] items-center justify-center">
          <p className="text-sm text-ink-muted">No expenses in this range</p>
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          {rows.map((row, idx) => {
            const pct = max > 0 ? (row.value / max) * 100 : 0;
            const color =
              row.key === 'other' ? semanticColors.neutral : chartPalette[idx % chartPalette.length];
            return (
              <div key={row.key}>
                <div className="flex items-baseline justify-between">
                  <p className="text-sm font-semibold text-ink">{row.label}</p>
                  <span className="text-xs text-ink-muted">{currency.format(row.value)}</span>
                </div>
                <div
                  className="mt-1 h-2.5 overflow-hidden rounded-full"
                  style={{ backgroundColor: withAlpha(semanticColors.neutral, 0.3) }}
                >
                  <div
                    className="h-full rounded-full transition-all duration-240 ease-out-soft"
                    style={{ width: `${pct}%`, backgroundColor: color }}
                  />
                </div>
              </div>
            );
          })}
        </div>
      )}
    </Card>
  );
};

export default WhereItWent;
