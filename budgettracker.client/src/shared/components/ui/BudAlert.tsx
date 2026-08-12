import type { ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import AlertTitle from '@mui/material/AlertTitle';
import type { SxProps, Theme } from '@mui/material/styles';

export type BudAlertSeverity = 'success' | 'warning' | 'error' | 'info';

export interface BudAlertProps {
  severity: BudAlertSeverity;
  message?: ReactNode;
  title?: string;
  onClose?: () => void;
  variant?: 'standard' | 'outlined';
  sx?: SxProps<Theme>;
  children?: ReactNode;
}

/**
 * Inline feedback banner (BUD-16). Replaces both the MUI Alert usages and the
 * hand-rolled Tailwind status banners.
 */
const BudAlert = ({
  severity,
  message,
  title,
  onClose,
  variant = 'standard',
  sx,
  children,
}: BudAlertProps) => (
  <Alert
    severity={severity}
    variant={variant}
    onClose={onClose}
    sx={{
      borderRadius: 2,
      ...(variant === 'standard' && {
        bgcolor: `${severity}.subtle`,
        color: `${severity}.dark`,
        border: 1,
        borderColor: `${severity}.light`,
        '& .MuiAlert-icon': { color: `${severity}.main` },
      }),
      ...sx,
    }}
  >
    {title && <AlertTitle>{title}</AlertTitle>}
    {message}
    {children}
  </Alert>
);

export default BudAlert;
