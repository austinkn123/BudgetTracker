import Button from './Button';
import type { ButtonVariant } from './Button';

export interface ModalActionsProps {
  onCancel: () => void;
  cancelLabel?: string;
  onConfirm?: () => void;
  confirmLabel: string;
  confirmVariant?: ButtonVariant;
  /** Submits the enclosing <form> instead of firing onConfirm. */
  confirmType?: 'button' | 'submit';
  /** Optional destructive action, pinned to the left of the row. */
  onDelete?: () => void;
  deleteLabel?: string;
  isPending?: boolean;
}

/** The Cancel / Delete / Confirm triad shared by every dialog (BUD-20). */
const ModalActions = ({
  onCancel,
  cancelLabel = 'Cancel',
  onConfirm,
  confirmLabel,
  confirmVariant = 'primary',
  confirmType = 'button',
  onDelete,
  deleteLabel = 'Delete',
  isPending = false,
}: ModalActionsProps) => (
  <>
    {onDelete && (
      <Button
        variant="destructive-ghost"
        onClick={onDelete}
        disabled={isPending}
        className="mr-auto"
      >
        {deleteLabel}
      </Button>
    )}
    <Button variant="ghost" onClick={onCancel} disabled={isPending} className="ml-auto">
      {cancelLabel}
    </Button>
    <Button variant={confirmVariant} type={confirmType} onClick={onConfirm} loading={isPending}>
      {confirmLabel}
    </Button>
  </>
);

export default ModalActions;
