import { format } from 'date-fns';
import type { BudgetPlan, BudgetPlanLine, Category } from '../../../shared/types/api';

type EditPlanExpenseFormProps = {
  budgetPlans: BudgetPlan[];
  expensePlanLines: { planId: number; line: BudgetPlanLine }[];
  getExpenseLineOptionLabel: (line: BudgetPlanLine) => string;
  editPlanExpensePlanId: number | '';
  editPlanExpenseLineId: number | '';
  editPlanExpense: {
    categoryId: number;
    bucket: 'Core' | 'Buffer';
    cadence: 'Monthly' | 'Annual';
    amount: number;
    isStressFactor: boolean;
    notes: string;
  };
  setEditPlanExpense: React.Dispatch<React.SetStateAction<EditPlanExpenseFormProps['editPlanExpense']>>;
  expenseCategories: Category[];
  onEditPlanSelection: (planId: number | '') => void;
  onEditPlanLineSelection: (lineId: number | '') => void;
  onSave: () => void;
  isSaving: boolean;
};

export function EditPlanExpenseForm({
  budgetPlans,
  expensePlanLines,
  getExpenseLineOptionLabel,
  editPlanExpensePlanId,
  editPlanExpenseLineId,
  editPlanExpense,
  setEditPlanExpense,
  onEditPlanSelection,
  onEditPlanLineSelection,
  onSave,
  isSaving,
}: EditPlanExpenseFormProps) {
  return (
    <div>
      <h3 className="text-sm font-semibold text-gray-900 mb-3">Update Planned Expense Line</h3>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={editPlanExpensePlanId}
          onChange={(e) => onEditPlanSelection(e.target.value ? Number(e.target.value) : '')}
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
          value={editPlanExpenseLineId}
          onChange={(e) => onEditPlanLineSelection(e.target.value ? Number(e.target.value) : '')}
        >
          <option value="">Select expense line</option>
          {expensePlanLines
            .filter((item) => editPlanExpensePlanId === '' || item.planId === editPlanExpensePlanId)
            .map((item) => (
              <option key={item.line.id} value={item.line.id}>
                {getExpenseLineOptionLabel(item.line)}
              </option>
            ))}
        </select>
        <input
          type="number"
          step="0.01"
          min={0}
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          placeholder="Amount"
          value={editPlanExpense.amount}
          onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, amount: Number(e.target.value) }))}
        />
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={editPlanExpense.bucket}
          onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, bucket: e.target.value as 'Core' | 'Buffer' }))}
        >
          <option value="Core">Core</option>
          <option value="Buffer">Buffer</option>
        </select>
        <select
          className="rounded-md border border-gray-300 px-3 py-2 text-sm"
          value={editPlanExpense.cadence}
          onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, cadence: e.target.value as 'Monthly' | 'Annual' }))}
        >
          <option value="Monthly">Monthly</option>
          <option value="Annual">Annual</option>
        </select>
        <label className="inline-flex items-center gap-2 rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={editPlanExpense.isStressFactor}
            onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, isStressFactor: e.target.checked }))}
          />
          Stress Factor
        </label>
        <input
          type="text"
          className="rounded-md border border-gray-300 px-3 py-2 text-sm md:col-span-3"
          placeholder="Notes"
          value={editPlanExpense.notes}
          onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, notes: e.target.value }))}
        />
      </div>
      <div className="mt-3">
        <button
          type="button"
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
          onClick={onSave}
          disabled={isSaving}
        >
          {isSaving ? 'Saving...' : 'Update Planned Expense'}
        </button>
      </div>
    </div>
  );
}
