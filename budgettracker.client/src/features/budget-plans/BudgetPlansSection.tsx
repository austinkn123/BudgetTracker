import { useMemo, useState } from 'react';
import { Plus } from 'lucide-react';
import { Badge, Button, Card, ConfirmModal, Skeleton } from '../../shared/components/ui';
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

  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);

  if (isLoading) {
    return (
      <div className="space-y-4" aria-busy>
        <div className="space-y-2">
          <Skeleton width={120} height={18} />
          <Skeleton width={280} height={12} />
        </div>
        <Card>
          <Skeleton width={180} height={18} />
          <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} height={64} className="rounded-lg" />
            ))}
          </div>
          <div className="mt-4 space-y-2">
            {[0, 1, 2, 3].map((i) => (
              <Skeleton key={i} height={14} />
            ))}
          </div>
        </Card>
      </div>
    );
  }

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-ink">Your plans</h2>
          <p className="text-sm text-ink-muted">
            Only the active plan drives the dashboard and the month's pacing.
          </p>
        </div>

        <div className="flex items-center gap-2">
          {planManagement.activePlan ? (
            <Badge color="success" label={`Active: ${planManagement.activePlan.name}`} />
          ) : (
            <Badge label="No active plan" />
          )}
          <Button startIcon={<Plus size={16} />} onClick={planManagement.openForAdd}>
            Add plan
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
              onDeletePlan={(selectedPlan) => setPendingDeleteId(selectedPlan.id)}
            />
          ))
        ) : (
          <Card>
            <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-sm font-semibold text-ink">No plans yet</p>
                <p className="mt-1 text-sm text-ink-muted">
                  A plan is your take-home pay split into the lines you expect to spend on. The
                  dashboard measures every month against it.
                </p>
              </div>
              <Button startIcon={<Plus size={16} />} onClick={planManagement.openForAdd}>
                Add plan
              </Button>
            </div>
          </Card>
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

      <ConfirmModal
        open={pendingDeleteId !== null}
        title="Delete budget plan?"
        message="This removes the plan and all of its lines. This can't be undone."
        confirmLabel="Delete plan"
        destructive
        isPending={planManagement.isSaving}
        onCancel={() => setPendingDeleteId(null)}
        onConfirm={() => {
          const planId = pendingDeleteId;
          setPendingDeleteId(null);
          if (planId !== null) void planManagement.deletePlan(planId);
        }}
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
