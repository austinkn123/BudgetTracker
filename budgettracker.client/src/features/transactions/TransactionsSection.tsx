import { useMemo } from 'react';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import { Plus } from 'lucide-react';
import { useTransactions } from './hooks/useTransactions';
import { useTransactionForm } from './hooks/useTransactionForm';
import { useCategories } from '../categories/hooks/useCategories';
import TransactionDialog from './components/TransactionDialog';
import TransactionTable from './components/TransactionTable';

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

  const expenseCategories = useMemo(
    () => categories.filter((c) => c.categoryType === 'Expense' || c.categoryType === 'Both'),
    [categories],
  );

  const form = useTransactionForm(transactions, expenseCategories, setStatusMessage, setStatusError);

  if (isLoading) return null;

  return (
    <>
      <div className="flex items-center justify-between">
        <span className="text-sm text-gray-500">
          {transactions.length} {transactions.length === 1 ? 'transaction' : 'transactions'}
        </span>
        <Button variant="contained" startIcon={<Plus className="w-4 h-4" />} onClick={form.openForAdd}>
          Add Transaction
        </Button>
      </div>

      <Card variant="outlined">
        <TransactionTable
          transactions={transactions}
          categories={categories}
          onRowClick={form.openForEdit}
        />
      </Card>

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
