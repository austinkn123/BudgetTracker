import type { CSSProperties, ReactNode } from 'react';
import { X } from 'lucide-react';
import { cn } from '../../utils/cn';

export type BadgeColor = 'success' | 'warning' | 'error' | 'info' | 'primary' | 'neutral';
export type BadgeVariant = 'solid' | 'soft' | 'outline';

const SURFACE_CLASSES: Record<BadgeColor, Record<BadgeVariant, string>> = {
  neutral: {
    solid: 'bg-ink text-background border-transparent',
    soft: 'bg-border-subtle text-ink-muted border-transparent',
    outline: 'bg-transparent text-ink-muted border-border',
  },
  primary: {
    solid: 'bg-primary text-white border-transparent',
    soft: 'bg-primary-subtle text-primary-dark border-transparent',
    outline: 'bg-transparent text-primary-dark border-primary',
  },
  success: {
    solid: 'bg-success text-white border-transparent',
    soft: 'bg-success-subtle text-success-dark border-transparent',
    outline: 'bg-transparent text-success-dark border-success',
  },
  warning: {
    solid: 'bg-warning text-warning-subtle border-transparent',
    soft: 'bg-warning-subtle text-warning-dark border-transparent',
    outline: 'bg-transparent text-warning-dark border-warning',
  },
  error: {
    solid: 'bg-error text-white border-transparent',
    soft: 'bg-error-subtle text-error-dark border-transparent',
    outline: 'bg-transparent text-error-dark border-error',
  },
  info: {
    solid: 'bg-info text-white border-transparent',
    soft: 'bg-info-subtle text-info-dark border-transparent',
    outline: 'bg-transparent text-info-dark border-info',
  },
};

export interface BadgeProps {
  label: ReactNode;
  color?: BadgeColor;
  variant?: BadgeVariant;
  size?: 'sm' | 'md';
  icon?: ReactNode;
  onClick?: () => void;
  onDelete?: () => void;
  title?: string;
  className?: string;
  /** For token-derived runtime colors (e.g. per-category chart tints). */
  style?: CSSProperties;
}

/**
 * Status pill (BUD-20). Defaults to `soft` so the background is the semantic
 * `subtle` token.
 */
const Badge = ({
  label,
  color = 'neutral',
  variant = 'soft',
  size = 'sm',
  icon,
  onClick,
  onDelete,
  title,
  className,
  style,
}: BadgeProps) => {
  const Root = onClick ? 'button' : 'span';

  return (
    <Root
      type={onClick ? 'button' : undefined}
      onClick={onClick}
      title={title}
      style={style}
      className={cn(
        'inline-flex max-w-full items-center gap-1 truncate rounded-full border font-semibold',
        size === 'sm' ? 'h-6 px-2.5 text-xs' : 'h-7 px-3 text-sm',
        SURFACE_CLASSES[color][variant],
        onClick && 'focus-ring cursor-pointer transition-colors duration-120 hover:opacity-80',
        className,
      )}
    >
      {icon && <span className="inline-flex shrink-0 [&>svg]:h-3.5 [&>svg]:w-3.5">{icon}</span>}
      <span className="truncate">{label}</span>
      {onDelete && (
        <button
          type="button"
          aria-label="Remove"
          onClick={(event) => {
            event.stopPropagation();
            onDelete();
          }}
          className="focus-ring -mr-1 inline-flex shrink-0 rounded-full p-0.5 opacity-70 transition-opacity duration-120 hover:opacity-100"
        >
          <X size={12} />
        </button>
      )}
    </Root>
  );
};

export default Badge;
