import { useMemo } from 'react';
import { format } from 'date-fns';
import { Sparkles } from 'lucide-react';
import { Badge, Button, InlineCheckbox, InlineSelect } from '../../../shared/components/ui';
import type { Category, Transaction } from '../../../shared/types/api';
import { isInflowTransaction } from '../utils/transactionGroups';
import { formatPlaidCategory } from '../../../shared/constants/plaidCategories';
import { signedAmount } from '../../../shared/utils/format';
import { cn } from '../../../shared/utils/cn';

type TransactionListProps = {
  transactions: Transaction[];
  categories: Category[];
  maskByPlaidAccountId: Map<string, string | null | undefined>;
  selectedIds: Set<number>;
  onToggleSelected: (id: number, selected: boolean) => void;
  onToggleAll: (selected: boolean) => void;
  onCategoryChange: (id: number, categoryId: number | null) => void;
  onRowClick: (transaction: Transaction) => void;
  isBusy: boolean;
};

/**
 * Flat, scannable ledger with inline categorisation. This is the review surface: the calendar
 * view answers "what happened on the 14th", but categorising a few hundred imported rows needs
 * one list, a dropdown per row, and multi-select.
 */
const TransactionList = ({
  transactions,
  categories,
  maskByPlaidAccountId,
  selectedIds,
  onToggleSelected,
  onToggleAll,
  onCategoryChange,
  onRowClick,
  isBusy,
}: TransactionListProps) => {
  const categoryOptions = useMemo(
    () => categories.map((c) => ({ value: c.id, label: c.name })),
    [categories],
  );

  // Plaid taxonomy value -> the user's category that claims it, for one-click accept (BUD-9).
  const categoryByPlaidValue = useMemo(() => {
    const map = new Map<string, Category>();
    for (const category of categories) {
      if (category.plaidCategoryPrimary) {
        map.set(category.plaidCategoryPrimary.toUpperCase(), category);
      }
    }
    return map;
  }, [categories]);

  const allSelected = transactions.length > 0 && selectedIds.size === transactions.length;
  const someSelected = selectedIds.size > 0 && !allSelected;

  if (transactions.length === 0) {
    return (
      <div className="rounded-md border border-dashed border-border bg-background p-8 text-center">
        <p className="text-sm font-semibold text-ink">Nothing matches these filters</p>
        <p className="mt-1.5 text-sm text-ink-muted">
          Try clearing the search or switching back to All.
        </p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[720px] border-collapse text-sm">
        <thead>
          <tr className="border-b border-border text-left text-xs uppercase tracking-wide text-ink-muted">
            <th scope="col" className="w-10 px-3 py-2.5">
              <InlineCheckbox
                checked={allSelected ? true : someSelected ? 'indeterminate' : false}
                onCheckedChange={onToggleAll}
                ariaLabel={allSelected ? 'Deselect all transactions' : 'Select all transactions'}
              />
            </th>
            <th scope="col" className="px-3 py-2.5 font-medium">Date</th>
            <th scope="col" className="px-3 py-2.5 font-medium">Payee</th>
            <th scope="col" className="w-56 px-3 py-2.5 font-medium">Category</th>
            <th scope="col" className="px-3 py-2.5 text-right font-medium">Amount</th>
          </tr>
        </thead>
        <tbody>
          {transactions.map((transaction) => {
            const inflow = isInflowTransaction(transaction);
            const mask = transaction.plaidAccountId
              ? maskByPlaidAccountId.get(transaction.plaidAccountId)
              : undefined;
            const selected = selectedIds.has(transaction.id);
            const suggestion = transaction.plaidCategoryPrimary
              ? categoryByPlaidValue.get(transaction.plaidCategoryPrimary.toUpperCase())
              : undefined;

            return (
              <tr
                key={transaction.id}
                className={cn(
                  'border-b border-border-subtle transition-colors duration-120',
                  selected ? 'bg-primary-subtle/40' : 'hover:bg-background',
                )}
              >
                <td className="px-3 py-2.5 align-middle">
                  <InlineCheckbox
                    checked={selected}
                    onCheckedChange={(next) => onToggleSelected(transaction.id, next)}
                    ariaLabel={`Select transaction ${transaction.payee ?? transaction.id}`}
                  />
                </td>

                <td className="whitespace-nowrap px-3 py-2.5 align-middle text-ink-muted">
                  {format(new Date(transaction.occurredAt), 'MMM d')}
                </td>

                <td className="px-3 py-2.5 align-middle">
                  <button
                    type="button"
                    onClick={() => onRowClick(transaction)}
                    className="focus-ring rounded text-left font-medium text-ink hover:underline"
                  >
                    {transaction.payee || 'Untitled transaction'}
                  </button>
                  <div className="mt-1 flex flex-wrap items-center gap-1.5">
                    {transaction.isImported && (
                      <Badge label="Imported" color="neutral" variant="outline" />
                    )}
                    {transaction.isPending && (
                      <Badge label="Pending" color="warning" variant="soft" />
                    )}
                    {mask && <span className="text-xs text-ink-muted">•••• {mask}</span>}
                  </div>
                </td>

                <td className="px-3 py-2.5 align-middle">
                  <InlineSelect
                    value={transaction.categoryId ?? null}
                    onChange={(next) => onCategoryChange(transaction.id, next)}
                    options={categoryOptions}
                    emptyOptionLabel="Uncategorized"
                    placeholder="Uncategorized"
                    ariaLabel={`Category for ${transaction.payee ?? 'transaction'}`}
                    valueAs="number"
                    disabled={isBusy}
                    subtle
                  />
                  {transaction.categoryId == null && transaction.plaidCategoryPrimary && (
                    <div className="mt-1 flex flex-wrap items-center gap-1 px-2 text-xs text-ink-muted">
                      <Sparkles size={11} />
                      {suggestion ? (
                        <>
                          <span>Plaid suggests</span>
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={isBusy}
                            onClick={() => onCategoryChange(transaction.id, suggestion.id)}
                          >
                            {suggestion.name}
                          </Button>
                        </>
                      ) : (
                        <span>
                          Plaid says {formatPlaidCategory(transaction.plaidCategoryPrimary)} — map it
                          on a category to auto-assign
                        </span>
                      )}
                    </div>
                  )}
                </td>

                <td
                  className={cn(
                    'numeric whitespace-nowrap px-3 py-2.5 text-right align-middle font-semibold',
                    inflow ? 'text-success-dark' : 'text-error-dark',
                  )}
                >
                  {signedAmount(transaction.amount, inflow)}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
};

export default TransactionList;
