import type { Category, Transaction } from '../../../shared/types/api';

type EditExpenseFormProps = {
  expenseTransactions: Transaction[];
  editExpenseId: number | '';
  editExpense: {
    accountId: number;
    categoryId: number;
    amount: number;
    occurredAt: string;
    payee: string;
    notes: string;
  };
  setEditExpense: React.Dispatch<React.SetStateAction<EditExpenseFormProps['editExpense']>>;
  expenseCategories: Category[];
  onLoadForEdit: (id: number | '') => void;
  onSave: () => void;
  isSaving: boolean;
  isUpdating: boolean;
};

export function EditExpenseForm({
  expenseTransactions,
  editExpenseId,
  editExpense,
  setEditExpense,
  expenseCategories,
  onLoadForEdit,
  onSave,
  isSaving,
  isUpdating,
}: EditExpenseFormProps) {
  return (
    <div>
      <h3 className="text-sm font-semibold text-gray-900 mb-3">Update Expense Transaction</h3>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={editExpenseId}
          onChange={(e) => onLoadForEdit(e.target.value ? Number(e.target.value) : '')}
        >
          <option value="">Select expense transaction</option>
          {expenseTransactions.map((transaction) => (
            <option key={transaction.id} value={transaction.id}>
              #{transaction.id} - ${transaction.amount.toFixed(2)} - {transaction.payee || 'No payee'}
            </option>
          ))}
        </select>
        <input
          type="number"
          min={1}
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          placeholder="Account ID"
          value={editExpense.accountId}
          onChange={(e) => setEditExpense((prev) => ({ ...prev, accountId: Number(e.target.value) }))}
        />
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={editExpense.categoryId}
          onChange={(e) => setEditExpense((prev) => ({ ...prev, categoryId: Number(e.target.value) }))}
        >
          {expenseCategories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </select>
        <input
          type="number"
          step="0.01"
          min={0}
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          placeholder="Amount"
          value={editExpense.amount}
          onChange={(e) => setEditExpense((prev) => ({ ...prev, amount: Number(e.target.value) }))}
        />
        <input
          type="date"
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={editExpense.occurredAt}
          onChange={(e) => setEditExpense((prev) => ({ ...prev, occurredAt: e.target.value }))}
        />
        <input
          type="text"
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          placeholder="Payee"
          value={editExpense.payee}
          onChange={(e) => setEditExpense((prev) => ({ ...prev, payee: e.target.value }))}
        />
        <input
          type="text"
          className="rounded-md border border-gray-300 px-3 py-2 text-sm md:col-span-3"
          placeholder="Notes"
          value={editExpense.notes}
          onChange={(e) => setEditExpense((prev) => ({ ...prev, notes: e.target.value }))}
        />
      </div>
      <div className="mt-3">
        <button
          type="button"
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
          onClick={onSave}
          disabled={isSaving}
        >
          {isUpdating ? 'Saving...' : 'Update Expense'}
        </button>
      </div>
    </div>
  );
}
