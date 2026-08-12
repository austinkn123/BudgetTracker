import type { ReactNode } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import type { SxProps, Theme } from '@mui/material/styles';

export type BudModalMaxWidth = 'xs' | 'sm' | 'md' | 'lg';

export interface BudModalProps {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  maxWidth?: BudModalMaxWidth;
  fullWidth?: boolean;
  /** Blocks backdrop/Escape dismissal, e.g. while a save is in flight. */
  disableBackdropClose?: boolean;
  contentSx?: SxProps<Theme>;
  children?: ReactNode;
}

/**
 * Dialog shell (BUD-16).
 *
 * Note: dialog content is portaled outside #root, where Tailwind's
 * `important: '#root'` scoping does not reach — all spacing here is `sx`.
 */
const BudModal = ({
  open,
  onClose,
  title,
  description,
  actions,
  maxWidth = 'sm',
  fullWidth = true,
  disableBackdropClose = false,
  contentSx,
  children,
}: BudModalProps) => (
  <Dialog
    open={open}
    onClose={(_event, reason) => {
      if (disableBackdropClose && (reason === 'backdropClick' || reason === 'escapeKeyDown')) return;
      onClose();
    }}
    maxWidth={maxWidth}
    fullWidth={fullWidth}
    slotProps={{ paper: { sx: { borderRadius: 3 } } }}
  >
    <DialogTitle sx={{ px: 3, pt: 3, pb: 1, fontWeight: 600 }}>{title}</DialogTitle>

    {/*
      The `&&` doubles specificity on purpose: MUI ships
      `.MuiDialogTitle-root + .MuiDialogContent-root { padding-top: 0 }`, which
      otherwise wins over the sx class and clips the outlined label that sits
      ~9px above its field inside this overflow container.
    */}
    <DialogContent
      sx={{
        '&&': { pt: 3 },
        px: 3,
        display: 'flex',
        flexDirection: 'column',
        gap: 2,
        ...contentSx,
      }}
    >
      {description && <DialogContentText sx={{ m: 0 }}>{description}</DialogContentText>}
      {children}
    </DialogContent>

    {actions && <DialogActions sx={{ px: 3, pb: 3, pt: 1, gap: 1 }}>{actions}</DialogActions>}
  </Dialog>
);

export default BudModal;
