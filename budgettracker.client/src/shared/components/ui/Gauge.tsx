import { cssVar } from '../../theme/tokens';

export interface GaugeProps {
  /** 0–100. */
  value: number;
  size?: number;
  /** CSS color of the value arc (token-derived). */
  valueColor: string;
  /** CSS color of the reference track; defaults to a translucent valueColor. */
  trackColor?: string;
  /** CSS color of the centered text. */
  textColor: string;
}

/** Sweep matches the old MUI Gauge: −110° → 110° (220° of arc). */
const START_ANGLE = -110;
const END_ANGLE = 110;

/** Point on the ring at `angle` degrees clockwise from 12 o'clock. */
const polar = (center: number, radius: number, angle: number): [number, number] => {
  const rad = (angle * Math.PI) / 180;
  return [center + radius * Math.sin(rad), center - radius * Math.cos(rad)];
};

/**
 * Static SVG gauge (BUD-20). Replaces @mui/x-charts Gauge — two stroked arcs
 * with round caps plus centered text; no interactivity by design.
 */
const Gauge = ({ value, size = 200, valueColor, trackColor, textColor }: GaugeProps) => {
  const clamped = Math.min(100, Math.max(0, value));
  const center = size / 2;
  // Slim ring: a thinner arc reads as precision instrumentation rather than
  // a chunky progress donut.
  const radius = center * 0.86;
  const strokeWidth = center * 0.1;

  const [startX, startY] = polar(center, radius, START_ANGLE);
  const [endX, endY] = polar(center, radius, END_ANGLE);
  const arcPath = `M ${startX} ${startY} A ${radius} ${radius} 0 1 1 ${endX} ${endY}`;

  return (
    <svg
      width={size}
      height={size}
      viewBox={`0 0 ${size} ${size}`}
      role="meter"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={Math.round(clamped)}
    >
      <path
        d={arcPath}
        fill="none"
        stroke={trackColor ?? cssVar('ink', 0.08)}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
        pathLength={100}
      />
      {clamped > 0 && (
        <path
          d={arcPath}
          fill="none"
          stroke={valueColor}
          strokeWidth={strokeWidth}
          strokeLinecap="round"
          pathLength={100}
          strokeDasharray={`${clamped} 100`}
          className="transition-all duration-240 ease-out-soft"
        />
      )}
      <text
        x={center}
        y={center - 4}
        textAnchor="middle"
        dominantBaseline="central"
        fill={textColor}
        style={{
          fontSize: size * 0.24,
          fontWeight: 600,
          letterSpacing: '-0.03em',
          fontVariantNumeric: 'tabular-nums',
        }}
      >
        {Math.round(clamped)}%
      </text>
      <text
        x={center}
        y={center + size * 0.16}
        textAnchor="middle"
        dominantBaseline="central"
        fill={textColor}
        opacity={0.5}
        style={{
          fontSize: size * 0.055,
          fontWeight: 600,
          letterSpacing: '0.08em',
          textTransform: 'uppercase',
        }}
      >
        of plan
      </text>
    </svg>
  );
};

export default Gauge;
