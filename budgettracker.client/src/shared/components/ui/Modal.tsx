import type { ReactNode } from 'react';
import * as RadixDialog from '@radix-ui/react-dialog';
import { cn } from '../../utils/cn';

export type ModalMaxWidth = 'xs' | 'sm' | 'md' | 'lg';

/** MUI Dialog maxWidth equivalents, never wider than the screen less a 1rem gutter each side (BUD-29). */
const MAX_WIDTH_CLASSES: Record<ModalMaxWidth, string> = {
  xs: 'max-w-[min(444px,calc(100vw_-_2rem))]',
  sm: 'max-w-[min(600px,calc(100vw_-_2rem))]',
  md: 'max-w-[min(900px,calc(100vw_-_2rem))]',
  lg: 'max-w-[min(1200px,calc(100vw_-_2rem))]',
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
      <RadixDialog.Overlay className="fixed inset-0 z-40 bg-grey-900/50 backdrop-blur-[2px] animate-fade-in data-[state=closed]:animate-fade-out" />
      <RadixDialog.Content
        onPointerDownOutside={(event) => disableBackdropClose && event.preventDefault()}
        onEscapeKeyDown={(event) => disableBackdropClose && event.preventDefault()}
        className={cn(
          'fixed left-1/2 top-1/2 z-50 flex max-h-[calc(100vh-4rem)] -translate-x-1/2 -translate-y-1/2 flex-col overflow-hidden rounded-xl bg-surface shadow-lg ring-1 ring-ink/10 animate-zoom-in data-[state=closed]:animate-zoom-out',
          fullWidth && 'w-[calc(100vw-2rem)]',
          MAX_WIDTH_CLASSES[maxWidth],
        )}
      >
        <RadixDialog.Title className="border-b border-border-subtle px-6 py-4 text-[15px] font-semibold tracking-[-0.01em] text-ink">
          {title}
        </RadixDialog.Title>

        <div className={cn('flex flex-col gap-5 overflow-y-auto px-6 py-5', contentClassName)}>
          {description && (
            <RadixDialog.Description className="text-sm leading-relaxed text-ink-muted">
              {description}
            </RadixDialog.Description>
          )}
          {children}
        </div>

        {actions && (
          <div className="flex flex-wrap items-center justify-end gap-2 border-t border-border-subtle bg-background/60 px-6 py-4">
            {actions}
          </div>
        )}
      </RadixDialog.Content>
    </RadixDialog.Portal>
  </RadixDialog.Root>
);

export default Modal;
