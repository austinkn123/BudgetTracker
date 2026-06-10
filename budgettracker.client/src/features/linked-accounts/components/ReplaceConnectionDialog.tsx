import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import Button from '@mui/material/Button';

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
export const ReplaceConnectionDialog = ({
  open,
  currentInstitutionName,
  onCancel,
  onConfirm,
}: ReplaceConnectionDialogProps) => (
  <Dialog open={open} onClose={onCancel}>
    <DialogTitle>Replace your current connection?</DialogTitle>
    <DialogContent>
      <DialogContentText>
        You're already connected to <strong>{currentInstitutionName ?? 'a bank'}</strong>.
        Connecting a new bank will disconnect the current one. Imported transactions stay in
        your history, but no new transactions will sync from {currentInstitutionName ?? 'it'}
        {' '}afterwards.
      </DialogContentText>
    </DialogContent>
    <DialogActions>
      <Button onClick={onCancel}>Cancel</Button>
      <Button onClick={onConfirm} color="primary" variant="contained">
        Replace connection
      </Button>
    </DialogActions>
  </Dialog>
);
