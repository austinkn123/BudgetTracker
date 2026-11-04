import type { ReactNode } from 'react';
import { TrendingDown, TrendingUp } from 'lucide-react';

/** Figure pieces shared by the Holdings and Trades tabs, so the two read as one page. */

/** Shown for any figure Webull didn't supply. Never $0: that would claim a value nobody reported. */
export const MISSING = '—';

export const overlineClass = 'text-2xs font-semibold uppercase tracking-[0.08em] text-ink-muted';

/** Name the meaning, not Webull's code. Anything else shows as Webull sent it. */
const INSTRUMENT_LABELS: Record<string, string> = { EQUITY: 'Stock', CRYPTO: 'Crypto' };

/** A table's symbol cell: the ticker, with what kind of instrument it is beneath. */
export const SymbolCell = ({ symbol, instrumentType }: { symbol: string; instrumentType: string }) => (
  <>
    <span className="block font-semibold">{symbol}</span>
    <span className="block text-xs text-ink-muted">
      {INSTRUMENT_LABELS[instrumentType] ?? instrumentType}
    </span>
  </>
);

/**
 * Direction as an arrow plus the word, never hue: green is reserved for income and red for
 * over-plan (design.md §3), and a gain or loss is neither. Shape scans as fast and
 * survives colour blindness; the word carries it for screen readers.
 */
export const Direction = ({ value }: { value: number | null }) => {
  if (!value) return null;
  const up = value > 0;
  const Arrow = up ? TrendingUp : TrendingDown;
  return (
    <span
      data-trend={up ? 'up' : 'down'}
      className="ml-1.5 inline-flex items-center gap-1 rounded-full bg-border-subtle px-1.5 py-px align-middle text-2xs font-semibold uppercase tracking-[0.06em] text-ink-muted"
    >
      <Arrow size={12} className="text-ink" aria-hidden />
      {up ? 'Gain' : 'Loss'}
    </span>
  );
};

interface StatProps {
  label: string;
  figure: string;
  /** Small qualifier under the figure. */
  detail?: ReactNode;
}

/**
 * One cell of a summary band. Same look as the Dashboard's StatStrip, but not the component:
 * StatStrip counts up from $0 and needs a number, while these are nullable snapshots where a
 * passing "$0" would read as a real balance.
 */
export const Stat = ({ label, figure, detail }: StatProps) => (
  <div className="bg-background px-5 py-4">
    <dt className={overlineClass}>{label}</dt>
    <dd className="numeric mt-1.5 text-[26px] font-semibold leading-none tracking-[-0.03em] text-ink">
      {figure}
    </dd>
    {detail && <dd className="mt-1.5 text-xs text-ink-muted">{detail}</dd>}
  </div>
);
