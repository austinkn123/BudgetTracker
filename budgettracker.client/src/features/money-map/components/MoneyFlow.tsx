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
const PHONE_HEIGHT = 300;
/** Leaf labels get 230px of right margin; longer names are cut and keep the full text in a tooltip. */
const MAX_LEAF_CHARS = 28;

const GROUP_STYLE: Record<FlowGroup['key'], { color: string }> = {
  mustPay: { color: cssVar('primary') },
  flexible: { color: cssVar('secondary') },
  saving: { color: cssVar('success') },
};

/** Paychecks → must-pay / flexible / saving → each plan line. Band width is dollars per month. */
/** `leaves: false` stops at the three groups: a phone has no room for each plan line's label. */
const buildSankey = (groups: FlowGroup[], income: number, leaves: boolean) => {
  const nodes: FlowNode[] = [{ name: 'Paychecks', amount: income, color: cssVar('ink-muted') }];
  const links: FlowLink[] = [];
  const add = (node: FlowNode, from: number) => {
    links.push({ source: from, target: nodes.push(node) - 1, value: node.amount });
    return nodes.length - 1;
  };
  for (const group of groups) {
    const { color } = GROUP_STYLE[group.key];
    const groupIndex = add({ name: group.label, amount: group.amount, color, share: group.share }, 0);
    if (leaves) for (const line of group.lines) add({ name: line.name, amount: line.amount, color }, groupIndex);
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

interface MoneyFlowProps {
  lines: MoneyMapLine[];
  income: number;
}

const MoneyFlow = ({ lines, income }: MoneyFlowProps) => {
  const isPhone = useMediaQuery(PHONE_QUERY);
  const groups = useMemo(() => planFlow(lines, income), [lines, income]);
  const sankey = useMemo(() => buildSankey(groups, income, !isPhone), [groups, income, isPhone]);

  return (
    <Card
      title="Where a normal month’s pay goes"
      subtitle="The plan. A band’s width is dollars per month; yearly bills are spread across 12 months."
    >
      <div style={{ height: isPhone ? PHONE_HEIGHT : HEIGHT }}>
        <ResponsiveContainer width="100%" height="100%">
          <Sankey
            data={sankey}
            sort={false}
            nodeWidth={12}
            nodePadding={isPhone ? 24 : 14}
            margin={isPhone ? { top: 8, right: 100, bottom: 8, left: 90 } : { top: 8, right: 230, bottom: 8, left: 130 }}
            node={(props) => <NodeShape {...(props as unknown as NodeShapeProps)} />}
            link={(props) => <LinkShape {...(props as unknown as LinkShapeProps)} />}
          />
        </ResponsiveContainer>
      </div>
    </Card>
  );
};

export default MoneyFlow;
