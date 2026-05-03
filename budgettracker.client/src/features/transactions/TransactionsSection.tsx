import { useMemo, useState } from 'react';
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

  const expenseCategories = useMemo(
    () => categories.filter((c) => c.categoryType === 'Expense' || c.categoryType === 'Both'),
    [categories],
  );

  const form = useTransactionForm(transactions, expenseCategories, setStatusMessage, setStatusError);

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
        categories={expenseCategories}
        isSaving={form.isSaving}
        onClose={form.closeDialog}
        onSave={form.save}
        onDelete={form.deleteTransaction}
      />
    </>
  );
};

export default TransactionsSection;
