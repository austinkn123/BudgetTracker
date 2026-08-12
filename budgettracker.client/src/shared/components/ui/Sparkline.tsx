import { useId } from 'react';
import { withAlpha } from '../../theme/tokens';

export interface SparklineProps {
  data: readonly number[];
  height?: number;
  /** Line/area color as a hex token value (alpha derived internally). */
  color: string;
}

const VIEW_WIDTH = 100;

/**
 * Monotone-cubic control points (Fritsch–Carlson-ish tangents) — smooth
 * without overshooting past data extremes.
 */
const buildSmoothPath = (points: readonly [number, number][]): string => {
  if (points.length < 2) return '';
  let d = `M ${points[0][0]} ${points[0][1]}`;
  for (let i = 0; i < points.length - 1; i++) {
    const [x0, y0] = points[Math.max(0, i - 1)];
    const [x1, y1] = points[i];
    const [x2, y2] = points[i + 1];
    const [x3, y3] = points[Math.min(points.length - 1, i + 2)];
    const c1x = x1 + (x2 - x0) / 6;
    const c1y = y1 + (y2 - y0) / 6;
    const c2x = x2 - (x3 - x1) / 6;
    const c2y = y2 - (y3 - y1) / 6;
    d += ` C ${c1x} ${c1y}, ${c2x} ${c2y}, ${x2} ${y2}`;
  }
  return d;
};

/**
 * Tiny area sparkline (BUD-20). Replaces @mui/x-charts SparkLineChart.
 * Hover highlighting was intentionally dropped — the spark sits inside a
 * card-wide click target and the two fought each other.
 */
const Sparkline = ({ data, height = 40, color }: SparklineProps) => {
  const gradientId = useId();

  if (data.length < 2) return null;

  const max = Math.max(...data);
  const min = Math.min(...data);
  const range = max - min || 1;
  const pad = height * 0.15;

  const points: [number, number][] = data.map((value, index) => [
    (index / (data.length - 1)) * VIEW_WIDTH,
    pad + (1 - (value - min) / range) * (height - pad * 2),
  ]);

  const linePath = buildSmoothPath(points);
  const areaPath = `${linePath} L ${VIEW_WIDTH} ${height} L 0 ${height} Z`;

  return (
    <svg
      width="100%"
      height={height}
      viewBox={`0 0 ${VIEW_WIDTH} ${height}`}
      preserveAspectRatio="none"
      aria-hidden
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={withAlpha(color, 0.25)} />
          <stop offset="100%" stopColor={withAlpha(color, 0.02)} />
        </linearGradient>
      </defs>
      <path d={areaPath} fill={`url(#${gradientId})`} />
      <path
        d={linePath}
        fill="none"
        stroke={color}
        strokeWidth={1.5}
        strokeLinecap="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
};

export default Sparkline;
