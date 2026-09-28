import { Search, X } from 'lucide-react';
import { Badge, Button } from '../../../shared/components/ui';
import { cn } from '../../../shared/utils/cn';

import type { TransactionStatusFilter } from '../utils/transactionFilters';

type TransactionFiltersProps = {
  status: TransactionStatusFilter;
  onStatusChange: (status: TransactionStatusFilter) => void;
  search: string;
  onSearchChange: (search: string) => void;
  /** Counts for the chips that represent work to do; omitted when zero. */
  uncategorizedCount: number;
};

const CHIPS: { value: TransactionStatusFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'uncategorized', label: 'Uncategorized' },
  { value: 'pending', label: 'Pending' },
];

/**
 * Status chips + free-text search for the ledger. Before this the page could only be
 * navigated one calendar day at a time, which does not scale to a few hundred imported rows.
 */
const TransactionFilters = ({
  status,
  onStatusChange,
  search,
  onSearchChange,
  uncategorizedCount,
}: TransactionFiltersProps) => {
  const countFor = (value: TransactionStatusFilter) =>
    value === 'uncategorized' ? uncategorizedCount : 0;

  return (
    <div className="flex flex-col gap-3">
      <div className="relative">
        <Search
          size={16}
          className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-muted"
        />
        <input
          type="search"
          value={search}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Search payee or notes…"
          aria-label="Search transactions"
          className="focus-ring h-10 w-full rounded-lg border border-border bg-surface pl-9 pr-9 text-[14px] text-ink shadow-xs transition-colors duration-120 hover:border-border-strong placeholder:text-ink-muted/60"
        />
        {search && (
          <button
            type="button"
            onClick={() => onSearchChange('')}
            aria-label="Clear search"
            className="focus-ring absolute right-2 top-1/2 -translate-y-1/2 rounded p-1 text-ink-muted hover:text-ink"
          >
            <X size={14} />
          </button>
        )}
      </div>

      <div className="flex flex-wrap gap-2">
        {CHIPS.map((chip) => {
          const count = countFor(chip.value);
          // Hide the work-queue chips when there is no work; keep the navigational ones always.
          if (count === 0 && chip.value === 'uncategorized') {
            if (status !== chip.value) return null;
          }

          const active = status === chip.value;
          return (
            <Button
              key={chip.value}
              variant={active ? 'primary' : 'secondary'}
              size="sm"
              onClick={() => onStatusChange(chip.value)}
              className={cn(active && 'shadow-xs')}
            >
              <span className="flex items-center gap-1.5">
                {chip.label}
                {count > 0 && (
                  <Badge
                    label={String(count)}
                    color="primary"
                    variant={active ? 'solid' : 'soft'}
                  />
                )}
              </span>
            </Button>
          );
        })}
      </div>
    </div>
  );
};

export default TransactionFilters;
