import { useEffect, useMemo, useState } from 'react';
import { format } from 'date-fns';
import { Sparkles, StickyNote } from 'lucide-react';
import { Badge, InlineCheckbox, InlineSelect } from '../../../shared/components/ui';
import type { Category, Transaction } from '../../../shared/types/api';
import { formatPlaidCategory } from '../../../shared/constants/plaidCategories';
import { signedAmount } from '../../../shared/utils/format';
import { isInflowTransaction } from '../utils/transactionGroups';
import { cn } from '../../../shared/utils/cn';

type TransactionRowsProps = {
  transactions: Transaction[];
  categories: Category[];
  maskByPlaidAccountId: Map<string, string | null | undefined>;
  selectedIds: Set<number>;
  onToggleSelected: (id: number, selected: boolean) => void;
  onCategoryChange: (id: number, categoryId: number | null) => void;
  onNotesChange: (id: number, notes: string | null) => void;
  isBusy: boolean;
};

/**
 * The transaction row, shared by the grouped and flat views so a row looks and behaves the same
 * wherever it appears. Every write here is one of the two a person can make on imported data:
 * assign a category, or leave a note.
 */
const TransactionRows = ({
  transactions,
  categories,
  maskByPlaidAccountId,
  selectedIds,
  onToggleSelected,
  onCategoryChange,
  onNotesChange,
  isBusy,
}: TransactionRowsProps) => {
  const categoryOptions = useMemo(
    () => categories.map((c) => ({ value: c.id, label: c.name })),
    [categories],
  );

  // Plaid taxonomy value -> the user's category claiming it, for one-click accept (BUD-9).
  const categoryByPlaidValue = useMemo(() => {
    const map = new Map<string, Category>();
    for (const category of categories) {
      if (category.plaidCategoryPrimary) {
        map.set(category.plaidCategoryPrimary.toUpperCase(), category);
      }
    }
    return map;
  }, [categories]);

  if (transactions.length === 0) {
    return (
      <p className="px-4 py-6 text-center text-sm text-ink-muted">No transactions here.</p>
    );
  }

  return (
    <ul className="divide-y divide-border-subtle">
      {transactions.map((transaction) => {
        const suggestion = transaction.plaidCategoryPrimary
          ? categoryByPlaidValue.get(transaction.plaidCategoryPrimary.toUpperCase())
          : undefined;

        return (
          <TransactionRow
            key={transaction.id}
            transaction={transaction}
            categoryOptions={categoryOptions}
            suggestion={suggestion}
            mask={
              transaction.plaidAccountId
                ? maskByPlaidAccountId.get(transaction.plaidAccountId)
                : undefined
            }
            selected={selectedIds.has(transaction.id)}
            onToggleSelected={onToggleSelected}
            onCategoryChange={onCategoryChange}
            onNotesChange={onNotesChange}
            isBusy={isBusy}
          />
        );
      })}
    </ul>
  );
};

type TransactionRowProps = {
  transaction: Transaction;
  categoryOptions: { value: number; label: string }[];
  suggestion: Category | undefined;
  mask: string | null | undefined;
  selected: boolean;
  onToggleSelected: (id: number, selected: boolean) => void;
  onCategoryChange: (id: number, categoryId: number | null) => void;
  onNotesChange: (id: number, notes: string | null) => void;
  isBusy: boolean;
};

const TransactionRow = ({
  transaction,
  categoryOptions,
  suggestion,
  mask,
  selected,
  onToggleSelected,
  onCategoryChange,
  onNotesChange,
  isBusy,
}: TransactionRowProps) => {
  const [editingNote, setEditingNote] = useState(false);
  const [noteDraft, setNoteDraft] = useState(transaction.notes ?? '');

  // A re-sync can change the stored note underneath an open editor; follow it when not editing.
  useEffect(() => {
    if (!editingNote) setNoteDraft(transaction.notes ?? '');
  }, [transaction.notes, editingNote]);

  const inflow = isInflowTransaction(transaction);

  const commitNote = () => {
    setEditingNote(false);
    const next = noteDraft.trim();
    if (next === (transaction.notes ?? '')) return;
    onNotesChange(transaction.id, next.length > 0 ? next : null);
  };

  return (
    <li
      className={cn(
        'flex items-start gap-3 px-4 py-3 transition-colors duration-120',
        selected ? 'bg-primary-subtle/40' : 'hover:bg-background',
      )}
    >
      <div className="pt-0.5">
        <InlineCheckbox
          checked={selected}
          onCheckedChange={(next) => onToggleSelected(transaction.id, next)}
          ariaLabel={`Select ${transaction.payee ?? 'transaction'}`}
        />
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
          <span className="truncate text-sm font-medium text-ink">
            {transaction.payee || 'Untitled transaction'}
          </span>
          <span
            className={cn(
              'numeric shrink-0 text-sm font-semibold',
              inflow ? 'text-success-dark' : 'text-ink',
            )}
          >
            {signedAmount(transaction.amount, inflow)}
          </span>
        </div>

        <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-ink-muted">
          <span className="numeric">{format(new Date(transaction.occurredAt), 'MMM d')}</span>
          {mask && <span>•••• {mask}</span>}
          {transaction.isPending && <Badge label="Pending" color="warning" variant="soft" />}
        </div>

        <div className="mt-2 flex flex-wrap items-center gap-2">
          <div className="w-56">
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
          </div>

          {transaction.categoryId == null && suggestion && (
            <button
              type="button"
              disabled={isBusy}
              onClick={() => onCategoryChange(transaction.id, suggestion.id)}
              className="focus-ring inline-flex items-center gap-1 rounded-md border border-primary/40 bg-primary-subtle px-2 py-1 text-xs font-medium text-primary-dark transition-colors duration-120 hover:border-primary disabled:opacity-50"
            >
              <Sparkles size={11} />
              Use {suggestion.name}
            </button>
          )}

          {transaction.categoryId == null && !suggestion && transaction.plaidCategoryPrimary && (
            <span className="inline-flex items-center gap-1 text-xs text-ink-muted">
              <Sparkles size={11} />
              Plaid says {formatPlaidCategory(transaction.plaidCategoryPrimary)}
            </span>
          )}

          {editingNote ? (
            <input
              autoFocus
              value={noteDraft}
              maxLength={1000}
              onChange={(e) => setNoteDraft(e.target.value)}
              onBlur={commitNote}
              onKeyDown={(e) => {
                if (e.key === 'Enter') commitNote();
                if (e.key === 'Escape') {
                  setNoteDraft(transaction.notes ?? '');
                  setEditingNote(false);
                }
              }}
              placeholder="Add a note…"
              aria-label={`Note for ${transaction.payee ?? 'transaction'}`}
              className="focus-ring h-8 min-w-[14rem] flex-1 rounded-md border border-border bg-surface px-2 text-xs text-ink"
            />
          ) : (
            <button
              type="button"
              disabled={isBusy}
              onClick={() => setEditingNote(true)}
              className="focus-ring inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-ink-muted transition-colors duration-120 hover:bg-surface hover:text-ink disabled:opacity-50"
            >
              <StickyNote size={11} />
              {transaction.notes ? transaction.notes : 'Add note'}
            </button>
          )}
        </div>
      </div>
    </li>
  );
};

export default TransactionRows;
