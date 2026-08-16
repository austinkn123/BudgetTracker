import Card from '../../../shared/components/ui/Card';
import { withAlpha } from '../../../shared/theme/tokens';
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
      <Card
        title="Bucket Breakdown"
        fullHeight
        contentClassName="flex min-h-[200px] items-center justify-center"
      >
        <p className="text-sm text-ink-muted">No bucket data for this plan yet</p>
      </Card>
    );
  }

  const maxValue = rows.reduce((m, r) => Math.max(m, r.planned, r.actual), 0);

  return (
    <Card title="Bucket Breakdown" fullHeight>
      <div className="flex flex-col gap-5">
        {rows.map((row) => {
          const baseWidth = maxValue > 0 ? (Math.min(row.planned, row.actual) / maxValue) * 100 : 0;
          const overage = Math.max(0, row.actual - row.planned);
          const overageWidth = maxValue > 0 ? (overage / maxValue) * 100 : 0;
          const underUsed = row.actual < row.planned;
          return (
            <div key={row.bucket}>
              <div className="flex items-baseline justify-between">
                <p className="text-sm font-semibold text-ink">{row.bucket}</p>
                <span className="text-xs text-ink-muted">
                  {`${currency.format(row.actual)} of ${currency.format(row.planned)}`}
                  {overage > 0 && ` (+${currency.format(overage)})`}
                </span>
              </div>
              <div
                className="mt-1.5 flex h-3.5 overflow-hidden rounded-full"
                style={{ backgroundColor: withAlpha(semanticColors.neutral, 0.25) }}
              >
                <div
                  className="h-full"
                  style={{
                    width: `${baseWidth}%`,
                    backgroundColor: underUsed
                      ? withAlpha(semanticColors.income, 0.65)
                      : semanticColors.neutral,
                  }}
                />
                {overage > 0 && (
                  <div
                    className="h-full"
                    style={{ width: `${overageWidth}%`, backgroundColor: semanticColors.overspend }}
                  />
                )}
              </div>
            </div>
          );
        })}
      </div>
    </Card>
  );
};

export default BucketBreakdown;
