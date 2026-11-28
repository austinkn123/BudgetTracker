import type { ReactNode } from 'react';
import { RotateCw, XCircle } from 'lucide-react';
import { cn } from '../../utils/cn';
import Button from './Button';

export interface EmptyStateProps {
  /** A short headline with no full stop (design.md §2). */
  title: string;
  /** What it means, and what would fill it. */
  description?: ReactNode;
  icon?: ReactNode;
  /** The next step, usually one Button. */
  action?: ReactNode;
  tone?: 'neutral' | 'error';
  /**
   * `panel` (default) draws its own surface, for a state that stands in for a whole section.
   * `inline` draws none, for a state that sits inside a Card that already has one.
   */
  layout?: 'panel' | 'inline';
  role?: 'alert' | 'status';
  className?: string;
}

/**
 * The one shape for "nothing here yet" and "this didn't load": a headline, a line saying what it
 * means, and the next step. Empty states name the next action rather than ending the road
 * (design.md §2), so `action` is expected whenever there is something the person can do.
 */
const EmptyState = ({
  title,
  description,
  icon,
  action,
  tone = 'neutral',
  layout = 'panel',
  role,
  className,
}: EmptyStateProps) => (
  <div
    role={role}
    className={cn(
      'flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between',
      layout === 'panel' && 'rounded-xl border border-border bg-surface px-5 py-4',
      layout === 'panel' && tone === 'error' && 'border-error-light',
      className,
    )}
  >
    <div className="flex min-w-0 items-start gap-3">
      {icon && (
        <span className={cn('mt-0.5 shrink-0', tone === 'error' ? 'text-error' : 'text-ink-muted')} aria-hidden>
          {icon}
        </span>
      )}
      <div className="min-w-0">
        <p className="text-sm font-semibold text-ink">{title}</p>
        {description && <p className="mt-1 text-sm text-ink-muted">{description}</p>}
      </div>
    </div>
    {action && <div className="shrink-0">{action}</div>}
  </div>
);

export interface ErrorStateProps {
  /** What didn't load, as "Couldn't load …" with no full stop. */
  title: string;
  description?: ReactNode;
  /** Refetch whatever failed. */
  onRetry: () => void;
  /** The retry is in flight: the button spins and can't be pressed twice. */
  isRetrying?: boolean;
  layout?: EmptyStateProps['layout'];
  className?: string;
}

/**
 * A load that failed, with a Try again button. Use it in place of the figures a failed read would
 * otherwise show, so a failure never reads as zero or as an empty account.
 */
export const ErrorState = ({
  title,
  description = "We couldn't reach the server. Try again in a moment.",
  onRetry,
  isRetrying = false,
  layout,
  className,
}: ErrorStateProps) => (
  <EmptyState
    role="alert"
    tone="error"
    layout={layout}
    className={className}
    icon={<XCircle size={18} />}
    title={title}
    description={description}
    action={
      <Button
        variant="secondary"
        size="sm"
        onClick={onRetry}
        loading={isRetrying}
        startIcon={<RotateCw size={14} />}
      >
        Try again
      </Button>
    }
  />
);

export default EmptyState;
