import type { ReactNode } from 'react';
import { useCountUp } from '../../../shared/hooks/useCountUp';
import { cn } from '../../../shared/utils/cn';

export interface Stat {
  label: string;
  /** Raw figure; it counts up on load and is rendered through `format`. */
  value: number;
  format: (value: number) => string;
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

const StatFigure = ({ stat }: { stat: Stat }) => {
  const shown = useCountUp(stat.value);
  return (
    <dd
      className={cn(
        'numeric mt-1.5 text-[26px] font-semibold leading-none tracking-[-0.03em]',
        TONE[stat.tone ?? 'default'],
      )}
    >
      {/* Screen readers get the settled figure, not every frame. */}
      <span aria-hidden>{stat.format(shown)}</span>
      <span className="sr-only">{stat.format(stat.value)}</span>
    </dd>
  );
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
        <StatFigure stat={stat} />
        {stat.detail && <p className="mt-1.5 text-xs text-ink-muted">{stat.detail}</p>}
      </div>
    ))}
  </dl>
);

export default StatStrip;
