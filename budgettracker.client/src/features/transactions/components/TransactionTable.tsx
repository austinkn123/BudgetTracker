import { useMemo } from 'react';
import { format, startOfDay, startOfMonth } from 'date-fns';
import { Badge, Button, Calendar, Separator } from '../../../shared/components/ui';
import type { Category, Transaction } from '../../../shared/types/api';
import {
  toDateKey,
  type TransactionDaySummary,
} from '../utils/transactionGroups';

type TransactionTableProps = {
  categories: Category[];
  daySummaries: Map<string, TransactionDaySummary>;
  selectedDate: Date;
  selectedDaySummary: TransactionDaySummary | null;
  maskByPlaidAccountId: Map<string, string | null | undefined>;
  onDateChange: (date: Date) => void;
  onMonthChange: (month: Date) => void;
  onAddTransaction: (occurredAt?: string) => void;
  onRowClick: (transaction: Transaction) => void;
};

const TransactionTable = ({
  categories,
  daySummaries,
  selectedDate,
  selectedDaySummary,
  maskByPlaidAccountId,
  onDateChange,
  onMonthChange,
  onAddTransaction,
  onRowClick,
}: TransactionTableProps) => {
  const categoryMap = new Map(categories.map((c) => [c.id, c.name]));
  const selectedDateKey = toDateKey(selectedDate);
  const selectedTransactions = selectedDaySummary?.transactions ?? [];
  const selectedIncomeTotal = selectedDaySummary?.incomeTotal ?? 0;
  const selectedOutflowTotal = selectedDaySummary?.outflowTotal ?? 0;
  const selectedNetTotal = selectedDaySummary?.netTotal ?? 0;

  const visibleMonth = startOfMonth(selectedDate);

  // Day tinting: only days in the visible month with a summary.
  // Filtering here reproduces the old outside-month/no-summary guard —
  // react-day-picker applies modifiers to outside days too.
  const { positiveDays, negativeDays } = useMemo(() => {
    const positive: Date[] = [];
    const negative: Date[] = [];
    const monthKeyPrefix = format(visibleMonth, 'yyyy-MM');
    for (const [dateKey, summary] of daySummaries) {
      if (!dateKey.startsWith(monthKeyPrefix)) continue;
      const [year, month, day] = dateKey.split('-').map(Number);
      const date = new Date(year, month - 1, day);
      (summary.netTotal >= 0 ? positive : negative).push(date);
    }
    return { positiveDays: positive, negativeDays: negative };
  }, [daySummaries, visibleMonth]);

  return (
    <div className="flex flex-col items-stretch lg:flex-row">
      <div className="order-1 min-w-0 flex-1 border-b border-border-subtle bg-background/50 p-5 pb-4 lg:order-2 lg:border-b-0 lg:border-l">
        <div className="flex flex-col gap-4">
          <div>
            <span className="text-2xs font-semibold uppercase tracking-[0.06em] text-ink-muted">
              Selected Day
            </span>
            <h3 className="text-base font-semibold text-ink">{format(selectedDate, 'PPPP')}</h3>
            <p className="text-sm text-ink-muted">
              {selectedTransactions.length > 0
                ? `Review ${selectedTransactions.length} ${selectedTransactions.length === 1 ? 'transaction' : 'transactions'} for this day.`
                : 'No transactions are recorded for this day yet.'}
            </p>
          </div>

          <div className="flex flex-row flex-wrap gap-2">
            <Badge label={`${selectedTransactions.length} item${selectedTransactions.length === 1 ? '' : 's'}`} />
            <Badge variant="outline" color="success" label={`Income $${selectedIncomeTotal.toFixed(2)}`} />
            <Badge variant="outline" color="error" label={`Outflow $${selectedOutflowTotal.toFixed(2)}`} />
            <Badge
              color={selectedNetTotal >= 0 ? 'success' : 'error'}
              label={`Net ${selectedNetTotal >= 0 ? '+' : '-'}$${Math.abs(selectedNetTotal).toFixed(2)}`}
            />
          </div>

          {selectedTransactions.length > 0 ? (
            <ul className="overflow-hidden rounded-md border border-border">
              {selectedTransactions.map((transaction, index) => {
                // An Adjustment is a user-driven balance correction. Its sign carries
                // semantic meaning: negative = balance was overstated (outflow correction,
                // render as expense), positive = balance was understated (inflow correction,
                // render as income). The engine accepts either sign for this type precisely
                // because direction is the user's choice; the UI must honor that choice
                // rather than forcing all Adjustments into the "outflow" bucket.
                const isInflow =
                  transaction.transactionType === 'Income' ||
                  (transaction.transactionType === 'Adjustment' && transaction.amount > 0);
                const sign = isInflow ? '+' : '-';

                const mask = transaction.plaidAccountId
                  ? maskByPlaidAccountId.get(transaction.plaidAccountId)
                  : undefined;
                const secondaryText = [
                  categoryMap.get(transaction.categoryId) ?? 'Uncategorized',
                  transaction.notes || transaction.transactionType,
                ]
                  .filter(Boolean)
                  .join(' · ');

                return (
                  <li key={transaction.id}>
                    {index > 0 && <Separator />}
                    <button
                      type="button"
                      onClick={() => onRowClick(transaction)}
                      className="focus-ring flex w-full items-start justify-between gap-4 px-4 py-3 text-left transition-colors duration-120 hover:bg-background"
                    >
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-ink">
                          {transaction.payee ||
                            categoryMap.get(transaction.categoryId) ||
                            'Uncategorized transaction'}
                        </p>
                        <span className="text-sm text-ink-muted">{secondaryText}</span>
                        {transaction.isImported && (
                          <span className="mt-1 flex flex-row flex-wrap items-center gap-1.5">
                            <Badge variant="outline" label="Imported" />
                            {mask && <span className="text-xs text-ink-muted">{`•••• ${mask}`}</span>}
                            {transaction.isPending && <Badge color="warning" label="Pending" />}
                          </span>
                        )}
                      </div>
                      <span
                        className={
                          isInflow
                            ? 'whitespace-nowrap text-sm font-semibold text-success-dark'
                            : 'whitespace-nowrap text-sm font-semibold text-error'
                        }
                      >
                        {sign}${Math.abs(transaction.amount).toFixed(2)}
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          ) : (
            <div className="rounded-md border border-dashed border-border bg-background p-6 text-center">
              <p className="text-sm font-semibold text-ink">
                No transactions on {format(selectedDate, 'PP')}
              </p>
              <p className="mb-4 mt-1.5 text-sm text-ink-muted">
                Choose another date on the calendar or add a transaction for this day.
              </p>
              <Button onClick={() => onAddTransaction(selectedDateKey)}>
                Add Transaction for This Day
              </Button>
            </div>
          )}
        </div>
      </div>

      <div className="order-2 flex shrink-0 justify-center px-2 py-4 md:px-4 lg:order-1 lg:w-[340px]">
        <Calendar
          selected={selectedDate}
          onSelect={(date) => onDateChange(startOfDay(date))}
          month={visibleMonth}
          onMonthChange={(month) => onMonthChange(startOfDay(month))}
          positiveDays={positiveDays}
          negativeDays={negativeDays}
        />
      </div>
    </div>
  );
};

export default TransactionTable;
