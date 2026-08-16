import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { format, startOfDay, startOfMonth } from 'date-fns';
import { Plus } from 'lucide-react';
import { Badge, Button, Card } from '../../shared/components/ui';
import { useTransactions } from './hooks/useTransactions';
import { useTransactionForm } from './hooks/useTransactionForm';
import { useCategories } from '../categories/hooks/useCategories';
import { plaidService } from '../../shared/services/plaid.service';
import TransactionDialog from './components/TransactionDialog';
import TransactionTable from './components/TransactionTable';
import {
  buildTransactionDaySummaries,
  getTransactionDaySummary,
  getTransactionMonthSummary,
  toDateKey,
} from './utils/transactionGroups';

type TransactionsSectionProps = {
  isLoading: boolean;
  setStatusMessage: (msg: string | null) => void;
  setStatusError: (msg: string | null) => void;
};

const TransactionsSection = ({
  isLoading,
  setStatusMessage,
  setStatusError,
}: TransactionsSectionProps) => {
  const { data: transactions = [] } = useTransactions();
  const { data: categories = [] } = useCategories();
  const [selectedDate, setSelectedDate] = useState(() => startOfDay(new Date()));

  // Read-only share of the Settings page connection cache (same query key) so we
  // can resolve a Plaid account mask for each imported row. Never mutates.
  const { data: connection } = useQuery({
    queryKey: ['plaid', 'connection'],
    queryFn: plaidService.getConnection,
  });

  const maskByPlaidAccountId = useMemo(
    () => new Map(connection?.accounts.map((a) => [a.plaidAccountId, a.mask]) ?? []),
    [connection],
  );

  const expenseCategories = useMemo(
    () => categories.filter((c) => c.categoryType === 'Expense' || c.categoryType === 'Both'),
    [categories],
  );

  const incomeCategories = useMemo(
    () => categories.filter((c) => c.categoryType === 'Income' || c.categoryType === 'Both'),
    [categories],
  );

  const form = useTransactionForm(transactions, expenseCategories, setStatusMessage, setStatusError);

  // For a locked (imported) edit, offer categories matching the row's type so the
  // dropdown isn't empty for Income rows, falling back to ALL categories.
  const dialogCategories = useMemo(() => {
    const editing = form.editingTransaction;
    if (!editing || !form.locked) return expenseCategories;
    const typed = editing.transactionType === 'Income' ? incomeCategories : expenseCategories;
    return typed.length > 0 ? typed : categories;
  }, [form.editingTransaction, form.locked, expenseCategories, incomeCategories, categories]);

  const daySummaries = useMemo(() => buildTransactionDaySummaries(transactions), [transactions]);
  const selectedDaySummary = useMemo(
    () => getTransactionDaySummary(daySummaries, selectedDate),
    [daySummaries, selectedDate],
  );
  const visibleMonthSummary = useMemo(
    () => getTransactionMonthSummary(daySummaries, selectedDate),
    [daySummaries, selectedDate],
  );

  const handleToday = () => {
    setSelectedDate(startOfDay(new Date()));
  };

  if (isLoading) return null;

  return (
    <>
      <div className="flex flex-col gap-5">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <p className="text-sm text-ink-muted">
              {visibleMonthSummary.label} has {visibleMonthSummary.transactionCount}{' '}
              {visibleMonthSummary.transactionCount === 1 ? 'transaction' : 'transactions'} across{' '}
              {visibleMonthSummary.activeDayCount} active{' '}
              {visibleMonthSummary.activeDayCount === 1 ? 'day' : 'days'}.
            </p>
            <span className="text-xs text-ink-muted">
              Select a day on the calendar to inspect that ledger slice or start a new entry.
            </span>
          </div>
          <div className="flex flex-col gap-2.5 sm:flex-row">
            <Button variant="secondary" onClick={handleToday}>
              Today
            </Button>
            <Button
              startIcon={<Plus size={16} />}
              onClick={() => form.openForAdd(toDateKey(selectedDate))}
            >
              Add Transaction
            </Button>
          </div>
        </div>

        <Card padding="sm">
          <div className="flex flex-col flex-wrap gap-2.5 lg:flex-row">
            <Badge label={`Selected ${format(selectedDate, 'PP')}`} color="primary" variant="outline" />
            <Badge label={`Income $${visibleMonthSummary.incomeTotal.toFixed(2)}`} color="success" variant="outline" />
            <Badge label={`Outflow $${visibleMonthSummary.outflowTotal.toFixed(2)}`} color="error" variant="outline" />
            <Badge
              label={`Net ${visibleMonthSummary.netTotal >= 0 ? '+' : '-'}$${Math.abs(visibleMonthSummary.netTotal).toFixed(2)}`}
              color={visibleMonthSummary.netTotal >= 0 ? 'success' : 'error'}
            />
          </div>
        </Card>

        <Card padding="none">
          <TransactionTable
            categories={categories}
            daySummaries={daySummaries}
            selectedDate={selectedDate}
            selectedDaySummary={selectedDaySummary}
            maskByPlaidAccountId={maskByPlaidAccountId}
            onDateChange={setSelectedDate}
            onMonthChange={(month) => setSelectedDate(startOfMonth(month))}
            onAddTransaction={form.openForAdd}
            onRowClick={form.openForEdit}
          />
        </Card>
      </div>

      <TransactionDialog
        open={form.dialogOpen}
        mode={form.dialogMode}
        initialValues={form.initialValues}
        categories={dialogCategories}
        isSaving={form.isSaving}
        locked={form.locked}
        onClose={form.closeDialog}
        onSave={form.save}
        onDelete={form.deleteTransaction}
      />
    </>
  );
};

export default TransactionsSection;
