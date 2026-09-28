import { useState } from 'react';
import { ChevronDown } from 'lucide-react';
import { Badge, Card, Collapsible, Progress } from '../../../shared/components/ui';
import type { Category } from '../../../shared/types/api';
import { currency } from '../../../shared/utils/format';
import { cssVar } from '../../../shared/theme/tokens';
import { cn } from '../../../shared/utils/cn';
import type { BucketGroup, CategoryGroup, PlanGrouping, SpendStatus } from '../utils/planGrouping';
import { spendStatus } from '../utils/planGrouping';
import TransactionRows from './TransactionRows';

type PlanGroupedViewProps = {
  grouping: PlanGrouping;
  categories: Category[];
  maskByPlaidAccountId: Map<string, string | null | undefined>;
  selectedIds: Set<number>;
  onToggleSelected: (id: number, selected: boolean) => void;
  onCategoryChange: (id: number, categoryId: number | null) => void;
  onNotesChange: (id: number, notes: string | null) => void;
  isBusy: boolean;
};

/**
 * The four-step ramp mapped onto the existing semantic scales. `status.*` tokens are specced in
 * ui-modernization.md but were never built, so warn-low and warn-high borrow warning's two shades.
 *
 * Must go through `cssVar` — these are inline styles, which Tailwind's `dark:` variant cannot
 * reach, so a resolved hex would freeze the light palette.
 */
const STATUS_BAR: Record<SpendStatus, string> = {
  none: cssVar('secondary'),
  'on-pace': cssVar('success'),
  'warn-low': cssVar('warning'),
  'warn-high': cssVar('warning-dark'),
  over: cssVar('error'),
};

const BUCKET_BLURB: Record<BucketGroup['bucket'], string> = {
  Core: 'Committed spending you have signed up for.',
  Buffer: 'Flex spending. Anything unplanned or uncategorised lands here too.',
};

/**
 * The month grouped the way the budget is actually structured: Core commitments first, then Buffer,
 * with uncategorised pinned above both because it is invisible to every figure until it is cleared.
 */
const PlanGroupedView = ({
  grouping,
  categories,
  maskByPlaidAccountId,
  selectedIds,
  onToggleSelected,
  onCategoryChange,
  onNotesChange,
  isBusy,
}: PlanGroupedViewProps) => {
  const rowProps = {
    categories,
    maskByPlaidAccountId,
    selectedIds,
    onToggleSelected,
    onCategoryChange,
    onNotesChange,
    isBusy,
  };

  return (
    <div className="flex flex-col gap-5">
      {grouping.uncategorized && (
        <Card padding="none">
          <div className="border-b border-border bg-warning-subtle/40 px-4 py-3">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <div>
                <h3 className="text-sm font-semibold text-ink">Needs a category</h3>
                <p className="mt-0.5 text-xs text-ink-muted">
                  These are missing from every figure above until you categorise them.
                </p>
              </div>
              <span className="numeric text-sm font-semibold text-ink">
                {currency.format(grouping.uncategorized.actual)}
              </span>
            </div>
          </div>
          <TransactionRows transactions={grouping.uncategorized.transactions} {...rowProps} />
        </Card>
      )}

      {grouping.buckets.map((bucket) => (
        <Card key={bucket.bucket} padding="none">
          <div className="border-b border-border px-4 py-3">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <div>
                <h3 className="text-sm font-semibold text-ink">{bucket.bucket}</h3>
                <p className="mt-0.5 text-xs text-ink-muted">{BUCKET_BLURB[bucket.bucket]}</p>
              </div>
              <span className="numeric text-sm text-ink-muted">
                <span className="font-semibold text-ink">{currency.format(bucket.actual)}</span>
                {bucket.planned > 0 && <> of {currency.format(bucket.planned)}</>}
              </span>
            </div>
          </div>

          {bucket.categories.length === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-ink-muted">
              Nothing in {bucket.bucket} this month.
            </p>
          ) : (
            <ul>
              {bucket.categories.map((category) => (
                <CategoryRow key={category.categoryId} category={category} {...rowProps} />
              ))}
            </ul>
          )}
        </Card>
      ))}
    </div>
  );
};

type CategoryRowProps = {
  category: CategoryGroup;
  categories: Category[];
  maskByPlaidAccountId: Map<string, string | null | undefined>;
  selectedIds: Set<number>;
  onToggleSelected: (id: number, selected: boolean) => void;
  onCategoryChange: (id: number, categoryId: number | null) => void;
  onNotesChange: (id: number, notes: string | null) => void;
  isBusy: boolean;
};

const CategoryRow = ({ category, ...rowProps }: CategoryRowProps) => {
  const [open, setOpen] = useState(false);
  const status = spendStatus(category.planned, category.actual);
  const hasTransactions = category.transactions.length > 0;

  const percentOfPlan =
    category.planned > 0 ? Math.min((category.actual / category.planned) * 100, 100) : 0;

  return (
    <li className="border-b border-border-subtle last:border-b-0">
      <button
        type="button"
        onClick={() => hasTransactions && setOpen((v) => !v)}
        aria-expanded={hasTransactions ? open : undefined}
        disabled={!hasTransactions}
        className={cn(
          'focus-ring flex w-full items-center gap-3 px-4 py-3 text-left transition-colors duration-120',
          hasTransactions ? 'hover:bg-background' : 'cursor-default',
        )}
      >
        <ChevronDown
          size={14}
          className={cn(
            'shrink-0 text-ink-muted transition-transform duration-120',
            !hasTransactions && 'invisible',
            open && 'rotate-180',
          )}
        />

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
            <span className="truncate text-sm font-medium text-ink">{category.name}</span>
            <span className="numeric shrink-0 text-sm text-ink-muted">
              <span className="font-semibold text-ink">{currency.format(category.actual)}</span>
              {category.planned > 0 ? (
                <> of {currency.format(category.planned)}</>
              ) : (
                <> · unbudgeted</>
              )}
            </span>
          </div>

          {category.planned > 0 && (
            <div className="mt-2 flex items-center gap-2.5">
              <Progress
                value={percentOfPlan}
                height={6}
                barColor={STATUS_BAR[status]}
                className="flex-1"
              />
              {category.overBy > 0 && (
                <Badge
                  label={`${currency.format(category.overBy)} over`}
                  color={status === 'over' ? 'error' : 'warning'}
                  variant="soft"
                />
              )}
            </div>
          )}
        </div>
      </button>

      {hasTransactions && (
        <Collapsible open={open}>
          <div className="border-t border-border-subtle bg-background">
            <TransactionRows transactions={category.transactions} {...rowProps} />
          </div>
        </Collapsible>
      )}
    </li>
  );
};

export default PlanGroupedView;
