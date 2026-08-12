import type { ReactNode } from 'react';
import { cn } from '../../utils/cn';

export interface FieldShellProps {
  label: string;
  htmlFor?: string;
  required?: boolean;
  /** Validation message; wins over helperText. */
  error?: string;
  helperText?: string;
  fullWidth?: boolean;
  className?: string;
  children: ReactNode;
}

/**
 * Shared label + helper/error shell for Input and Select (BUD-20).
 * Static label above the field — the Stripe pattern — replacing MUI's
 * floating outlined label.
 */
const FieldShell = ({
  label,
  htmlFor,
  required = false,
  error,
  helperText,
  fullWidth = true,
  className,
  children,
}: FieldShellProps) => (
  <div className={cn('flex flex-col gap-1.5', fullWidth && 'w-full', className)}>
    <label htmlFor={htmlFor} className="text-[13px] font-semibold text-ink">
      {label}
      {required && (
        <span aria-hidden className="ml-0.5 text-error">
          *
        </span>
      )}
    </label>
    {children}
    {(error || helperText) && (
      <p className={cn('text-xs', error ? 'text-error' : 'text-ink-muted')}>{error ?? helperText}</p>
    )}
  </div>
);

export default FieldShell;
