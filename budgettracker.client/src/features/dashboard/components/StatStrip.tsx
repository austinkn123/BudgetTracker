import type { ReactNode } from 'react';
import { cn } from '../../../shared/utils/cn';

export interface Stat {
  label: string;
  value: string;
  /** Small qualifier under the figure. */
  detail?: ReactNode;
  tone?: 'default' | 'positive' | 'negative';
}

export interface StatStripProps {
  stats: Stat[];
}

const TONE: Record<NonNullable<Stat['tone']>, string> = {
  default: 'text-ink',
  positive: 'text-success-dark',
  negative: 'text-error',
};

/**
 * Headline figures as a borderless divided band (BUD-20).
 *
 * Deliberately not cards: the page already has plenty of boxed surfaces, and
 * the top-line numbers read faster when nothing frames them.
 */
const StatStrip = ({ stats }: StatStripProps) => (
  <dl className="grid grid-cols-2 gap-px overflow-hidden rounded-xl bg-border-subtle lg:grid-cols-4">
    {stats.map((stat) => (
      <div key={stat.label} className="bg-background px-5 py-4">
        <dt className="text-2xs font-semibold uppercase tracking-[0.08em] text-ink-muted">
          {stat.label}
        </dt>
        <dd
          className={cn(
            'mt-1.5 text-[26px] font-semibold leading-none tracking-[-0.03em] tabular-nums',
            TONE[stat.tone ?? 'default'],
          )}
        >
          {stat.value}
        </dd>
        {stat.detail && <p className="mt-1.5 text-xs text-ink-muted">{stat.detail}</p>}
      </div>
    ))}
  </dl>
);

export default StatStrip;
