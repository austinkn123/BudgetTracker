import { useMemo } from 'react';
import { Card, InlineSelect } from '../../../shared/components/ui';
import type { SelectOption } from '../../../shared/components/ui/Select';
import type { Transaction } from '../../../shared/types/api';
import { formatPlaidCategory } from '../../../shared/constants/plaidCategories';
import { signedCurrency } from '../../../shared/utils/format';
import { groupUncategorizedByPayee } from '../utils/transactionGroups';

type PayeeReviewProps = {
  transactions: Transaction[];
  categoryOptions: SelectOption<number>[];
  onCategorize: (ids: number[], categoryId: number | null) => void;
  isBusy: boolean;
};

/**
 * The whole uncategorized backlog, one line per payee. Filing a payee here categorizes every one of
 * its rows, and sync then files that payee's new rows the same way.
 */
const PayeeReview = ({ transactions, categoryOptions, onCategorize, isBusy }: PayeeReviewProps) => {
  const groups = useMemo(() => groupUncategorizedByPayee(transactions), [transactions]);

  return (
    <Card padding="none">
      <div className="border-b border-border bg-warning-subtle/40 px-4 py-3">
        <h3 className="text-sm font-semibold text-ink">Categorize by payee</h3>
        <p className="mt-0.5 text-xs text-ink-muted">
          Pick once per payee. New transactions from that payee follow your pick.
        </p>
      </div>

      {groups.length === 0 ? (
        <p className="px-4 py-6 text-center text-sm text-ink-muted">Everything is categorized.</p>
      ) : (
        <ul className="divide-y divide-border-subtle">
          {groups.map((group) => (
            <li key={group.payee} className="flex items-center gap-3 px-4 py-2.5">
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-ink">{group.payee}</p>
                <p className="text-xs text-ink-muted">
                  {group.ids.length} {group.ids.length === 1 ? 'transaction' : 'transactions'}
                  {group.plaidCategoryPrimary && (
                    <> · Plaid: {formatPlaidCategory(group.plaidCategoryPrimary)}</>
                  )}
                </p>
              </div>
              <span className="numeric shrink-0 text-sm text-ink">
                {signedCurrency(group.netTotal)}
              </span>
              <div className="w-56 shrink-0">
                <InlineSelect
                  value={null}
                  onChange={(categoryId) => categoryId != null && onCategorize(group.ids, categoryId)}
                  options={categoryOptions}
                  placeholder="Set category…"
                  ariaLabel={`Set category for ${group.payee}`}
                  valueAs="number"
                  disabled={isBusy}
                />
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
};

export default PayeeReview;
