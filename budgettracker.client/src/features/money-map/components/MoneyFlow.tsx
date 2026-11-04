import { useMemo } from 'react';
import { ResponsiveContainer, Sankey } from 'recharts';
import type { MoneyMapLine } from '../../../shared/types/api';
import { Card } from '../../../shared/components/ui';
import { cssVar } from '../../../shared/theme/tokens';
import { currency } from '../../../shared/utils/format';

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

/** Paychecks → must-pay / flexible / saving → each plan line. Band width is dollars per month. */
const buildFlow = (lines: MoneyMapLine[], income: number) => {
  const nodes: FlowNode[] = [{ name: 'Paychecks', amount: income, color: cssVar('ink-muted') }];
  const links: FlowLink[] = [];
  const add = (node: FlowNode, from: number) => {
    links.push({ source: from, target: nodes.push(node) - 1, value: node.amount });
    return nodes.length - 1;
  };

  let planned = 0;
  for (const [bucket, label, color] of [
    ['Core', 'Must-pay', cssVar('primary')],
    ['Buffer', 'Flexible', cssVar('secondary')],
  ] as const) {
    const group = lines.filter((l) => l.bucket === bucket && l.planMonthly > 0);
    const total = group.reduce((s, l) => s + l.planMonthly, 0);
    if (total === 0) continue;
    planned += total;
    const groupIndex = add({ name: label, amount: total, color, share: total / income }, 0);
    for (const line of group) add({ name: line.name, amount: line.planMonthly, color }, groupIndex);
  }

  const saving = income - planned;
  if (saving > 0) {
    const savingIndex = add({ name: 'Saving', amount: saving, color: cssVar('success'), share: saving / income }, 0);
    add({ name: 'Cushion, then investing', amount: saving, color: cssVar('success') }, savingIndex);
  }
  return { nodes, links };
};

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
        <tspan fontWeight={500}>{payload.name}</tspan>
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
  const data = useMemo(() => buildFlow(lines, income), [lines, income]);

  return (
    <Card
      title="Where a normal month’s pay goes"
      subtitle="The plan. A band’s width is dollars per month; yearly bills are spread across 12 months."
    >
      <div style={{ height: HEIGHT }}>
        <ResponsiveContainer width="100%" height="100%">
          <Sankey
            data={data}
            sort={false}
            nodeWidth={12}
            nodePadding={14}
            margin={{ top: 8, right: 230, bottom: 8, left: 130 }}
            node={(props) => <NodeShape {...(props as unknown as NodeShapeProps)} />}
            link={(props) => <LinkShape {...(props as unknown as LinkShapeProps)} />}
          />
        </ResponsiveContainer>
      </div>
    </Card>
  );
};

export default MoneyFlow;
