import { useMemo } from 'react';
import Typography from '@mui/material/Typography';
import { useBudgetPlans } from './hooks/useBudgetPlans';
import { useBudgetPlanForm } from './hooks/useBudgetPlanForm';
import { useCategories } from '../categories/hooks/useCategories';
import BudgetPlanCard from './components/BudgetPlanCard';
import PlanLineDialog from './components/PlanLineDialog';

type BudgetPlansSectionProps = {
  isLoading: boolean;
  setStatusMessage: (msg: string | null) => void;
  setStatusError: (msg: string | null) => void;
};

const BudgetPlansSection = ({
  isLoading,
  setStatusMessage,
  setStatusError,
}: BudgetPlansSectionProps) => {
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

  const form = useBudgetPlanForm(budgetPlans, expenseCategories, setStatusMessage, setStatusError);

  if (isLoading) return null;

  return (
    <>
      <div className="space-y-4">
        {budgetPlans.length > 0 ? (
          budgetPlans.map((plan) => (
            <BudgetPlanCard
              key={plan.id}
              plan={plan}
              categoryNameById={categoryNameById}
              onAddLine={form.openForAdd}
              onEditLine={form.openForEdit}
            />
          ))
        ) : (
          <Typography color="text.secondary" fontStyle="italic">
            No budget plans found
          </Typography>
        )}
      </div>

      <PlanLineDialog
        open={form.dialogOpen}
        mode={form.dialogMode}
        formData={form.formData}
        categories={expenseCategories}
        isSaving={form.isSaving}
        onClose={form.closeDialog}
        onChange={form.updateField}
        onSave={form.save}
        onDelete={form.deleteLine}
      />
    </>
  );
};

export default BudgetPlansSection;
