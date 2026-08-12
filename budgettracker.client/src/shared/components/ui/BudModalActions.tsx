import BudButton from './BudButton';
import type { BudButtonVariant } from './BudButton';

export interface BudModalActionsProps {
  onCancel: () => void;
  cancelLabel?: string;
  onConfirm?: () => void;
  confirmLabel: string;
  confirmVariant?: BudButtonVariant;
  /** Submits the enclosing <form> instead of firing onConfirm. */
  confirmType?: 'button' | 'submit';
  /** Optional destructive action, pinned to the left of the row. */
  onDelete?: () => void;
  deleteLabel?: string;
  isPending?: boolean;
}

/**
 * The Cancel / Delete / Confirm triad shared by every dialog (BUD-16).
 * This is the only genuinely common part of the app's dialogs — the forms
 * themselves diverge too much to justify a generic form-modal.
 */
const BudModalActions = ({
  onCancel,
  cancelLabel = 'Cancel',
  onConfirm,
  confirmLabel,
  confirmVariant = 'primary',
  confirmType = 'button',
  onDelete,
  deleteLabel = 'Delete',
  isPending = false,
}: BudModalActionsProps) => (
  <>
    {onDelete && (
      <BudButton
        variant="destructive-ghost"
        onClick={onDelete}
        disabled={isPending}
        sx={{ mr: 'auto' }}
      >
        {deleteLabel}
      </BudButton>
    )}
    <BudButton variant="ghost" onClick={onCancel} disabled={isPending}>
      {cancelLabel}
    </BudButton>
    <BudButton
      variant={confirmVariant}
      type={confirmType}
      onClick={onConfirm}
      loading={isPending}
    >
      {confirmLabel}
    </BudButton>
  </>
);

export default BudModalActions;
