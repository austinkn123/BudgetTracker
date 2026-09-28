import type { ButtonHTMLAttributes, ElementType, ReactNode } from 'react';
import { Loader2 } from 'lucide-react';
import { cn } from '../../utils/cn';

export type ButtonVariant =
  | 'primary'
  | 'secondary'
  | 'ghost'
  | 'destructive'
  | 'destructive-ghost';

export type ButtonSize = 'sm' | 'md' | 'lg';

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary:
    'bg-primary text-white border border-primary shadow-xs hover:bg-primary-hover hover:border-primary-hover active:translate-y-px',
  secondary:
    'bg-surface text-ink border border-border shadow-xs hover:border-border-strong hover:bg-background active:translate-y-px',
  ghost: 'bg-transparent text-ink-muted border border-transparent hover:bg-border-subtle hover:text-ink',
  destructive:
    'bg-error text-white border border-error shadow-xs hover:bg-error-hover hover:border-error-hover active:translate-y-px',
  'destructive-ghost': 'bg-transparent text-error border border-transparent hover:bg-error-subtle',
};

const SIZE_CLASSES: Record<ButtonSize, string> = {
  sm: 'h-8 px-3 text-[13px] gap-1.5',
  md: 'h-9 px-3.5 text-[13.5px] gap-1.5',
  lg: 'h-11 px-5 text-[14px] gap-2',
};

type ForwardedProps = Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'className' | 'children'>;

export interface ButtonProps extends ForwardedProps {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Shows a spinner and disables the button. */
  loading?: boolean;
  fullWidth?: boolean;
  startIcon?: ReactNode;
  endIcon?: ReactNode;
  /** Render as another element, e.g. `component={RouterLink} to="/budget-plans"`. */
  component?: ElementType;
  to?: string;
  className?: string;
  children: ReactNode;
}

/**
 * The single button primitive (BUD-20). Feature code must not import a
 * third-party button directly — ESLint enforces this.
 */
const Button = ({
  variant = 'primary',
  size = 'md',
  loading = false,
  fullWidth = false,
  startIcon,
  endIcon,
  component,
  className,
  disabled,
  children,
  ...rest
}: ButtonProps) => {
  const Comp: ElementType = component ?? 'button';

  return (
    <Comp
      className={cn(
        'focus-ring inline-flex select-none items-center justify-center whitespace-nowrap rounded-lg font-semibold tracking-[-0.005em] transition-all duration-120 ease-out-soft',
        'disabled:pointer-events-none disabled:opacity-50',
        VARIANT_CLASSES[variant],
        SIZE_CLASSES[size],
        fullWidth && 'w-full',
        className,
      )}
      disabled={disabled || loading}
      {...rest}
    >
      {loading ? <Loader2 size={16} className="animate-spin" aria-hidden /> : startIcon}
      {children}
      {!loading && endIcon}
    </Comp>
  );
};

export default Button;
