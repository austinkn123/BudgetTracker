import { useCallback, useMemo, useState } from 'react';
import { format } from 'date-fns';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { budgetPlanService } from '../../../shared/services/budgetPlan.service';
import type { BudgetPlan } from '../../../shared/types/api';
import type { BudgetPlanFormData } from '../../../shared/validation/budgetPlanSchema';

const defaultValues: BudgetPlanFormData = {
  name: '',
  planMonth: format(new Date(), 'yyyy-MM'),
  netIncomeMonthly: 0,
  isActive: false,
};

const toPlanMonthValue = (planMonth: string) => `${planMonth}-01`;

export const useBudgetPlanManagement = (
  budgetPlans: BudgetPlan[],
  setStatusMessage: (msg: string | null) => void,
  setStatusError: (msg: string | null) => void,
) => {
  const queryClient = useQueryClient();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'add' | 'edit'>('add');
  const [editingPlanId, setEditingPlanId] = useState<number | null>(null);
  const [initialValues, setInitialValues] = useState<BudgetPlanFormData>(defaultValues);

  const activePlan = useMemo(
    () => budgetPlans.find((plan) => plan.isActive) ?? null,
    [budgetPlans],
  );

  const deactivateOtherActivePlans = useCallback(async (targetPlanId: number | null) => {
    const plansToDeactivate = budgetPlans.filter(
      (plan) => plan.isActive && plan.id !== targetPlanId,
    );

    await Promise.all(
      plansToDeactivate.map((plan) =>
        budgetPlanService.update(plan.id, {
          ...plan,
          isActive: false,
          updatedAt: new Date().toISOString(),
        }),
      ),
    );
  }, [budgetPlans]);

  const openForAdd = useCallback(() => {
    setDialogMode('add');
    setEditingPlanId(null);
    setInitialValues({ ...defaultValues });
    setDialogOpen(true);
  }, []);

  const openForEdit = useCallback((plan: BudgetPlan) => {
    setDialogMode('edit');
    setEditingPlanId(plan.id);
    setInitialValues({
      name: plan.name,
      planMonth: plan.planMonth.slice(0, 7),
      netIncomeMonthly: plan.netIncomeMonthly,
      isActive: plan.isActive,
    });
    setDialogOpen(true);
  }, []);

  const closeDialog = useCallback(() => {
    setDialogOpen(false);
  }, []);

  const savePlanMutation = useMutation({
    mutationFn: async (values: BudgetPlanFormData) => {
      if (dialogMode === 'add') {
        if (values.isActive) {
          await deactivateOtherActivePlans(null);
        }

        await budgetPlanService.create({
          userId: 0,
          name: values.name,
          planMonth: toPlanMonthValue(values.planMonth),
          netIncomeMonthly: values.netIncomeMonthly,
          isActive: values.isActive,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
          lines: [],
        });

        return;
      }

      if (editingPlanId === null) {
        throw new Error('No budget plan selected for edit.');
      }

      const existingPlan = budgetPlans.find((plan) => plan.id === editingPlanId);
      if (!existingPlan) {
        throw new Error('Budget plan not found.');
      }

      if (values.isActive) {
        await deactivateOtherActivePlans(existingPlan.id);
      }

      await budgetPlanService.update(existingPlan.id, {
        ...existingPlan,
        name: values.name,
        planMonth: toPlanMonthValue(values.planMonth),
        netIncomeMonthly: values.netIncomeMonthly,
        isActive: values.isActive,
        updatedAt: new Date().toISOString(),
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['budgetPlans'] });
      setStatusError(null);
      setStatusMessage(dialogMode === 'add' ? 'Budget plan created.' : 'Budget plan updated.');
      closeDialog();
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to save budget plan: ${error.message}`);
    },
  });

  const deletePlanMutation = useMutation({
    mutationFn: async (planId: number) => {
      const deletingPlan = budgetPlans.find((plan) => plan.id === planId);
      if (!deletingPlan) {
        throw new Error('Budget plan not found.');
      }

      await budgetPlanService.delete(planId);

      if (deletingPlan.isActive) {
        const fallbackPlan = budgetPlans.find((plan) => plan.id !== deletingPlan.id);
        if (fallbackPlan) {
          await budgetPlanService.update(fallbackPlan.id, {
            ...fallbackPlan,
            isActive: true,
            updatedAt: new Date().toISOString(),
          });
        }
      }
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['budgetPlans'] });
      setStatusError(null);
      setStatusMessage('Budget plan deleted.');
      closeDialog();
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to delete budget plan: ${error.message}`);
    },
  });

  const switchActiveMutation = useMutation({
    mutationFn: async (planId: number) => {
      const targetPlan = budgetPlans.find((plan) => plan.id === planId);
      if (!targetPlan) {
        throw new Error('Budget plan not found.');
      }

      if (targetPlan.isActive) {
        return false;
      }

      const plansToUpdate = budgetPlans.filter((plan) => plan.isActive || plan.id === planId);

      await Promise.all(
        plansToUpdate.map((plan) =>
          budgetPlanService.update(plan.id, {
            ...plan,
            isActive: plan.id === planId,
            updatedAt: new Date().toISOString(),
          }),
        ),
      );

      return true;
    },
    onSuccess: async (switched) => {
      await queryClient.invalidateQueries({ queryKey: ['budgetPlans'] });
      setStatusError(null);
      setStatusMessage(switched ? 'Active budget plan updated.' : 'That plan is already active.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to switch active plan: ${error.message}`);
    },
  });

  const savePlan = useCallback(async (values: BudgetPlanFormData) => {
    await savePlanMutation.mutateAsync(values);
  }, [savePlanMutation]);

  const deletePlan = useCallback(async (planId?: number) => {
    const targetPlanId = planId ?? editingPlanId;
    if (targetPlanId === null) {
      throw new Error('No budget plan selected for delete.');
    }

    await deletePlanMutation.mutateAsync(targetPlanId);
  }, [deletePlanMutation, editingPlanId]);

  const switchActivePlan = useCallback(async (planId: number) => {
    await switchActiveMutation.mutateAsync(planId);
  }, [switchActiveMutation]);

  return {
    activePlan,
    dialogOpen,
    dialogMode,
    initialValues,
    isSaving: savePlanMutation.isPending || deletePlanMutation.isPending,
    isSwitchingPlan: switchActiveMutation.isPending,
    openForAdd,
    openForEdit,
    closeDialog,
    savePlan,
    deletePlan,
    switchActivePlan,
  };
};
