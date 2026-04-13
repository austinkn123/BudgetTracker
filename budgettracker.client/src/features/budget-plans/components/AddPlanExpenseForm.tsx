import { format } from 'date-fns';
import type { Category } from '../../../shared/types/api';
import type { BudgetPlan } from '../../../shared/types/api';

type AddPlanExpenseFormProps = {
  budgetPlans: BudgetPlan[];
  newPlanExpensePlanId: number | '';
  setNewPlanExpensePlanId: (id: number | '') => void;
  newPlanExpense: {
    categoryId: number;
    bucket: 'Core' | 'Buffer';
    cadence: 'Monthly' | 'Annual';
    amount: number;
    isStressFactor: boolean;
    notes: string;
  };
  setNewPlanExpense: React.Dispatch<React.SetStateAction<AddPlanExpenseFormProps['newPlanExpense']>>;
  expenseCategories: Category[];
  onSave: () => void;
  isSaving: boolean;
};

export function AddPlanExpenseForm({
  budgetPlans,
  newPlanExpensePlanId,
  setNewPlanExpensePlanId,
  newPlanExpense,
  setNewPlanExpense,
  expenseCategories,
  onSave,
  isSaving,
}: AddPlanExpenseFormProps) {
  return (
    <div>
      <h3 className="text-sm font-semibold text-gray-900 mb-3">Add Planned Expense Line</h3>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={newPlanExpensePlanId}
          onChange={(e) => setNewPlanExpensePlanId(e.target.value ? Number(e.target.value) : '')}
        >
          <option value="">Select budget plan</option>
          {budgetPlans.map((plan) => (
            <option key={plan.id} value={plan.id}>
              {plan.name} ({format(new Date(plan.planMonth), 'MMM yyyy')})
            </option>
          ))}
        </select>
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={newPlanExpense.categoryId}
          onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, categoryId: Number(e.target.value) }))}
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
          value={newPlanExpense.amount}
          onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, amount: Number(e.target.value) }))}
        />
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={newPlanExpense.bucket}
          onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, bucket: e.target.value as 'Core' | 'Buffer' }))}
        >
          <option value="Core">Core</option>
          <option value="Buffer">Buffer</option>
        </select>
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={newPlanExpense.cadence}
          onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, cadence: e.target.value as 'Monthly' | 'Annual' }))}
        >
          <option value="Monthly">Monthly</option>
          <option value="Annual">Annual</option>
        </select>
        <label className="inline-flex items-center gap-2 rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={newPlanExpense.isStressFactor}
            onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, isStressFactor: e.target.checked }))}
          />
          Stress Factor
        </label>
        <input
          type="text"
          className="rounded-md border border-gray-300 px-3 py-2 text-sm md:col-span-3"
          placeholder="Notes"
          value={newPlanExpense.notes}
          onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, notes: e.target.value }))}
        />
      </div>
      <div className="mt-3">
        <button
          type="button"
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
          onClick={onSave}
          disabled={isSaving}
        >
          {isSaving ? 'Saving...' : 'Add Planned Expense'}
        </button>
      </div>
    </div>
  );
}
