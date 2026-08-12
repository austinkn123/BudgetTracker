import type { ReactNode } from 'react';
import * as RadixDialog from '@radix-ui/react-dialog';
import { cn } from '../../utils/cn';

export type ModalMaxWidth = 'xs' | 'sm' | 'md' | 'lg';

/** MUI Dialog maxWidth equivalents. */
const MAX_WIDTH_CLASSES: Record<ModalMaxWidth, string> = {
  xs: 'max-w-[444px]',
  sm: 'max-w-[600px]',
  md: 'max-w-[900px]',
  lg: 'max-w-[1200px]',
};

export interface ModalProps {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  maxWidth?: ModalMaxWidth;
  fullWidth?: boolean;
  /** Blocks backdrop/Escape dismissal, e.g. while a save is in flight. */
  disableBackdropClose?: boolean;
  contentClassName?: string;
  children?: ReactNode;
}

/** Dialog shell (BUD-20) on Radix — focus trap, ARIA, and Escape for free. */
const Modal = ({
  open,
  onClose,
  title,
  description,
  actions,
  maxWidth = 'sm',
  fullWidth = true,
  disableBackdropClose = false,
  contentClassName,
  children,
}: ModalProps) => (
  <RadixDialog.Root open={open} onOpenChange={(next) => !next && onClose()}>
    <RadixDialog.Portal>
      <RadixDialog.Overlay className="fixed inset-0 z-40 bg-ink/40 animate-fade-in" />
      <RadixDialog.Content
        onPointerDownOutside={(event) => disableBackdropClose && event.preventDefault()}
        onEscapeKeyDown={(event) => disableBackdropClose && event.preventDefault()}
        className={cn(
          'fixed left-1/2 top-1/2 z-50 flex max-h-[calc(100vh-4rem)] -translate-x-1/2 -translate-y-1/2 flex-col rounded-md border border-border bg-surface shadow-lg animate-zoom-in',
          fullWidth && 'w-[calc(100vw-2rem)]',
          MAX_WIDTH_CLASSES[maxWidth],
        )}
      >
        <RadixDialog.Title className="px-6 pb-1 pt-6 text-lg font-semibold tracking-tight text-ink">
          {title}
        </RadixDialog.Title>

        <div className={cn('flex flex-col gap-4 overflow-y-auto px-6 pb-2 pt-3', contentClassName)}>
          {description && (
            <RadixDialog.Description className="text-sm text-ink-muted">
              {description}
            </RadixDialog.Description>
          )}
          {children}
        </div>

        {actions && <div className="flex items-center gap-2 px-6 pb-6 pt-3">{actions}</div>}
      </RadixDialog.Content>
    </RadixDialog.Portal>
  </RadixDialog.Root>
);

export default Modal;
