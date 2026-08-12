import type { ReactNode } from 'react';
import { cn } from '../../utils/cn';

export type CardPadding = 'none' | 'sm' | 'md' | 'lg';
export type CardVariant = 'outlined' | 'elevated' | 'plain';

/**
 * Padding is a named token: MUI's spacing was 8px-based and Tailwind's is
 * 4px-based, so a numeric prop would be ambiguous at the API edge.
 */
const PADDING_CLASSES: Record<CardPadding, { header: string; content: string; footer: string }> = {
  none: { header: '', content: '', footer: '' },
  sm: { header: 'px-4 pt-4', content: 'p-4', footer: 'px-4 pb-4' },
  md: { header: 'px-6 pt-6', content: 'p-6', footer: 'px-6 pb-6' },
  lg: { header: 'px-8 pt-8', content: 'p-8', footer: 'px-8 pb-8' },
};

export interface CardProps {
  title?: ReactNode;
  subtitle?: ReactNode;
  /** Header top-right slot, typically actions or a badge. */
  actions?: ReactNode;
  footer?: ReactNode;
  padding?: CardPadding;
  variant?: CardVariant;
  fullHeight?: boolean;
  /** When set, the card body becomes a full-width button. */
  onClick?: () => void;
  className?: string;
  headerClassName?: string;
  contentClassName?: string;
  children: ReactNode;
}

/** Surface primitive (BUD-20): 1px border + micro-shadow, 8px radius. */
const Card = ({
  title,
  subtitle,
  actions,
  footer,
  padding = 'md',
  variant = 'outlined',
  fullHeight = false,
  onClick,
  className,
  headerClassName,
  contentClassName,
  children,
}: CardProps) => {
  const pad = PADDING_CLASSES[padding];
  const hasHeader = Boolean(title || subtitle || actions);

  const body = (
    <>
      {hasHeader && (
        <div className={cn('flex items-start justify-between gap-4', pad.header, headerClassName)}>
          <div className="min-w-0">
            {title && <h2 className="text-base font-semibold text-ink">{title}</h2>}
            {subtitle && <p className="mt-1 text-sm text-ink-muted">{subtitle}</p>}
          </div>
          {actions && <div className="shrink-0">{actions}</div>}
        </div>
      )}

      <div className={cn(pad.content, hasHeader && padding !== 'none' && 'pt-4', contentClassName)}>
        {children}
      </div>

      {footer && <div className={cn(pad.footer)}>{footer}</div>}
    </>
  );

  return (
    <div
      className={cn(
        'rounded-md',
        variant === 'outlined' && 'border border-border bg-surface shadow-sm',
        variant === 'elevated' && 'border border-border bg-surface shadow-md',
        variant === 'plain' && 'bg-transparent',
        fullHeight && 'h-full',
        className,
      )}
    >
      {onClick ? (
        <button type="button" onClick={onClick} className="focus-ring block w-full rounded-md text-left">
          {body}
        </button>
      ) : (
        body
      )}
    </div>
  );
};

export default Card;
