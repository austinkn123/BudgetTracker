import { AlertTriangle, TrendingUp } from 'lucide-react';
import { format, parseISO } from 'date-fns';
import { Badge, Card, Gauge, Progress } from '../../../shared/components/ui';
import type { AnalyzedPlan, PeriodPacing } from '../../../shared/types/api';
import type { DriftingCategory } from '../utils/selectors';
import { heroSurface, semanticColors } from '../utils/chartTheme';

interface PlanStoryHeroProps {
  plan: AnalyzedPlan;
  pacing: PeriodPacing;
  /** Copy is composed client-side from the pacing numbers. */
  headline: string;
  drifting: DriftingCategory[];
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
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
});

const clampPct = (value: number): number => {
  if (!Number.isFinite(value)) return 0;
  return Math.max(0, Math.min(100, value));
};

const PlanStoryHero = ({ plan, pacing, headline, drifting }: PlanStoryHeroProps) => {
  const gaugeValue = clampPct(pacing.spentPct * 100);
  const onTrack = pacing.pacingDelta <= 0;
  const gaugeColor = onTrack ? semanticColors.income : semanticColors.overspend;
  const projectionPct = pacing.plannedExpenses > 0
    ? clampPct((pacing.projectedEnd / pacing.plannedExpenses) * 100)
    : 0;
  const overshoot = projectionPct > 100;

  const remainingLabel = pacing.remaining >= 0
    ? `${currency.format(pacing.remaining)} left`
    : `${currency.format(Math.abs(pacing.remaining))} over`;

  const planMonthLabel = format(parseISO(plan.planMonth), 'MMMM yyyy');

  return (
    <Card padding="none" className="overflow-hidden">
      {/* Gradient + border are computed token strings, so they ride inline. */}
      <div
        className="p-6 md:p-8"
        style={{ background: heroSurface.background }}
      >
        <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-[28px] font-semibold leading-tight tracking-[-0.02em] text-white">
              {headline}
            </h1>
            <p className="mt-1.5 text-sm text-white/60">
              {plan.name} · {planMonthLabel}
            </p>
          </div>
          <Badge
            icon={onTrack ? <TrendingUp size={14} /> : <AlertTriangle size={14} />}
            label={onTrack ? 'On pace' : 'Watch your pace'}
            color={onTrack ? 'success' : 'warning'}
            variant="solid"
          />
        </div>

        <div className="grid items-center gap-8 lg:grid-cols-[260px_1fr]">
          {/* Gauge */}
          <div className="flex flex-col items-center">
            <Gauge
              value={gaugeValue}
              valueColor={gaugeColor}
              trackColor="rgb(255 255 255 / 0.12)"
              textColor="rgb(255 255 255)"
            />
            <p className="-mt-1 text-sm font-semibold text-white/60">{remainingLabel}</p>
          </div>

          {/* Progress rows */}
          <div className="flex flex-col gap-4">
            <ProgressRow
              label="Days elapsed"
              detail={`${pacing.daysElapsed} / ${pacing.daysInMonth}`}
              percent={pacing.daysPct * 100}
              color="rgb(255 255 255 / 0.55)"
            />

            <ProgressRow
              label="Budget spent"
              detail={`${currency.format(pacing.actualExpenses)} / ${currency.format(pacing.plannedExpenses)}`}
              percent={pacing.spentPct * 100}
              color={gaugeColor}
            />

            <div>
              <div className="flex items-baseline justify-between">
                <p className="text-sm font-semibold text-white">Projection</p>
                <span className="text-xs text-white/60">
                  {`Projects to ${currency.format(pacing.projectedEnd)}`}
                </span>
              </div>
              <div className="relative mt-2">
                <Progress
                  value={Math.min(projectionPct, 100)}
                  height={8}
                  barColor={overshoot ? semanticColors.overspend : semanticColors.income}
                  trackColor="rgb(255 255 255 / 0.12)"
                />
                {/* 100% marker */}
                <div
                  aria-hidden
                  className="absolute -bottom-0.5 -top-0.5 w-0.5 bg-white/40"
                  style={{ left: 'calc(100% - 1px)' }}
                />
                {overshoot && (
                  <span className="mt-1.5 block text-xs font-semibold text-warning-light">
                    {`Overshoot: +${currency.format(pacing.projectedEnd - pacing.plannedExpenses)}`}
                  </span>
                )}
              </div>
              <span className="mt-1.5 block text-xs text-white/50">
                {pacing.perDiemToStay > 0
                  ? `Stay-on-plan rate: ${currencyPrecise.format(pacing.perDiemToStay)}/day`
                  : `No days remaining in this plan month`}
              </span>
            </div>
          </div>
        </div>

        {drifting.length > 0 && (
          <div className="mt-8 flex flex-wrap items-center gap-2 border-t border-white/10 pt-6">
            <span className="mr-1 text-xs font-semibold uppercase tracking-[0.06em] text-white/50">
              Drifting
            </span>
            {drifting.map((d) => (
              <Badge
                key={d.key}
                label={`${d.name} +${currency.format(d.overBy)}`}
                className="border-white/20 bg-white/10 text-white"
              />
            ))}
          </div>
        )}
      </div>
    </Card>
  );
};

interface ProgressRowProps {
  label: string;
  detail: string;
  percent: number;
  color: string;
}

const ProgressRow = ({ label, detail, percent, color }: ProgressRowProps) => (
  <div>
    <div className="flex items-baseline justify-between">
      <p className="text-sm font-semibold text-white">{label}</p>
      <span className="text-xs text-white/60">{detail}</span>
    </div>
    <Progress
      value={clampPct(percent)}
      height={8}
      barColor={color}
      trackColor="rgb(255 255 255 / 0.12)"
      className="mt-2"
    />
  </div>
);

export default PlanStoryHero;
