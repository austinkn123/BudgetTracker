import { useMemo } from 'react';
import { ResponsiveContainer, Sankey } from 'recharts';
import type { MoneyMapLine } from '../../../shared/types/api';
import { Card } from '../../../shared/components/ui';
import { cssVar } from '../../../shared/theme/tokens';
import { currency, percent } from '../../../shared/utils/format';
import { cn } from '../../../shared/utils/cn';
import { PHONE_QUERY, useMediaQuery } from '../../../shared/hooks/useMediaQuery';
import { planFlow } from '../utils/moneyMap';
import type { FlowGroup } from '../utils/moneyMap';

interface FlowNode {
  name: string;
  amount: number;
  color: string;
  /** Group nodes caption their share of pay under the name. */
  share?: number;
}

interface FlowLink {
  source: number;
  target: number;
  value: number;
}

const HEIGHT = 520;
/** Leaf labels get 230px of right margin; longer names are cut and keep the full text in a tooltip. */
const MAX_LEAF_CHARS = 28;

const GROUP_STYLE: Record<FlowGroup['key'], { color: string; bar: string }> = {
  mustPay: { color: cssVar('primary'), bar: 'bg-primary' },
  flexible: { color: cssVar('secondary'), bar: 'bg-secondary' },
  saving: { color: cssVar('success'), bar: 'bg-success' },
};

/** Paychecks → must-pay / flexible / saving → each plan line. Band width is dollars per month. */
const buildSankey = (groups: FlowGroup[], income: number) => {
  const nodes: FlowNode[] = [{ name: 'Paychecks', amount: income, color: cssVar('ink-muted') }];
  const links: FlowLink[] = [];
  const add = (node: FlowNode, from: number) => {
    links.push({ source: from, target: nodes.push(node) - 1, value: node.amount });
    return nodes.length - 1;
  };
  for (const group of groups) {
    const { color } = GROUP_STYLE[group.key];
    const groupIndex = add({ name: group.label, amount: group.amount, color, share: group.share }, 0);
    for (const line of group.lines) add({ name: line.name, amount: line.amount, color }, groupIndex);
  }
  return { nodes, links };
};

const shorten = (name: string) => (name.length > MAX_LEAF_CHARS ? `${name.slice(0, MAX_LEAF_CHARS - 1).trimEnd()}…` : name);

interface NodeShapeProps {
  x: number;
  y: number;
  width: number;
  height: number;
  payload: FlowNode & { depth: number };
}

const NodeShape = ({ x, y, width, height, payload }: NodeShapeProps) => {
  const ink = cssVar('ink');
  const muted = cssVar('ink-muted');
  const mid = y + height / 2;
  if (payload.depth === 0)
    return (
      <g>
        <rect x={x} y={y} width={width} height={height} rx={3} fill={payload.color} />
        <text x={x - 10} y={mid - 8} textAnchor="end" fontSize={14} fontWeight={600} fill={ink}>{payload.name}</text>
        <text x={x - 10} y={mid + 10} textAnchor="end" fontSize={12} fill={muted}>{currency.format(payload.amount)} a month</text>
      </g>
    );
  if (payload.share !== undefined)
    return (
      <g>
        <rect x={x} y={y} width={width} height={height} rx={3} fill={payload.color} />
        <text x={x + width + 10} y={y + 16} fontSize={14} fontWeight={600} fill={ink}>{payload.name}</text>
        <text x={x + width + 10} y={y + 33} fontSize={12} fill={muted}>
          {currency.format(payload.amount)} · {Math.round(payload.share * 100)}%
        </text>
      </g>
    );
  return (
    <g>
      <rect x={x} y={y} width={width} height={Math.max(height, 2)} rx={2} fill={payload.color} />
      <text x={x + width + 10} y={mid} dominantBaseline="middle" fontSize={12} fill={ink}>
        <title>{payload.name}</title>
        <tspan fontWeight={500}>{shorten(payload.name)}</tspan>
        <tspan fill={muted}>{`  ${currency.format(payload.amount)}`}</tspan>
      </text>
    </g>
  );
};

interface LinkShapeProps {
  sourceX: number;
  targetX: number;
  sourceY: number;
  targetY: number;
  sourceControlX: number;
  targetControlX: number;
  linkWidth: number;
  payload: { target: FlowNode };
}

const LinkShape = ({ sourceX, targetX, sourceY, targetY, sourceControlX, targetControlX, linkWidth, payload }: LinkShapeProps) => (
  <path
    d={`M${sourceX},${sourceY} C${sourceControlX},${sourceY} ${targetControlX},${targetY} ${targetX},${targetY}`}
    fill="none"
    stroke={payload.target.color}
    strokeOpacity={0.35}
    strokeWidth={linkWidth}
  />
);

/** Phone layout: the Sankey's side labels need ~360px of margin, so each group becomes a bar and a list. */
const FlowList = ({ groups }: { groups: FlowGroup[] }) => (
  <ul className="flex flex-col gap-5">
    {groups.map((group) => (
      <li key={group.key}>
        <div className="flex items-baseline justify-between gap-3">
          <span className="flex items-center gap-2 text-sm font-semibold text-ink">
            <span className={cn('h-2.5 w-2.5 rounded-full', GROUP_STYLE[group.key].bar)} aria-hidden />
            {group.label}
          </span>
          <span className="numeric shrink-0 text-sm text-ink-muted">
            <span className="font-semibold text-ink">{currency.format(group.amount)}</span> · {percent(group.share)}
          </span>
        </div>
        <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-border-subtle" aria-hidden>
          {/* Inline width: the bar's length is data (share of pay), which no utility class can express. */}
          <div
            className={cn('h-full rounded-full', GROUP_STYLE[group.key].bar)}
            style={{ width: `${Math.min(group.share, 1) * 100}%` }}
          />
        </div>
        <ul className="mt-3 flex flex-col gap-2 border-l border-border-subtle pl-3">
          {group.lines.map((line) => (
            <li key={line.name} className="flex items-baseline justify-between gap-3 text-sm">
              <span className="min-w-0 truncate text-ink" title={line.name}>
                {line.name}
              </span>
              <span className="numeric shrink-0 text-ink-muted">{currency.format(line.amount)}</span>
            </li>
          ))}
        </ul>
      </li>
    ))}
  </ul>
);

interface MoneyFlowProps {
  lines: MoneyMapLine[];
  income: number;
}

const MoneyFlow = ({ lines, income }: MoneyFlowProps) => {
  const isPhone = useMediaQuery(PHONE_QUERY);
  const groups = useMemo(() => planFlow(lines, income), [lines, income]);
  const sankey = useMemo(() => buildSankey(groups, income), [groups, income]);

  return (
    <Card
      title="Where a normal month’s pay goes"
      subtitle={
        isPhone
          ? 'The plan, per month. Yearly bills are spread across 12 months.'
          : 'The plan. A band’s width is dollars per month; yearly bills are spread across 12 months.'
      }
    >
      {isPhone ? (
        <FlowList groups={groups} />
      ) : (
        <div style={{ height: HEIGHT }}>
          <ResponsiveContainer width="100%" height="100%">
            <Sankey
              data={sankey}
              sort={false}
              nodeWidth={12}
              nodePadding={14}
              margin={{ top: 8, right: 230, bottom: 8, left: 130 }}
              node={(props) => <NodeShape {...(props as unknown as NodeShapeProps)} />}
              link={(props) => <LinkShape {...(props as unknown as LinkShapeProps)} />}
            />
          </ResponsiveContainer>
        </div>
      )}
    </Card>
  );
};

export default MoneyFlow;
