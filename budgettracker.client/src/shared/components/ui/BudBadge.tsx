import type { ReactElement, ReactNode } from 'react';
import Chip from '@mui/material/Chip';
import type { SxProps, Theme } from '@mui/material/styles';

export type BudBadgeColor = 'success' | 'warning' | 'error' | 'info' | 'primary' | 'neutral';
export type BudBadgeVariant = 'solid' | 'soft' | 'outline';

export interface BudBadgeProps {
  label: ReactNode;
  color?: BudBadgeColor;
  variant?: BudBadgeVariant;
  size?: 'sm' | 'md';
  icon?: ReactElement;
  onClick?: () => void;
  onDelete?: () => void;
  title?: string;
  sx?: SxProps<Theme>;
}

interface BadgeSurface {
  bgcolor: string;
  color: string;
  borderColor: string;
}

/** Resolves palette paths; `neutral` maps onto the grey ramp. */
const surfaceFor = (color: BudBadgeColor, variant: BudBadgeVariant): BadgeSurface => {
  if (color === 'neutral') {
    return variant === 'solid'
      ? { bgcolor: 'grey.700', color: 'common.white', borderColor: 'grey.700' }
      : variant === 'outline'
        ? { bgcolor: 'transparent', color: 'text.secondary', borderColor: 'divider' }
        : { bgcolor: 'grey.100', color: 'text.secondary', borderColor: 'grey.100' };
  }

  return variant === 'solid'
    ? { bgcolor: `${color}.main`, color: `${color}.contrastText`, borderColor: `${color}.main` }
    : variant === 'outline'
      ? { bgcolor: 'transparent', color: `${color}.dark`, borderColor: `${color}.main` }
      : { bgcolor: `${color}.subtle`, color: `${color}.dark`, borderColor: `${color}.subtle` };
};

/**
 * Status pill (BUD-16). Defaults to the `soft` variant so the background is the
 * semantic `subtle` token.
 */
const BudBadge = ({
  label,
  color = 'neutral',
  variant = 'soft',
  size = 'sm',
  icon,
  onClick,
  onDelete,
  title,
  sx,
}: BudBadgeProps) => (
  <Chip
    label={label}
    icon={icon}
    onClick={onClick}
    onDelete={onDelete}
    title={title}
    size={size === 'sm' ? 'small' : 'medium'}
    variant={variant === 'outline' ? 'outlined' : 'filled'}
    sx={{
      fontWeight: 600,
      borderWidth: 1,
      borderStyle: 'solid',
      ...surfaceFor(color, variant),
      '& .MuiChip-deleteIcon': { color: 'inherit', opacity: 0.7, '&:hover': { opacity: 1 } },
      '& .MuiChip-icon': { color: 'inherit' },
      ...sx,
    }}
  />
);

export default BudBadge;
