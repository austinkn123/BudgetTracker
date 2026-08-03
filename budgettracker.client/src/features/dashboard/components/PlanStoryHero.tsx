import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import LinearProgress from '@mui/material/LinearProgress';
import Chip from '@mui/material/Chip';
import Box from '@mui/material/Box';
import { Gauge, gaugeClasses } from '@mui/x-charts/Gauge';
import { alpha, useTheme } from '@mui/material/styles';
import { AlertTriangle, TrendingUp } from 'lucide-react';
import { format, parseISO } from 'date-fns';
import type { AnalyzedPlan, PeriodPacing } from '../../../shared/types/api';
import type { DriftingCategory } from '../utils/selectors';
import { getHeroSurface, getSemanticColors } from '../utils/chartTheme';

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
  const theme = useTheme();
  const semantic = getSemanticColors(theme);
  const surface = getHeroSurface(theme);

  const gaugeValue = clampPct(pacing.spentPct * 100);
  const onTrack = pacing.pacingDelta <= 0;
  const gaugeColor = onTrack ? semantic.income : semantic.overspend;
  const projectionPct = pacing.plannedExpenses > 0
    ? clampPct((pacing.projectedEnd / pacing.plannedExpenses) * 100)
    : 0;
  const overshoot = projectionPct > 100;

  const remainingLabel = pacing.remaining >= 0
    ? `${currency.format(pacing.remaining)} left`
    : `${currency.format(Math.abs(pacing.remaining))} over`;

  const planMonthLabel = format(parseISO(plan.planMonth), 'MMMM yyyy');

  return (
    <Card
      sx={{
        background: surface.background,
        border: `1px solid ${surface.border}`,
        overflow: 'hidden',
      }}
    >
      <CardContent sx={{ p: { xs: 3, md: 4 } }}>
        <Box className="flex items-start justify-between flex-wrap gap-2 mb-4">
          <Box>
            <Typography variant="h4" sx={{ fontWeight: 700, color: semantic.ink }}>
              {headline}
            </Typography>
            <Typography variant="body2" sx={{ color: semantic.expense, mt: 0.5 }}>
              {plan.name} · {planMonthLabel}
            </Typography>
          </Box>
          <Chip
            icon={onTrack ? <TrendingUp size={14} /> : <AlertTriangle size={14} />}
            label={onTrack ? 'On pace' : 'Watch your pace'}
            size="small"
            sx={{
              backgroundColor: alpha(gaugeColor, 0.18),
              color: semantic.ink,
              fontWeight: 600,
              borderRadius: 999,
            }}
          />
        </Box>

        <Box
          className="grid gap-6"
          sx={{
            gridTemplateColumns: { xs: '1fr', lg: '260px 1fr' },
            alignItems: 'center',
          }}
        >
          {/* Gauge */}
          <Box className="flex flex-col items-center">
            <Gauge
              width={220}
              height={220}
              value={gaugeValue}
              valueMin={0}
              valueMax={100}
              startAngle={-110}
              endAngle={110}
              innerRadius="78%"
              outerRadius="100%"
              cornerRadius="50%"
              text={({ value }) => `${Math.round(value ?? 0)}%`}
              sx={{
                [`& .${gaugeClasses.valueText}`]: {
                  fontSize: 32,
                  fontWeight: 700,
                  fill: semantic.ink,
                  transform: 'translateY(-6px)',
                },
                [`& .${gaugeClasses.valueArc}`]: { fill: gaugeColor },
                [`& .${gaugeClasses.referenceArc}`]: {
                  fill: alpha(semantic.neutral, 0.35),
                },
              }}
            />
            <Typography
              variant="body2"
              sx={{ mt: -1, color: semantic.expense, fontWeight: 600 }}
            >
              {remainingLabel}
            </Typography>
          </Box>

          {/* Progress rows */}
          <Box className="flex flex-col gap-4">
            <ProgressRow
              label="Days elapsed"
              detail={`${pacing.daysElapsed} / ${pacing.daysInMonth}`}
              percent={pacing.daysPct * 100}
              color={semantic.neutral}
              textColor={semantic.ink}
            />

            <ProgressRow
              label="Budget spent"
              detail={`${currency.format(pacing.actualExpenses)} / ${currency.format(pacing.plannedExpenses)}`}
              percent={pacing.spentPct * 100}
              color={gaugeColor}
              textColor={semantic.ink}
            />

            <Box>
              <Box className="flex items-baseline justify-between">
                <Typography
                  variant="body2"
                  sx={{ fontWeight: 600, color: semantic.ink }}
                >
                  Projection
                </Typography>
                <Typography variant="caption" sx={{ color: semantic.expense }}>
                  {`Projects to ${currency.format(pacing.projectedEnd)}`}
                </Typography>
              </Box>
              <Box sx={{ position: 'relative', mt: 1 }}>
                <LinearProgress
                  variant="determinate"
                  value={Math.min(projectionPct, 100)}
                  sx={{
                    height: 10,
                    borderRadius: 5,
                    backgroundColor: alpha(semantic.neutral, 0.35),
                    '& .MuiLinearProgress-bar': {
                      backgroundColor: overshoot ? semantic.overspend : semantic.income,
                    },
                  }}
                />
                {/* 100% marker */}
                <Box
                  sx={{
                    position: 'absolute',
                    top: -2,
                    bottom: -2,
                    left: 'calc(100% - 1px)',
                    width: 2,
                    backgroundColor: semantic.ink,
                    opacity: 0.5,
                  }}
                />
                {overshoot && (
                  <Typography
                    variant="caption"
                    sx={{
                      color: semantic.overspend,
                      fontWeight: 600,
                      display: 'block',
                      mt: 0.5,
                    }}
                  >
                    {`Overshoot: +${currency.format(pacing.projectedEnd - pacing.plannedExpenses)}`}
                  </Typography>
                )}
              </Box>
              <Typography
                variant="caption"
                sx={{ color: semantic.expense, mt: 0.5, display: 'block' }}
              >
                {pacing.perDiemToStay > 0
                  ? `Stay-on-plan rate: ${currencyPrecise.format(pacing.perDiemToStay)}/day`
                  : `No days remaining in this plan month`}
              </Typography>
            </Box>
          </Box>
        </Box>

        {drifting.length > 0 && (
          <Box sx={{ mt: 3, display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 1 }}>
            <Typography
              variant="caption"
              sx={{ color: semantic.expense, fontWeight: 600, mr: 1 }}
            >
              Drifting:
            </Typography>
            {drifting.map((d) => (
              <Chip
                key={d.key}
                label={`${d.name} +${currency.format(d.overBy)}`}
                size="small"
                variant="outlined"
                sx={{
                  borderColor: semantic.overspend,
                  color: semantic.ink,
                  backgroundColor: alpha(semantic.overspend, 0.08),
                  fontWeight: 500,
                }}
              />
            ))}
          </Box>
        )}
      </CardContent>
    </Card>
  );
};

interface ProgressRowProps {
  label: string;
  detail: string;
  percent: number;
  color: string;
  textColor: string;
}

const ProgressRow = ({ label, detail, percent, color, textColor }: ProgressRowProps) => {
  const theme = useTheme();
  const semantic = getSemanticColors(theme);
  return (
    <Box>
      <Box className="flex items-baseline justify-between">
        <Typography variant="body2" sx={{ fontWeight: 600, color: textColor }}>
          {label}
        </Typography>
        <Typography variant="caption" sx={{ color: semantic.expense }}>
          {detail}
        </Typography>
      </Box>
      <LinearProgress
        variant="determinate"
        value={clampPct(percent)}
        sx={{
          mt: 1,
          height: 10,
          borderRadius: 5,
          backgroundColor: alpha(semantic.neutral, 0.35),
          '& .MuiLinearProgress-bar': { backgroundColor: color },
        }}
      />
    </Box>
  );
};

export default PlanStoryHero;
