import type { ReactNode } from 'react';
import { AlertTriangle, CheckCircle2, Info, X, XCircle } from 'lucide-react';
import { cn } from '../../utils/cn';

export type AlertSeverity = 'success' | 'warning' | 'error' | 'info';

const SEVERITY_STYLES: Record<
  AlertSeverity,
  { standard: string; outlined: string; icon: ReactNode; iconColor: string }
> = {
  success: {
    standard: 'bg-success-subtle border-success-light text-success-dark',
    outlined: 'bg-transparent border-success text-success-dark',
    icon: <CheckCircle2 size={18} />,
    iconColor: 'text-success',
  },
  warning: {
    standard: 'bg-warning-subtle border-warning-light text-warning-dark',
    outlined: 'bg-transparent border-warning text-warning-dark',
    icon: <AlertTriangle size={18} />,
    iconColor: 'text-warning',
  },
  error: {
    standard: 'bg-error-subtle border-error-light text-error-dark',
    outlined: 'bg-transparent border-error text-error-dark',
    icon: <XCircle size={18} />,
    iconColor: 'text-error',
  },
  info: {
    standard: 'bg-info-subtle border-info-light text-info-dark',
    outlined: 'bg-transparent border-info text-info-dark',
    icon: <Info size={18} />,
    iconColor: 'text-info',
  },
};

export interface AlertProps {
  severity: AlertSeverity;
  message?: ReactNode;
  title?: string;
  onClose?: () => void;
  variant?: 'standard' | 'outlined';
  className?: string;
  children?: ReactNode;
}

/** Inline feedback banner (BUD-20). */
const Alert = ({
  severity,
  message,
  title,
  onClose,
  variant = 'standard',
  className,
  children,
}: AlertProps) => {
  const styles = SEVERITY_STYLES[severity];

  return (
    <div
      role="alert"
      className={cn(
        'flex items-start gap-3 rounded-md border px-4 py-3 text-sm',
        variant === 'standard' ? styles.standard : styles.outlined,
        className,
      )}
    >
      <span className={cn('mt-0.5 shrink-0', styles.iconColor)}>{styles.icon}</span>
      <div className="min-w-0 flex-1">
        {title && <p className="font-semibold">{title}</p>}
        {message}
        {children}
      </div>
      {onClose && (
        <button
          type="button"
          aria-label="Dismiss"
          onClick={onClose}
          className="focus-ring -mr-1 shrink-0 rounded p-0.5 opacity-70 transition-opacity duration-120 hover:opacity-100"
        >
          <X size={16} />
        </button>
      )}
    </div>
  );
};

export default Alert;
