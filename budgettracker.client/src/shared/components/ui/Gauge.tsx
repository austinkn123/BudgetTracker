import { colorTokens, withAlpha } from '../../theme/tokens';

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
const Gauge = ({ value, size = 220, valueColor, trackColor, textColor }: GaugeProps) => {
  const clamped = Math.min(100, Math.max(0, value));
  const center = size / 2;
  // innerRadius 78% / outerRadius 100% of the old 110px ring → stroke centered
  // at 89% of half-size, stroke width 22% of half-size.
  const radius = center * 0.89;
  const strokeWidth = center * 0.22;

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
        stroke={trackColor ?? withAlpha(colorTokens.neutral.textPrimary, 0.08)}
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
        y={center - 6}
        textAnchor="middle"
        dominantBaseline="central"
        fill={textColor}
        style={{ fontSize: 32, fontWeight: 600 }}
      >
        {Math.round(clamped)}%
      </text>
    </svg>
  );
};

export default Gauge;
