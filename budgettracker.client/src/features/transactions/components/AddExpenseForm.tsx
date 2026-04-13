import type { Category } from '../../../shared/types/api';

type AddExpenseFormProps = {
  newExpense: {
    accountId: number;
    categoryId: number;
    amount: number;
    occurredAt: string;
    payee: string;
    notes: string;
  };
  setNewExpense: React.Dispatch<React.SetStateAction<AddExpenseFormProps['newExpense']>>;
  expenseCategories: Category[];
  onSave: () => void;
  isSaving: boolean;
  isCreating: boolean;
};

export function AddExpenseForm({
  newExpense,
  setNewExpense,
  expenseCategories,
  onSave,
  isSaving,
  isCreating,
}: AddExpenseFormProps) {
  return (
    <div>
      <h3 className="text-sm font-semibold text-gray-900 mb-3">Add Expense Transaction</h3>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <input
          type="number"
          min={1}
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          placeholder="Account ID"
          value={newExpense.accountId}
          onChange={(e) => setNewExpense((prev) => ({ ...prev, accountId: Number(e.target.value) }))}
        />
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={newExpense.categoryId}
          onChange={(e) => setNewExpense((prev) => ({ ...prev, categoryId: Number(e.target.value) }))}
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
          value={newExpense.amount}
          onChange={(e) => setNewExpense((prev) => ({ ...prev, amount: Number(e.target.value) }))}
        />
        <input
          type="date"
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={newExpense.occurredAt}
          onChange={(e) => setNewExpense((prev) => ({ ...prev, occurredAt: e.target.value }))}
        />
        <input
          type="text"
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          placeholder="Payee"
          value={newExpense.payee}
          onChange={(e) => setNewExpense((prev) => ({ ...prev, payee: e.target.value }))}
        />
        <input
          type="text"
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          placeholder="Notes"
          value={newExpense.notes}
          onChange={(e) => setNewExpense((prev) => ({ ...prev, notes: e.target.value }))}
        />
      </div>
      <div className="mt-3">
        <button
          type="button"
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
          onClick={onSave}
          disabled={isSaving}
        >
          {isCreating ? 'Saving...' : 'Add Expense'}
        </button>
      </div>
    </div>
  );
}
