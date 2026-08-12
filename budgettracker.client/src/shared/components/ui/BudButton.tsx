import type { ElementType, ReactNode } from 'react';
import Button from '@mui/material/Button';
import type { ButtonProps } from '@mui/material/Button';

export type BudButtonVariant =
  | 'primary'
  | 'secondary'
  | 'ghost'
  | 'destructive'
  | 'destructive-ghost';

export type BudButtonSize = 'sm' | 'md' | 'lg';

/** Maps a Bud variant onto MUI's variant + color pair. */
const VARIANT_MAP: Record<BudButtonVariant, Pick<ButtonProps, 'variant' | 'color'>> = {
  primary: { variant: 'contained', color: 'primary' },
  secondary: { variant: 'outlined', color: 'primary' },
  ghost: { variant: 'text', color: 'inherit' },
  destructive: { variant: 'contained', color: 'error' },
  'destructive-ghost': { variant: 'text', color: 'error' },
};

const SIZE_MAP: Record<BudButtonSize, ButtonProps['size']> = {
  sm: 'small',
  md: 'medium',
  lg: 'large',
};

type ForwardedProps = Omit<
  ButtonProps,
  'variant' | 'color' | 'size' | 'startIcon' | 'endIcon' | 'children' | 'className'
>;

export interface BudButtonProps extends ForwardedProps {
  variant?: BudButtonVariant;
  size?: BudButtonSize;
  /** Shows a spinner and disables the button (MUI v7 native loading). */
  loading?: boolean;
  startIcon?: ReactNode;
  endIcon?: ReactNode;
  /** Render as another element, e.g. `component={RouterLink} to="/budget-plans"`. */
  component?: ElementType;
  to?: string;
  children: ReactNode;
}

/**
 * The single button primitive (BUD-16). Feature code must not import MUI Button
 * directly — an ESLint rule enforces this.
 */
const BudButton = ({
  variant = 'primary',
  size = 'md',
  loading = false,
  children,
  sx,
  ...rest
}: BudButtonProps) => {
  const { variant: muiVariant, color } = VARIANT_MAP[variant];

  return (
    <Button
      variant={muiVariant}
      color={color}
      size={SIZE_MAP[size]}
      loading={loading}
      sx={{
        borderRadius: 2,
        ...(variant === 'ghost' && { color: 'text.secondary' }),
        ...sx,
      }}
      {...rest}
    >
      {children}
    </Button>
  );
};

export default BudButton;
