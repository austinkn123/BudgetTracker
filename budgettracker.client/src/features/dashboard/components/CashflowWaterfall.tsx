import { useMemo } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { TooltipContentProps } from 'recharts';
import type { NameType, ValueType } from 'recharts/types/component/DefaultTooltipContent';
import Card from '../../../shared/components/ui/Card';
import { withAlpha } from '../../../shared/theme/tokens';
import type { WaterfallBar } from '../utils/selectors';
import { semanticColors } from '../utils/chartTheme';

interface CashflowWaterfallProps {
  /** Ordered bars: income → top expense categories → Other → net. */
  bars: WaterfallBar[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

interface WaterfallRow {
  label: string;
  base: number;
  income: number | null;
  expense: number | null;
  netPositive: number | null;
  netNegative: number | null;
}

/** Signed tooltip line per visible series. */
const SERIES_SIGNS: Record<string, string> = {
  Income: '+',
  Expense: '-',
  'Net surplus': '+',
  'Net deficit': '-',
};

const WaterfallTooltip = ({ active, payload, label }: TooltipContentProps<ValueType, NameType>) => {
  if (!active || !payload?.length) return null;
  // Hide the transparent float series.
  const visible = payload.filter((entry) => entry.name !== 'base' && entry.value != null);
  if (visible.length === 0) return null;

  return (
    <div className="rounded border border-border bg-surface px-3 py-2 text-sm shadow-md">
      <p className="mb-1 font-semibold text-ink">{label}</p>
      {visible.map((entry) => (
        <p key={String(entry.name)} className="text-ink-muted">
          {entry.name}: {SERIES_SIGNS[String(entry.name)] ?? ''}
          {currency.format(Math.abs(Number(entry.value ?? 0)))}
        </p>
      ))}
    </div>
  );
};

/**
 * Pseudo-waterfall on Recharts (BUD-20). The value column is split into
 * stacked series so each gets one color, with a transparent "base" series
 * floating bars to their running-total position.
 */
const CashflowWaterfall = ({ bars: items }: CashflowWaterfallProps) => {
  const rows = useMemo<WaterfallRow[] | null>(() => {
    if (items.length === 0) {
      return null;
    }

    let running = 0;
    return items.map((item) => {
      if (item.kind === 'net') {
        return {
          label: item.label,
          base: item.signed >= 0 ? 0 : item.signed,
          income: null,
          expense: null,
          netPositive: item.signed >= 0 ? item.value : null,
          netNegative: item.signed >= 0 ? null : item.value,
        };
      }

      if (item.kind === 'income') {
        const row: WaterfallRow = {
          label: item.label,
          base: running,
          income: item.value,
          expense: null,
          netPositive: null,
          netNegative: null,
        };
        running += item.value;
        return row;
      }

      running -= item.value;
      return {
        label: item.label,
        base: running,
        income: null,
        expense: item.value,
        netPositive: null,
        netNegative: null,
      };
    });
  }, [items]);

  if (!rows) {
    return (
      <Card
        title="Cashflow Waterfall"
        fullHeight
        contentClassName="flex min-h-[320px] items-center justify-center"
      >
        <p className="text-sm text-ink-muted">No transactions yet this plan month</p>
      </Card>
    );
  }

  return (
    <Card title="Cashflow Waterfall" fullHeight>
      <ResponsiveContainer width="100%" height={320}>
        <BarChart data={rows} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
          <CartesianGrid vertical={false} stroke={withAlpha(semanticColors.ink, 0.08)} />
          <XAxis
            dataKey="label"
            axisLine={false}
            tickLine={false}
            tick={{ fontSize: 12, fill: semanticColors.expense }}
          />
          <YAxis
            axisLine={false}
            tickLine={false}
            tick={{ fontSize: 12, fill: semanticColors.expense }}
            tickFormatter={(value: number) => currency.format(value)}
            width={72}
          />
          <Tooltip
            content={WaterfallTooltip}
            cursor={{ fill: withAlpha(semanticColors.ink, 0.04) }}
          />
          <Legend verticalAlign="top" align="center" iconType="circle" iconSize={8} />
          <Bar dataKey="base" stackId="w" fill="transparent" legendType="none" isAnimationActive={false} />
          <Bar dataKey="income" stackId="w" fill={semanticColors.income} name="Income" radius={[3, 3, 0, 0]} />
          <Bar dataKey="expense" stackId="w" fill={semanticColors.expense} name="Expense" radius={[3, 3, 0, 0]} />
          <Bar dataKey="netPositive" stackId="w" fill={semanticColors.income} name="Net surplus" radius={[3, 3, 0, 0]} />
          <Bar dataKey="netNegative" stackId="w" fill={semanticColors.overspend} name="Net deficit" radius={[3, 3, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </Card>
  );
};

export default CashflowWaterfall;
