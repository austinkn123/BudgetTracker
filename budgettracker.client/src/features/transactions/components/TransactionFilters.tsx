import { Badge, Button, SearchInput } from '../../../shared/components/ui';
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
      <SearchInput
        value={search}
        onChange={onSearchChange}
        placeholder="Search payee or notes…"
        ariaLabel="Search transactions"
      />

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
