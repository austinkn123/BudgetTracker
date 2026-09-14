import { addMonths, format, isSameMonth, startOfMonth } from 'date-fns';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Badge, Button, Card, Progress } from '../../../shared/components/ui';
import type { PlanPerformance } from '../../../shared/types/api';
import { currency, percent } from '../../../shared/utils/format';
import { semanticColors } from '../../dashboard/utils/chartTheme';

type MonthHeaderProps = {
  month: Date;
  onMonthChange: (month: Date) => void;
  performance: PlanPerformance | null;
};

/**
 * The month under review, and how it is tracking against the plan governing it.
 *
 * Pacing wording is deliberate: the API's `Ahead` means spending *slower* than the calendar, which
 * reads backwards to most people, so the copy states the meaning rather than the enum name.
 */
const MonthHeader = ({ month, onMonthChange, performance }: MonthHeaderProps) => {
  const now = new Date();
  const isCurrentMonth = isSameMonth(month, now);
  const pacing = performance?.pacing;

  const spentPct = pacing && pacing.plannedExpenses > 0 ? pacing.spentPct : 0;
  const overPlan = pacing ? pacing.remaining < 0 : false;

  const paceLabel = !pacing
    ? null
    : pacing.status === 'Behind'
      ? 'Spending faster than the month'
      : pacing.status === 'Ahead'
        ? 'Spending slower than the month'
        : 'On pace';

  return (
    <Card padding="md">
      <div className="flex flex-col gap-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              aria-label="Previous month"
              onClick={() => onMonthChange(startOfMonth(addMonths(month, -1)))}
            >
              <ChevronLeft size={16} />
            </Button>
            <h2 className="min-w-[9ch] text-center text-[17px] font-semibold tracking-[-0.01em] text-ink">
              {format(month, 'MMMM yyyy')}
            </h2>
            <Button
              variant="ghost"
              size="sm"
              aria-label="Next month"
              disabled={isCurrentMonth}
              onClick={() => onMonthChange(startOfMonth(addMonths(month, 1)))}
            >
              <ChevronRight size={16} />
            </Button>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            {performance && (
              <span className="text-sm text-ink-muted">{performance.plan.name}</span>
            )}
            {paceLabel && (
              <Badge
                label={paceLabel}
                color={pacing!.status === 'Behind' ? 'warning' : 'success'}
                variant="soft"
              />
            )}
          </div>
        </div>

        {pacing ? (
          <div className="flex flex-col gap-4">
            <div className="grid gap-4 sm:grid-cols-3">
              <Figure label="Planned" value={currency.format(pacing.plannedExpenses)} />
              <Figure label="Actual" value={currency.format(pacing.actualExpenses)} />
              <Figure
                label={overPlan ? 'Over plan' : 'Remaining'}
                value={currency.format(Math.abs(pacing.remaining))}
                tone={overPlan ? 'negative' : 'positive'}
              />
            </div>

            <div>
              <div className="mb-1.5 flex items-baseline justify-between">
                <span className="text-xs font-medium text-ink-muted">
                  {percent(spentPct)} of plan spent
                </span>
                <span className="numeric text-xs text-ink-muted">
                  Day {pacing.daysElapsed} of {pacing.daysInMonth}
                </span>
              </div>
              <div className="relative">
                <Progress
                  value={Math.min(spentPct * 100, 100)}
                  height={8}
                  barColor={overPlan ? semanticColors.overspend : semanticColors.income}
                />
                {/* Where the calendar has got to. Spend bar left of this marker is ahead of pace. */}
                <div
                  aria-hidden
                  className="absolute -bottom-1 -top-1 w-0.5 bg-ink/40"
                  style={{ left: `calc(${Math.min(pacing.daysPct * 100, 100)}% - 1px)` }}
                />
              </div>
            </div>
          </div>
        ) : (
          <p className="text-sm text-ink-muted">
            No budget plan governs {format(month, 'MMMM yyyy')} yet, so there is nothing to measure
            this month against. Spending below is grouped by category.
          </p>
        )}
      </div>
    </Card>
  );
};

const Figure = ({
  label,
  value,
  tone,
}: {
  label: string;
  value: string;
  tone?: 'positive' | 'negative';
}) => (
  <div>
    <p className="text-xs font-medium uppercase tracking-wide text-ink-muted">{label}</p>
    <p
      className={`numeric mt-0.5 text-[22px] font-semibold tracking-[-0.02em] ${
        tone === 'negative' ? 'text-error-dark' : tone === 'positive' ? 'text-success-dark' : 'text-ink'
      }`}
    >
      {value}
    </p>
  </div>
);

export default MonthHeader;
