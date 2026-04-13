import { useMemo } from 'react';
import { Database } from 'lucide-react';
import { useBudgetPlans } from './hooks/useBudgetPlans';
import { useBudgetPlanForm } from './hooks/useBudgetPlanForm';
import { useCategories } from '../categories/hooks/useCategories';
import { AddPlanExpenseForm } from './components/AddPlanExpenseForm';
import { EditPlanExpenseForm } from './components/EditPlanExpenseForm';
import { BudgetPlanCard } from './components/BudgetPlanCard';

type BudgetPlansSectionProps = {
  isLoading: boolean;
  setStatusMessage: (msg: string | null) => void;
  setStatusError: (msg: string | null) => void;
};

export default function BudgetPlansSection({
  isLoading,
  setStatusMessage,
  setStatusError,
}: BudgetPlansSectionProps) {
  const { data: budgetPlans = [] } = useBudgetPlans();
  const { data: categories = [] } = useCategories();

  const expenseCategories = useMemo(
    () => categories.filter((c) => c.categoryType === 'Expense' || c.categoryType === 'Both'),
    [categories],
  );

  const categoryNameById = useMemo(
    () => new Map(categories.map((c) => [c.id, c.name])),
    [categories],
  );

  const form = useBudgetPlanForm(budgetPlans, expenseCategories, categoryNameById, setStatusMessage, setStatusError);

  if (isLoading) return null;

  return (
    <section className="bg-white rounded-lg shadow">
      <div className="px-6 py-4 border-b border-gray-200">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Database className="w-5 h-5 text-indigo-600" />
            <h2 className="text-xl font-semibold text-gray-900">Budget Plans</h2>
          </div>
          <span className="text-sm text-gray-500">
            {budgetPlans.length} {budgetPlans.length === 1 ? 'plan' : 'plans'}
          </span>
        </div>
      </div>

      <div className="px-6 py-4 border-b border-gray-200 space-y-6 bg-gray-50">
        <AddPlanExpenseForm
          budgetPlans={budgetPlans}
          newPlanExpensePlanId={form.newPlanExpensePlanId}
          setNewPlanExpensePlanId={form.setNewPlanExpensePlanId}
          newPlanExpense={form.newPlanExpense}
          setNewPlanExpense={form.setNewPlanExpense}
          expenseCategories={expenseCategories}
          onSave={form.addBudgetPlanExpenseLine}
          isSaving={form.isSaving}
        />
        <EditPlanExpenseForm
          budgetPlans={budgetPlans}
          expensePlanLines={form.expensePlanLines}
          getExpenseLineOptionLabel={form.getExpenseLineOptionLabel}
          editPlanExpensePlanId={form.editPlanExpensePlanId}
          editPlanExpenseLineId={form.editPlanExpenseLineId}
          editPlanExpense={form.editPlanExpense}
          setEditPlanExpense={form.setEditPlanExpense}
          expenseCategories={expenseCategories}
          onEditPlanSelection={form.onEditPlanSelection}
          onEditPlanLineSelection={form.onEditPlanLineSelection}
          onSave={form.saveEditedBudgetPlanExpenseLine}
          isSaving={form.isSaving}
        />
      </div>

      <div className="px-6 py-4 space-y-6">
        {budgetPlans.length > 0 ? (
          budgetPlans.map((plan) => (
            <BudgetPlanCard key={plan.id} plan={plan} categoryNameById={categoryNameById} />
          ))
        ) : (
          <p className="text-gray-500 italic">No budget plans found</p>
        )}
      </div>
    </section>
  );
}
