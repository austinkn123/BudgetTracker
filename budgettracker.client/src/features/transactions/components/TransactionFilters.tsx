import { Badge, Button, InlineSelect, SearchInput } from '../../../shared/components/ui';
import type { SelectOption } from '../../../shared/components/ui';
import { cn } from '../../../shared/utils/cn';

import type { TransactionStatusFilter } from '../utils/transactionFilters';

type TransactionFiltersProps = {
  status: TransactionStatusFilter;
  onStatusChange: (status: TransactionStatusFilter) => void;
  search: string;
  onSearchChange: (search: string) => void;
  /** Counts for the chips that represent work to do; omitted when zero. */
  uncategorizedCount: number;
  /** Selected linked account; null means all accounts. */
  account: string | null;
  onAccountChange: (account: string | null) => void;
  /** One per linked account. The selector is only shown when there are two or more. */
  accountOptions: readonly SelectOption<string>[];
};

const CHIPS: { value: TransactionStatusFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'uncategorized', label: 'Uncategorized' },
  { value: 'pending', label: 'Pending' },
];

/**
 * Status chips + free-text search + account filter for the ledger. Before this the page could only be
 * navigated one calendar day at a time, which does not scale to a few hundred imported rows.
 */
const TransactionFilters = ({
  status,
  onStatusChange,
  search,
  onSearchChange,
  uncategorizedCount,
  account,
  onAccountChange,
  accountOptions,
}: TransactionFiltersProps) => {
  const countFor = (value: TransactionStatusFilter) =>
    value === 'uncategorized' ? uncategorizedCount : 0;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-2 sm:flex-row">
        <SearchInput
          value={search}
          onChange={onSearchChange}
          placeholder="Search payee or notes…"
          ariaLabel="Search transactions"
          className="flex-1"
        />
        {/* With a single account "All accounts" and that account are the same ledger. */}
        {accountOptions.length > 1 && (
          <div className="sm:w-56">
            <InlineSelect<string>
              value={account}
              onChange={onAccountChange}
              options={accountOptions}
              emptyOptionLabel="All accounts"
              placeholder="All accounts"
              ariaLabel="Filter by account"
              className="h-10"
            />
          </div>
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
