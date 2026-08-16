import { useMemo } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
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
  /** [low, high] — Recharts renders a floating bar from a 2-tuple dataKey. */
  range: [number, number];
  amount: number;
  kind: WaterfallBar['kind'];
  positive: boolean;
}

const LEGEND = [
  { label: 'Income', color: semanticColors.income },
  { label: 'Expense', color: semanticColors.expense },
  { label: 'Net deficit', color: semanticColors.overspend },
] as const;

const barColor = (row: WaterfallRow): string => {
  if (row.kind === 'income') return semanticColors.income;
  if (row.kind === 'net') return row.positive ? semanticColors.income : semanticColors.overspend;
  return semanticColors.expense;
};

const WaterfallTooltip = ({ active, payload }: TooltipContentProps<ValueType, NameType>) => {
  if (!active || !payload?.length) return null;
  const row = payload[0]?.payload as WaterfallRow | undefined;
  if (!row) return null;

  const sign = row.kind === 'expense' || !row.positive ? '−' : '+';

  return (
    <div className="rounded border border-border bg-surface px-3 py-2 shadow-md">
      <p className="text-sm font-semibold text-ink">{row.label}</p>
      <p className="text-sm text-ink-muted">
        {sign}
        {currency.format(Math.abs(row.amount))}
      </p>
    </div>
  );
};

/**
 * Waterfall on Recharts (BUD-20). Uses range bars ([low, high] tuples) rather
 * than a transparent stacked "base" series — Recharts stacks positive and
 * negative values in opposite directions, so the float trick renders nothing.
 */
const CashflowWaterfall = ({ bars: items }: CashflowWaterfallProps) => {
  const rows = useMemo<WaterfallRow[] | null>(() => {
    if (items.length === 0) return null;

    let running = 0;
    return items.map((item) => {
      if (item.kind === 'net') {
        const positive = item.signed >= 0;
        return {
          label: item.label,
          range: (positive ? [0, item.signed] : [item.signed, 0]) as [number, number],
          amount: item.value,
          kind: item.kind,
          positive,
        };
      }

      if (item.kind === 'income') {
        const start = running;
        running += item.value;
        return {
          label: item.label,
          range: [start, running] as [number, number],
          amount: item.value,
          kind: item.kind,
          positive: true,
        };
      }

      const start = running;
      running -= item.value;
      return {
        label: item.label,
        range: [running, start] as [number, number],
        amount: item.value,
        kind: item.kind,
        positive: false,
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
    <Card
      title="Cashflow Waterfall"
      actions={
        <div className="flex flex-wrap items-center gap-3">
          {LEGEND.map((entry) => (
            <span key={entry.label} className="flex items-center gap-1.5 text-xs text-ink-muted">
              <span
                className="h-2 w-2 rounded-full"
                style={{ backgroundColor: entry.color }}
                aria-hidden
              />
              {entry.label}
            </span>
          ))}
        </div>
      }
      fullHeight
    >
      <ResponsiveContainer width="100%" height={320}>
        <BarChart data={rows} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
          <CartesianGrid vertical={false} stroke={withAlpha(semanticColors.ink, 0.08)} />
          <XAxis
            dataKey="label"
            axisLine={false}
            tickLine={false}
            interval={0}
            tick={{ fontSize: 11, fill: semanticColors.expense }}
          />
          <YAxis
            axisLine={false}
            tickLine={false}
            tick={{ fontSize: 11, fill: semanticColors.expense }}
            tickFormatter={(value: number) => currency.format(value)}
            width={72}
          />
          <Tooltip content={WaterfallTooltip} cursor={{ fill: withAlpha(semanticColors.ink, 0.04) }} />
          <Bar dataKey="range" radius={3}>
            {rows.map((row) => (
              <Cell key={row.label} fill={barColor(row)} />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </Card>
  );
};

export default CashflowWaterfall;
