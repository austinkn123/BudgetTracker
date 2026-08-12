import type { ReactNode } from 'react';
import BudModal from './BudModal';
import BudModalActions from './BudModalActions';

export interface BudConfirmModalProps {
  open: boolean;
  title: ReactNode;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  isPending?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

/** Confirm-only dialog (BUD-16) — replaces the ad-hoc confirm dialogs. */
const BudConfirmModal = ({
  open,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  destructive = false,
  isPending = false,
  onCancel,
  onConfirm,
}: BudConfirmModalProps) => (
  <BudModal
    open={open}
    onClose={onCancel}
    title={title}
    description={message}
    maxWidth="xs"
    disableBackdropClose={isPending}
    actions={
      <BudModalActions
        onCancel={onCancel}
        cancelLabel={cancelLabel}
        onConfirm={onConfirm}
        confirmLabel={confirmLabel}
        confirmVariant={destructive ? 'destructive' : 'primary'}
        isPending={isPending}
      />
    }
  />
);

export default BudConfirmModal;
