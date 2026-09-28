import Card from '../../../shared/components/ui/Card';
import { Badge } from '../../../shared/components/ui';
import type { BucketPerformance } from '../../../shared/types/api';
import { semanticColors } from '../utils/chartTheme';

interface BucketBreakdownProps {
  /** Core/Buffer planned-vs-actual, straight from the analysis. */
  rows: BucketPerformance[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

const BucketBreakdown = ({ rows }: BucketBreakdownProps) => {
  if (rows.length === 0 || rows.every((r) => r.planned === 0 && r.actual === 0)) {
    return (
      <Card title="Bucket Breakdown" fullHeight contentClassName="flex min-h-[200px] items-center justify-center">
        <p className="text-sm text-ink-muted">No bucket data for this plan yet</p>
      </Card>
    );
  }

  const maxValue = rows.reduce((m, r) => Math.max(m, r.planned, r.actual), 0);

  return (
    <Card title="Bucket Breakdown" subtitle="Planned versus actual by bucket" fullHeight>
      <ul className="flex flex-col gap-6">
        {rows.map((row) => {
          const baseWidth = maxValue > 0 ? (Math.min(row.planned, row.actual) / maxValue) * 100 : 0;
          const overage = Math.max(0, row.actual - row.planned);
          const overageWidth = maxValue > 0 ? (overage / maxValue) * 100 : 0;
          const plannedWidth = maxValue > 0 ? (row.planned / maxValue) * 100 : 0;
          const underUsed = row.actual < row.planned;

          return (
            <li key={row.bucket}>
              <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <p className="text-[13px] font-semibold text-ink">{row.bucket}</p>
                  {overage > 0 && (
                    <Badge color="warning" label={`+${currency.format(overage)}`} />
                  )}
                </div>
                <span className="text-[13px] tabular-nums text-ink-muted">
                  <span className="font-semibold text-ink">{currency.format(row.actual)}</span>
                  {' of '}
                  {currency.format(row.planned)}
                </span>
              </div>

              <div className="relative mt-2 h-2.5">
                {/* Track */}
                <div className="absolute inset-0 rounded-full bg-border-subtle" />
                {/* Actual (+ overage continuation). inset-0 so the percentage
                    widths below resolve against the full track, not auto. */}
                <div className="absolute inset-0 flex overflow-hidden rounded-full">
                  <div
                    className="h-full transition-all duration-240 ease-out-soft"
                    style={{
                      width: `${baseWidth}%`,
                      backgroundColor: underUsed ? semanticColors.income : semanticColors.neutral,
                    }}
                  />
                  {overage > 0 && (
                    <div
                      className="h-full transition-all duration-240 ease-out-soft"
                      style={{ width: `${overageWidth}%`, backgroundColor: semanticColors.overspend }}
                    />
                  )}
                </div>
                {/* Plan marker */}
                {row.planned > 0 && (
                  <span
                    aria-hidden
                    className="absolute -top-0.5 h-3.5 w-px bg-ink/40"
                    style={{ left: `${plannedWidth}%` }}
                  />
                )}
              </div>
            </li>
          );
        })}
      </ul>
    </Card>
  );
};

export default BucketBreakdown;
