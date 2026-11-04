import { Badge, Card } from '../../../shared/components/ui';
import type { BadgeColor } from '../../../shared/components/ui';
import { currency } from '../../../shared/utils/format';
import { cn } from '../../../shared/utils/cn';
import type { Split } from '../utils/moneyMap';

const SEGMENTS = [
  { key: 'mustPay', label: 'Must-pay', bar: 'bg-primary', dot: 'bg-primary' },
  { key: 'flexible', label: 'Flexible', bar: 'bg-secondary', dot: 'bg-secondary' },
  { key: 'saving', label: 'Saving', bar: 'bg-success', dot: 'bg-success' },
] as const;

/** Segments narrower than this share of the bar show only the amount; narrower still, nothing. */
const LABEL_SHARE = 0.18;
const AMOUNT_SHARE = 0.06;
const TOLERANCE = 0.05;

interface PlanVsActualProps {
  income: number;
  plan: Split;
  actual: Split;
  months: number;
}

const Bar = ({ label, split, income }: { label: string; split: Split; income: number }) => (
  <div className="flex items-center gap-4">
    <span className="w-14 shrink-0 text-xs font-semibold text-ink-muted">{label}</span>
    <div className="flex h-9 flex-1 gap-[3px] overflow-hidden rounded-md" role="img" aria-label={`${label}: ${SEGMENTS.map((s) => `${s.label} ${currency.format(split[s.key])}`).join(', ')}`}>
      {SEGMENTS.map((s) => {
        const value = Math.max(split[s.key], 0);
        const share = income > 0 ? value / income : 0;
        if (value === 0) return null;
        return (
          <div key={s.key} className={cn('flex min-w-0 items-center px-3 text-xs font-semibold text-white', s.bar)} style={{ flexGrow: value, flexBasis: 0 }}>
            {share >= AMOUNT_SHARE && (
              <span className="numeric truncate">
                {share >= LABEL_SHARE ? `${s.label} ${currency.format(value)}` : currency.format(value)}
              </span>
            )}
          </div>
        );
      })}
    </div>
  </div>
);

const verdict = (key: (typeof SEGMENTS)[number]['key'], plan: number, actual: number): { color: BadgeColor; label: string } => {
  const gap = actual - plan;
  if (key === 'saving')
    return gap < -Math.abs(plan) * TOLERANCE
      ? { color: 'error', label: `${currency.format(-gap)} short of plan` }
      : { color: 'success', label: 'On track' };
  return gap > plan * TOLERANCE
    ? { color: 'warning', label: `${currency.format(gap)} over plan` }
    : { color: 'success', label: 'On track' };
};

/** A normal month's pay split three ways, plan above actual, with a one-line verdict per group. */
const PlanVsActual = ({ income, plan, actual, months }: PlanVsActualProps) => (
  <Card contentClassName="flex flex-col gap-5">
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
      <p className="numeric text-4xl font-bold tracking-[-0.03em] text-ink">{currency.format(income)}</p>
      <div>
        <p className="text-base font-medium text-ink">comes in during a normal month</p>
        <p className="text-sm text-ink-muted">
          {months === 1
            ? 'The plan, against what this month has cost so far.'
            : `The plan, against what a month actually cost on average over the last ${months} months.`}
        </p>
      </div>
    </div>

    <div className="flex flex-col gap-2.5">
      <Bar label="Plan" split={plan} income={income} />
      <Bar label="Actual" split={actual} income={income} />
    </div>

    <ul className="grid gap-4 md:grid-cols-3">
      {SEGMENTS.map((s) => {
        const v = verdict(s.key, plan[s.key], actual[s.key]);
        return (
          <li key={s.key} className="flex flex-col gap-2 rounded-lg border border-border-subtle bg-background/60 px-4 py-3.5">
            <span className="flex items-center gap-2 text-sm font-semibold text-ink">
              <span className={cn('h-2.5 w-2.5 rounded-full', s.dot)} aria-hidden />
              {s.label}
            </span>
            <span className="numeric text-sm text-ink-muted">
              Plan {currency.format(plan[s.key])} · Actual {currency.format(actual[s.key])}
            </span>
            <Badge label={v.label} color={v.color} variant="soft" className="self-start" />
          </li>
        );
      })}
    </ul>
  </Card>
);

export default PlanVsActual;
