import { useMemo } from 'react';
import { DollarSign } from 'lucide-react';
import { useTransactions } from './hooks/useTransactions';
import { useTransactionForm } from './hooks/useTransactionForm';
import { useCategories } from '../categories/hooks/useCategories';
import { AddExpenseForm } from './components/AddExpenseForm';
import { EditExpenseForm } from './components/EditExpenseForm';
import { TransactionTable } from './components/TransactionTable';

type TransactionsSectionProps = {
  isLoading: boolean;
  setStatusMessage: (msg: string | null) => void;
  setStatusError: (msg: string | null) => void;
};

export default function TransactionsSection({
  isLoading,
  setStatusMessage,
  setStatusError,
}: TransactionsSectionProps) {
  const { data: transactions = [] } = useTransactions();
  const { data: categories = [] } = useCategories();

  const expenseCategories = useMemo(
    () => categories.filter((c) => c.categoryType === 'Expense' || c.categoryType === 'Both'),
    [categories],
  );

  const form = useTransactionForm(transactions, expenseCategories, setStatusMessage, setStatusError);

  if (isLoading) return null;

  return (
    <section className="bg-white rounded-lg shadow">
      <div className="px-6 py-4 border-b border-gray-200">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <DollarSign className="w-5 h-5 text-purple-600" />
            <h2 className="text-xl font-semibold text-gray-900">Transactions</h2>
          </div>
          <span className="text-sm text-gray-500">
            {transactions.length} {transactions.length === 1 ? 'transaction' : 'transactions'}
          </span>
        </div>
      </div>

      <div className="px-6 py-4 border-b border-gray-200 space-y-6 bg-gray-50">
        <AddExpenseForm
          newExpense={form.newExpense}
          setNewExpense={form.setNewExpense}
          expenseCategories={expenseCategories}
          onSave={form.saveNewExpense}
          isSaving={form.isSaving}
          isCreating={form.isCreating}
        />
        <EditExpenseForm
          expenseTransactions={form.expenseTransactions}
          editExpenseId={form.editExpenseId}
          editExpense={form.editExpense}
          setEditExpense={form.setEditExpense}
          expenseCategories={expenseCategories}
          onLoadForEdit={form.loadExpenseForEdit}
          onSave={form.saveEditedExpense}
          isSaving={form.isSaving}
          isUpdating={form.isUpdating}
        />
      </div>

      <TransactionTable transactions={transactions} />
    </section>
  );
}
