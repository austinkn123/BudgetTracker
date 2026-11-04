import { useEffect, useMemo, useRef } from 'react';
import type { KeyboardEvent, RefObject } from 'react';
import {
  Bar,
  CartesianGrid,
  Cell,
  ComposedChart,
  Line,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  useActiveTooltipLabel,
} from 'recharts';
import type { TooltipContentProps } from 'recharts';
import type { NameType, ValueType } from 'recharts/types/component/DefaultTooltipContent';
import { Card } from '../../../shared/components/ui';
import { withAlpha } from '../../../shared/theme/tokens';
import type { CashFlowGrouping, CashFlowPeriod } from '../../../shared/types/api';
import { currency, signedCurrency } from '../../../shared/utils/format';
import { semanticColors } from '../../dashboard/utils/chartTheme';
import { formatPeriod, formatTick } from '../utils/cashFlow';

interface CashFlowChartProps {
  periods: CashFlowPeriod[];
  grouping: CashFlowGrouping;
  /** `start` of the selected bucket, or null. */
  selected: string | null;
  onSelect: (start: string | null) => void;
}

interface ChartRow extends CashFlowPeriod {
  /** Money out negated, so it draws below the axis. The tooltip shows `moneyOut`. */
  out: number;
}

const LEGEND = [
  { label: 'Money in', color: semanticColors.income, line: false },
  { label: 'Money out', color: semanticColors.expense, line: false },
  { label: 'Net', color: semanticColors.neutral, line: true },
] as const;

const AXIS_TICK = { fontSize: 11, fill: semanticColors.expense };
// Recharts' default list minus 'mousemove'.
const UNTHROTTLED_MOUSEMOVE = ['touchmove', 'pointermove', 'scroll', 'wheel'] as const;

const FlowTooltip = ({
  active,
  payload,
  grouping,
}: TooltipContentProps<ValueType, NameType> & { grouping: CashFlowGrouping }) => {
  const row = payload?.[0]?.payload as ChartRow | undefined;
  if (!active || !row) return null;

  return (
    <div className="rounded border border-border bg-surface px-3 py-2 shadow-md">
      <p className="text-sm font-semibold text-ink">{formatPeriod(row.start, grouping)}</p>
      <dl className="mt-1 grid grid-cols-[auto_auto] gap-x-4 gap-y-0.5 text-sm">
        <dt className="text-ink-muted">Money in</dt>
        <dd className="numeric text-right text-ink">{currency.format(row.moneyIn)}</dd>
        <dt className="text-ink-muted">Money out</dt>
        <dd className="numeric text-right text-ink">{currency.format(row.moneyOut)}</dd>
        <dt className="text-ink-muted">Net</dt>
        <dd className="numeric text-right font-semibold text-ink">{signedCurrency(row.net)}</dd>
      </dl>
    </div>
  );
};

/**
 * Recharts tracks the hovered or arrow-key-focused bucket internally; this mirrors it into a ref
 * so Enter can select the bucket a keyboard user has moved to. A ref, not state: it changes on
 * every mouse move and nothing needs to re-render for it.
 */
const ActiveLabelTracker = ({ into }: { into: RefObject<unknown> }) => {
  const label = useActiveTooltipLabel();
  useEffect(() => {
    into.current = label;
  }, [into, label]);
  return null;
};

/**
 * Money in rises above the axis, money out hangs below it, and net runs through both as a line.
 * Recharts' draw-in animation defaults to 'auto', which already honours prefers-reduced-motion.
 */
const CashFlowChart = ({ periods, grouping, selected, onSelect }: CashFlowChartProps) => {
  const activeLabel = useRef<unknown>(undefined);
  const rows = useMemo<ChartRow[]>(
    () => periods.map((p) => ({ ...p, out: -p.moneyOut })),
    [periods],
  );
  const isEmpty = periods.every((p) => p.moneyIn === 0 && p.moneyOut === 0);

  const toggle = (label: unknown) => {
    if (typeof label !== 'string') return;
    onSelect(label === selected ? null : label);
  };

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault();
    toggle(activeLabel.current);
  };

  // Unselected buckets fade back once one is picked, so the eye goes to the one in the list below.
  const opacityFor = (row: ChartRow) => (selected === null || row.start === selected ? 1 : 0.35);

  return (
    <Card
      title="Money in and out"
      actions={
        // Hidden on narrow screens: the legend would crowd the title out.
        <div className="hidden flex-wrap items-center gap-3 sm:flex">
          {LEGEND.map((entry) => (
            <span key={entry.label} className="flex items-center gap-1.5 text-xs text-ink-muted">
              <span
                className={entry.line ? 'h-0.5 w-3 rounded-full' : 'h-2 w-2 rounded-full'}
                style={{ backgroundColor: entry.color }}
                aria-hidden
              />
              {entry.label}
            </span>
          ))}
        </div>
      }
    >
      {isEmpty ? (
        <div className="flex min-h-[320px] flex-col items-center justify-center gap-1 text-center">
          <p className="text-sm font-medium text-ink">No money moved in this range</p>
          <p className="text-xs text-ink-muted">
            Try a wider range or clear the filters. New activity syncs in from your linked banks.
          </p>
        </div>
      ) : (
        <>
          <div className="cursor-pointer" onKeyDown={onKeyDown}>
            <ResponsiveContainer width="100%" height={320}>
              <ComposedChart
                data={rows}
                stackOffset="sign"
                margin={{ top: 8, right: 8, bottom: 0, left: 0 }}
                // onClick reports the hovered bucket, and hover is rAF-throttled by default; a tap's
                // emulated mousemove would land a frame after its click and select the previous bar.
                throttledEvents={UNTHROTTLED_MOUSEMOVE}
                onClick={(state) => toggle(state.activeLabel)}
              >
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
                  tickFormatter={(value: number) => currency.format(value)}
                  tick={AXIS_TICK}
                />
                <ReferenceLine y={0} stroke={withAlpha(semanticColors.ink, 0.2)} />
                <Tooltip
                  content={(props) => <FlowTooltip {...props} grouping={grouping} />}
                  cursor={{ fill: withAlpha(semanticColors.ink, 0.04) }}
                />
                {/* Shared stack + sign offset: in and out share one column, growing apart from 0.
                    Radius applies at each bar's value end, so both round on their outer edge. */}
                <Bar dataKey="moneyIn" name="Money in" stackId="flow" fill={semanticColors.income} radius={[3, 3, 0, 0]}>
                  {rows.map((row) => (
                    <Cell key={row.start} fillOpacity={opacityFor(row)} />
                  ))}
                </Bar>
                <Bar dataKey="out" name="Money out" stackId="flow" fill={semanticColors.expense} radius={[3, 3, 0, 0]}>
                  {rows.map((row) => (
                    <Cell key={row.start} fillOpacity={opacityFor(row)} />
                  ))}
                </Bar>
                <Line
                  dataKey="net"
                  name="Net"
                  type="monotone"
                  stroke={semanticColors.neutral}
                  strokeWidth={2}
                  dot={false}
                  activeDot={{ r: 4 }}
                />
                <ActiveLabelTracker into={activeLabel} />
              </ComposedChart>
            </ResponsiveContainer>
          </div>
          <p className="mt-2 text-xs text-ink-muted">
            {selected
              ? 'Select the same bar again to clear it.'
              : 'Select a bar to see its transactions. With the chart focused, use the arrow keys and Enter.'}
          </p>
        </>
      )}
    </Card>
  );
};

export default CashFlowChart;
