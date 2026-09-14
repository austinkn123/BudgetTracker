import type { ReactNode } from 'react';
import Modal from './Modal';
import ModalActions from './ModalActions';

export interface ConfirmModalProps {
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

/** Confirm-only dialog (BUD-20). */
const ConfirmModal = ({
  open,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  destructive = false,
  isPending = false,
  onCancel,
  onConfirm,
}: ConfirmModalProps) => (
  <Modal
    open={open}
    onClose={onCancel}
    title={title}
    description={message}
    maxWidth="xs"
    disableBackdropClose={isPending}
    actions={
      <ModalActions
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

export default ConfirmModal;
