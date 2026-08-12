import BudConfirmModal from '../../../shared/components/ui/BudConfirmModal';

interface ReplaceConnectionDialogProps {
  open: boolean;
  currentInstitutionName: string | undefined;
  onCancel: () => void;
  onConfirm: () => void;
}

/**
 * Confirmation dialog for AC-10: replacing an existing bank connection.
 * Surfaces the current institution name so the user knows what's being replaced.
 */
const ReplaceConnectionDialog = ({
  open,
  currentInstitutionName,
  onCancel,
  onConfirm,
}: ReplaceConnectionDialogProps) => (
  <BudConfirmModal
    open={open}
    title="Replace your current connection?"
    message={
      <>
        You're already connected to <strong>{currentInstitutionName ?? 'a bank'}</strong>.
        Connecting a new bank will disconnect the current one. Imported transactions stay in your
        history, but no new transactions will sync from {currentInstitutionName ?? 'it'} afterwards.
      </>
    }
    confirmLabel="Replace connection"
    onCancel={onCancel}
    onConfirm={onConfirm}
  />
);

export default ReplaceConnectionDialog;
