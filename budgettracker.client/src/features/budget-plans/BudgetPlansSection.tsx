import { useCallback, useMemo } from 'react';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Typography from '@mui/material/Typography';
import { Plus } from 'lucide-react';
import { useBudgetPlans } from './hooks/useBudgetPlans';
import { useBudgetPlanForm } from './hooks/useBudgetPlanForm';
import { useBudgetPlanManagement } from './hooks/useBudgetPlanManagement';
import { useCategories } from '../categories/hooks/useCategories';
import BudgetPlanCard from './components/BudgetPlanCard';
import BudgetPlanDialog from './components/BudgetPlanDialog';
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

  const lineForm = useBudgetPlanForm(
    budgetPlans,
    expenseCategories,
    setStatusMessage,
    setStatusError,
  );

  const planManagement = useBudgetPlanManagement(
    budgetPlans,
    setStatusMessage,
    setStatusError,
  );

  const handleDeletePlan = useCallback((planId: number) => {
    const confirmed = window.confirm('Delete this budget plan and all of its lines?');
    if (!confirmed) {
      return;
    }

    void planManagement.deletePlan(planId);
  }, [planManagement]);

  if (isLoading) return null;

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Typography variant="h6" className="font-semibold text-gray-900">
            Budget Plans
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Create, edit, switch, and maintain multiple monthly plans.
          </Typography>
        </div>

        <div className="flex items-center gap-2">
          {planManagement.activePlan ? (
            <Chip
              size="small"
              color="success"
              label={`Active: ${planManagement.activePlan.name}`}
            />
          ) : (
            <Chip size="small" label="No Active Plan" />
          )}
          <Button
            variant="contained"
            startIcon={<Plus className="h-4 w-4" />}
            onClick={planManagement.openForAdd}
          >
            Add Plan
          </Button>
        </div>
      </div>

      <div className="space-y-4">
        {budgetPlans.length > 0 ? (
          budgetPlans.map((plan) => (
            <BudgetPlanCard
              key={plan.id}
              plan={plan}
              categoryNameById={categoryNameById}
              isSwitchingPlan={planManagement.isSwitchingPlan}
              onAddLine={lineForm.openForAdd}
              onEditLine={lineForm.openForEdit}
              onSwitchActive={(planId) => void planManagement.switchActivePlan(planId)}
              onEditPlan={planManagement.openForEdit}
              onDeletePlan={(selectedPlan) => handleDeletePlan(selectedPlan.id)}
            />
          ))
        ) : (
          <Typography color="text.secondary" fontStyle="italic">
            No budget plans found
          </Typography>
        )}
      </div>

      <BudgetPlanDialog
        open={planManagement.dialogOpen}
        mode={planManagement.dialogMode}
        initialValues={planManagement.initialValues}
        isSaving={planManagement.isSaving}
        onClose={planManagement.closeDialog}
        onSave={planManagement.savePlan}
        onDelete={() => planManagement.deletePlan()}
      />

      <PlanLineDialog
        open={lineForm.dialogOpen}
        mode={lineForm.dialogMode}
        initialValues={lineForm.initialValues}
        categories={expenseCategories}
        isSaving={lineForm.isSaving}
        onClose={lineForm.closeDialog}
        onSave={lineForm.save}
        onDelete={lineForm.deleteLine}
      />
    </>
  );
};

export default BudgetPlansSection;
