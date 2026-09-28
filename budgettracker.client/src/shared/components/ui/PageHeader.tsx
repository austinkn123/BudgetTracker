import type { ReactNode } from 'react';
import { cn } from '../../utils/cn';

export interface PageHeaderProps {
  title: string;
  description?: ReactNode;
  /** Trailing controls — range selectors, primary actions. */
  actions?: ReactNode;
  className?: string;
}

/**
 * Page title block (BUD-20). Large tight display type with generous space
 * below, so every route opens with a clear anchor instead of a small bold line.
 */
const PageHeader = ({ title, description, actions, className }: PageHeaderProps) => (
  <div
    className={cn(
      'flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between',
      className,
    )}
  >
    <div className="min-w-0">
      <h1 className="text-[30px] font-semibold leading-tight tracking-[-0.025em] text-ink">
        {title}
      </h1>
      {description && <p className="mt-1.5 text-body text-ink-muted">{description}</p>}
    </div>
    {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
  </div>
);

export default PageHeader;
