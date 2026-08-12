import type { ReactNode } from 'react';
import * as RadixCollapsible from '@radix-ui/react-collapsible';
import { cn } from '../../utils/cn';

export interface CollapsibleProps {
  open: boolean;
  className?: string;
  children: ReactNode;
}

/**
 * Controlled expand/collapse region (BUD-20). Replaces MUI Collapse; the
 * trigger lives with the caller — this only animates the content.
 */
const Collapsible = ({ open, className, children }: CollapsibleProps) => (
  <RadixCollapsible.Root open={open}>
    <RadixCollapsible.Content
      className={cn(
        'overflow-hidden data-[state=closed]:animate-collapse-up data-[state=open]:animate-collapse-down',
        className,
      )}
    >
      {children}
    </RadixCollapsible.Content>
  </RadixCollapsible.Root>
);

export default Collapsible;
