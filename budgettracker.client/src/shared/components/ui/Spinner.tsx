import { Loader2 } from 'lucide-react';
import { cn } from '../../utils/cn';

export interface SpinnerProps {
  size?: number;
  className?: string;
}

/** Indeterminate loading indicator (BUD-20). */
const Spinner = ({ size = 20, className }: SpinnerProps) => (
  <Loader2 size={size} className={cn('animate-spin text-ink-muted', className)} aria-label="Loading" />
);

export default Spinner;
