import * as RadixSeparator from '@radix-ui/react-separator';
import { cn } from '../../utils/cn';

export interface SeparatorProps {
  orientation?: 'horizontal' | 'vertical';
  className?: string;
}

/** Themed divider (BUD-20). */
const Separator = ({ orientation = 'horizontal', className }: SeparatorProps) => (
  <RadixSeparator.Root
    orientation={orientation}
    className={cn(
      'shrink-0 bg-border-subtle',
      orientation === 'horizontal' ? 'h-px w-full' : 'h-full w-px',
      className,
    )}
  />
);

export default Separator;
