import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { format, startOfDay, startOfMonth } from 'date-fns';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { Plus } from 'lucide-react';
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
      <Stack spacing={2.5}>
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <Typography variant="body2" color="text.secondary">
              {visibleMonthSummary.label} has {visibleMonthSummary.transactionCount}{' '}
              {visibleMonthSummary.transactionCount === 1 ? 'transaction' : 'transactions'} across{' '}
              {visibleMonthSummary.activeDayCount} active{' '}
              {visibleMonthSummary.activeDayCount === 1 ? 'day' : 'days'}.
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Select a day on the calendar to inspect that ledger slice or start a new entry.
            </Typography>
          </div>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.25}>
            <Button variant="outlined" onClick={handleToday}>
              Today
            </Button>
            <Button
              variant="contained"
              startIcon={<Plus className="w-4 h-4" />}
              onClick={() => form.openForAdd(toDateKey(selectedDate))}
            >
              Add Transaction
            </Button>
          </Stack>
        </div>

        <Card variant="outlined" sx={{ p: 2 }}>
          <Stack direction={{ xs: 'column', lg: 'row' }} spacing={1.25} useFlexGap flexWrap="wrap">
            <Chip label={`Selected ${format(selectedDate, 'PP')}`} color="primary" variant="outlined" />
            <Chip label={`Income $${visibleMonthSummary.incomeTotal.toFixed(2)}`} color="success" variant="outlined" />
            <Chip label={`Outflow $${visibleMonthSummary.outflowTotal.toFixed(2)}`} color="error" variant="outlined" />
            <Chip
              label={`Net ${visibleMonthSummary.netTotal >= 0 ? '+' : '-'}$${Math.abs(visibleMonthSummary.netTotal).toFixed(2)}`}
              color={visibleMonthSummary.netTotal >= 0 ? 'success' : 'error'}
            />
          </Stack>
        </Card>

        <Card variant="outlined">
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
      </Stack>

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
