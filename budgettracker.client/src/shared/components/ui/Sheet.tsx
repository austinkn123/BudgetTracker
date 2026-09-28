import type { ReactNode } from 'react';
import * as RadixDialog from '@radix-ui/react-dialog';
import * as VisuallyHidden from '@radix-ui/react-visually-hidden';
import { cn } from '../../utils/cn';

export interface SheetProps {
  open: boolean;
  onClose: () => void;
  /** Accessible name — visually hidden (Radix Dialog requires a Title). */
  title: string;
  width?: number;
  className?: string;
  children: ReactNode;
}

/**
 * Left slide-over panel (BUD-20). Replaces MUI's temporary Drawer for the
 * mobile nav; Radix Dialog supplies focus trap, Escape, and scroll lock.
 */
const Sheet = ({ open, onClose, title, width = 240, className, children }: SheetProps) => (
  <RadixDialog.Root open={open} onOpenChange={(next) => !next && onClose()}>
    <RadixDialog.Portal>
      <RadixDialog.Overlay className="fixed inset-0 z-40 bg-ink/40 animate-fade-in" />
      <RadixDialog.Content
        className={cn(
          'fixed inset-y-0 left-0 z-50 shadow-lg data-[state=closed]:animate-slide-out-left data-[state=open]:animate-slide-in-left',
          className,
        )}
        style={{ width }}
      >
        <VisuallyHidden.Root>
          <RadixDialog.Title>{title}</RadixDialog.Title>
        </VisuallyHidden.Root>
        {children}
      </RadixDialog.Content>
    </RadixDialog.Portal>
  </RadixDialog.Root>
);

export default Sheet;
