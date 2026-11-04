import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { TooltipContentProps } from 'recharts';
import type { NameType, ValueType } from 'recharts/types/component/DefaultTooltipContent';
import { Card } from '../../../shared/components/ui';
import { withAlpha } from '../../../shared/theme/tokens';
import type { CashFlowGrouping, CashFlowPeriod } from '../../../shared/types/api';
import { currency } from '../../../shared/utils/format';
import { useCountUp } from '../../../shared/hooks/useCountUp';
import { chartPalette, semanticColors } from '../../dashboard/utils/chartTheme';
import { formatPeriod, formatTick } from '../utils/cashFlow';

interface BalanceChartProps {
  periods: CashFlowPeriod[];
  grouping: CashFlowGrouping;
  currentBalance: number;
}

// Brand blue: balance is its own measure, so it takes neither the income green nor the net teal.
const BALANCE_COLOR = chartPalette[0];
const AXIS_TICK = { fontSize: 11, fill: semanticColors.expense };

const BalanceTooltip = ({
  active,
  payload,
  grouping,
}: TooltipContentProps<ValueType, NameType> & { grouping: CashFlowGrouping }) => {
  const row = payload?.[0]?.payload as CashFlowPeriod | undefined;
  if (!active || !row || row.balance === null) return null;

  return (
    <div className="rounded border border-border bg-surface px-3 py-2 shadow-md">
      <p className="text-sm font-semibold text-ink">{formatPeriod(row.start, grouping)}</p>
      <p className="numeric text-sm text-ink-muted">{currency.format(row.balance)} at period end</p>
    </div>
  );
};

/** End-of-period balance across checking and savings, less card debt. */
const BalanceChart = ({ periods, grouping, currentBalance }: BalanceChartProps) => {
  const shown = useCountUp(currentBalance);

  return (
    <Card
      title="Balance"
      subtitle="Checking and savings, less card debt"
      actions={
        <p className="numeric text-xl font-semibold tracking-[-0.02em] text-ink">
          <span aria-hidden>{currency.format(shown)}</span>
          <span className="sr-only">{currency.format(currentBalance)}</span>
        </p>
      }
    >
      <ResponsiveContainer width="100%" height={200}>
        <AreaChart data={periods} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
          <CartesianGrid vertical={false} stroke={withAlpha(semanticColors.ink, 0.08)} />
          <XAxis
            dataKey="start"
            axisLine={false}
            tickLine={false}
            minTickGap={16}
            tickFormatter={(value: string) => formatTick(value, grouping)}
            tick={AXIS_TICK}
          />
          <YAxis
            axisLine={false}
            tickLine={false}
            width={72}
            domain={['auto', 'auto']}
            tickFormatter={(value: number) => currency.format(value)}
            tick={AXIS_TICK}
          />
          <Tooltip
            content={(props) => <BalanceTooltip {...props} grouping={grouping} />}
            cursor={{ stroke: withAlpha(semanticColors.ink, 0.2) }}
          />
          <Area
            dataKey="balance"
            type="monotone"
            stroke={BALANCE_COLOR}
            strokeWidth={2}
            fill={withAlpha(BALANCE_COLOR, 0.12)}
          />
        </AreaChart>
      </ResponsiveContainer>
    </Card>
  );
};

export default BalanceChart;
