import { useMemo } from 'react';
import { ResponsiveContainer, Sankey } from 'recharts';
import type { MoneyMapLine } from '../../../shared/types/api';
import { Card } from '../../../shared/components/ui';
import { cssVar } from '../../../shared/theme/tokens';
import { PHONE_QUERY, useMediaQuery } from '../../../shared/hooks/useMediaQuery';
import { currency } from '../../../shared/utils/format';
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
/** Phones: ~125px of right margin fits a cut name over its amount. */
const PHONE_LEAF_CHARS = 16;
/** Phone leaf labels are two lines (~28px), so leaves sit at least this far apart or labels overlap. */
const PHONE_GAP = 30;
/** Height for the bands themselves on phones, on top of the gaps between leaves. */
const PHONE_BANDS = 220;

const GROUP_STYLE: Record<FlowGroup['key'], { color: string }> = {
  mustPay: { color: cssVar('primary') },
  flexible: { color: cssVar('secondary') },
  saving: { color: cssVar('success') },
};

/**
 * Paychecks → must-pay / flexible / saving → each plan line. Band width is dollars per month.
 * `paychecks: false` (phones) starts at the three groups so the plan lines still fit beside them.
 */
const buildSankey = (groups: FlowGroup[], income: number, paychecks: boolean) => {
  const nodes: FlowNode[] = paychecks ? [{ name: 'Paychecks', amount: income, color: cssVar('ink-muted') }] : [];
  const links: FlowLink[] = [];
  const add = (node: FlowNode, from: number | null) => {
    const index = nodes.push(node) - 1;
    if (from !== null) links.push({ source: from, target: index, value: node.amount });
    return index;
  };
  for (const group of groups) {
    const { color } = GROUP_STYLE[group.key];
    const groupIndex = add({ name: group.label, amount: group.amount, color, share: group.share }, paychecks ? 0 : null);
    for (const line of group.lines) add({ name: line.name, amount: line.amount, color }, groupIndex);
  }
  return { nodes, links };
};

const shorten = (name: string, max: number) => (name.length > max ? `${name.slice(0, max - 1).trimEnd()}…` : name);

interface NodeShapeProps {
  x: number;
  y: number;
  width: number;
  height: number;
  payload: FlowNode & { depth: number };
  compact: boolean;
}

const NodeShape = ({ x, y, width, height, payload, compact }: NodeShapeProps) => {
  const ink = cssVar('ink');
  const muted = cssVar('ink-muted');
  const mid = y + height / 2;
  // Phones: groups are the first column, so their labels sit on the left like Paychecks' do.
  if (compact && payload.share !== undefined)
    return (
      <g>
        <rect x={x} y={y} width={width} height={height} rx={3} fill={payload.color} />
        <text x={x - 8} y={mid - 8} textAnchor="end" fontSize={13} fontWeight={600} fill={ink}>{payload.name}</text>
        <text x={x - 8} y={mid + 9} textAnchor="end" fontSize={11} fill={muted}>
          {currency.format(payload.amount)} · {Math.round(payload.share * 100)}%
        </text>
      </g>
    );
  if (compact)
    return (
      <g>
        <rect x={x} y={y} width={width} height={Math.max(height, 2)} rx={2} fill={payload.color} />
        <text x={x + width + 8} y={mid - 7} dominantBaseline="middle" fontSize={12} fontWeight={500} fill={ink}>
          <title>{payload.name}</title>
          {shorten(payload.name, PHONE_LEAF_CHARS)}
        </text>
        <text x={x + width + 8} y={mid + 8} dominantBaseline="middle" fontSize={11} fill={muted}>{currency.format(payload.amount)}</text>
      </g>
    );
  if (payload.depth === 0)
    return (
      <g>
        <rect x={x} y={y} width={width} height={height} rx={3} fill={payload.color} />
        <text x={x - 10} y={mid - 8} textAnchor="end" fontSize={14} fontWeight={600} fill={ink}>{payload.name}</text>
        <text x={x - 10} y={mid + 10} textAnchor="end" fontSize={12} fill={muted}>{currency.format(payload.amount)}</text>
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
        <tspan fontWeight={500}>{shorten(payload.name, MAX_LEAF_CHARS)}</tspan>
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

interface MoneyFlowProps {
  lines: MoneyMapLine[];
  income: number;
}

const MoneyFlow = ({ lines, income }: MoneyFlowProps) => {
  const isPhone = useMediaQuery(PHONE_QUERY);
  const groups = useMemo(() => planFlow(lines, income), [lines, income]);
  const sankey = useMemo(() => buildSankey(groups, income, !isPhone), [groups, income, isPhone]);
  const leaves = groups.reduce((n, g) => n + g.lines.length, 0);

  return (
    <Card
      title="Where a normal month’s pay goes"
      subtitle={
        isPhone
          ? `${currency.format(income)} of pay, per month. Yearly bills are spread across 12 months.`
          : 'The plan. A band’s width is dollars per month; yearly bills are spread across 12 months.'
      }
    >
      {/* Inline height: on phones it grows with the number of plan lines so their two-line labels never overlap. */}
      <div style={{ height: isPhone ? (leaves - 1) * PHONE_GAP + PHONE_BANDS : HEIGHT }}>
        <ResponsiveContainer width="100%" height="100%">
          <Sankey
            data={sankey}
            sort={false}
            nodeWidth={12}
            nodePadding={isPhone ? PHONE_GAP : 14}
            margin={isPhone ? { top: 8, right: 125, bottom: 8, left: 92 } : { top: 8, right: 230, bottom: 8, left: 130 }}
            node={(props) => <NodeShape {...(props as unknown as NodeShapeProps)} compact={isPhone} />}
            link={(props) => <LinkShape {...(props as unknown as LinkShapeProps)} />}
          />
        </ResponsiveContainer>
      </div>
    </Card>
  );
};

export default MoneyFlow;
