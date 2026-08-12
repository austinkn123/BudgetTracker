import * as RadixProgress from '@radix-ui/react-progress';
import { cn } from '../../utils/cn';

export interface ProgressProps {
  /** 0–100. */
  value: number;
  /** Track height in px. */
  height?: number;
  /** CSS color for the filled bar (token-derived; charts pass withAlpha output). */
  barColor?: string;
  /** CSS color for the track. */
  trackColor?: string;
  className?: string;
}

/** Determinate progress bar (BUD-20). Replaces MUI LinearProgress. */
const Progress = ({ value, height = 8, barColor, trackColor, className }: ProgressProps) => {
  const clamped = Math.min(100, Math.max(0, value));

  return (
    <RadixProgress.Root
      value={clamped}
      className={cn('relative w-full overflow-hidden rounded-full bg-border-subtle', className)}
      style={{ height, backgroundColor: trackColor }}
    >
      <RadixProgress.Indicator
        className="h-full rounded-full bg-primary transition-transform duration-160 ease-out-soft"
        style={{
          transform: `translateX(-${100 - clamped}%)`,
          backgroundColor: barColor,
        }}
      />
    </RadixProgress.Root>
  );
};

export default Progress;
