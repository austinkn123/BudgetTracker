import { cn } from '../../utils/cn';

export interface SkeletonProps {
  variant?: 'text' | 'circular' | 'rectangular';
  width?: number | string;
  height?: number | string;
  className?: string;
}

/** Loading placeholder (BUD-20). */
const Skeleton = ({ variant = 'text', width, height, className }: SkeletonProps) => (
  <div
    aria-hidden
    className={cn(
      'animate-pulse bg-border-subtle',
      variant === 'circular' ? 'rounded-full' : 'rounded',
      variant === 'text' && 'h-4',
      className,
    )}
    style={{ width, height }}
  />
);

export default Skeleton;
