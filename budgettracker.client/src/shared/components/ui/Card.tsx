import type { ReactNode } from 'react';
import { cn } from '../../utils/cn';

export type CardPadding = 'none' | 'sm' | 'md' | 'lg';
export type CardVariant = 'outlined' | 'elevated' | 'plain';

/**
 * Padding is a named token: a numeric prop would be ambiguous between
 * Tailwind's 4px scale and the 8px design grid.
 */
const PADDING: Record<CardPadding, { x: string; y: string }> = {
  none: { x: '', y: '' },
  sm: { x: 'px-4', y: 'py-4' },
  md: { x: 'px-6', y: 'py-5' },
  lg: { x: 'px-8', y: 'py-7' },
};

export interface CardProps {
  title?: ReactNode;
  subtitle?: ReactNode;
  /** Header trailing slot — actions, badges, legends. */
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

/**
 * Surface primitive (BUD-20).
 *
 * Hairline ring rather than a 1px border, a soft wide shadow for lift, and a
 * separated header rule — the header reads as a distinct band instead of text
 * floating above content.
 */
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
  const pad = PADDING[padding];
  const hasHeader = Boolean(title || subtitle || actions);

  const body = (
    <>
      {hasHeader && (
        <div
          className={cn(
            'flex items-center justify-between gap-4 border-b border-border-subtle',
            pad.x,
            padding === 'none' ? '' : 'py-4',
            headerClassName,
          )}
        >
          <div className="min-w-0">
            {title && (
              <h2 className="truncate text-[15px] font-semibold tracking-[-0.01em] text-ink">
                {title}
              </h2>
            )}
            {subtitle && <p className="mt-0.5 truncate text-[13px] text-ink-muted">{subtitle}</p>}
          </div>
          {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
        </div>
      )}

      <div className={cn(pad.x, pad.y, contentClassName)}>{children}</div>

      {footer && (
        <div className={cn('border-t border-border-subtle', pad.x, 'py-4')}>{footer}</div>
      )}
    </>
  );

  return (
    <div
      className={cn(
        'rounded-xl',
        variant === 'outlined' && 'bg-surface shadow-sm ring-1 ring-ink/[0.06]',
        variant === 'elevated' && 'bg-surface shadow-md ring-1 ring-ink/[0.04]',
        variant === 'plain' && 'bg-transparent',
        fullHeight && 'h-full',
        onClick && 'transition-shadow duration-160 ease-out-soft hover:shadow-md',
        className,
      )}
    >
      {onClick ? (
        <button type="button" onClick={onClick} className="focus-ring block w-full rounded-xl text-left">
          {body}
        </button>
      ) : (
        body
      )}
    </div>
  );
};

export default Card;
