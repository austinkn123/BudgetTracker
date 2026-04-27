import { useCallback, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { budgetPlanService } from '../../../shared/services/budgetPlan.service';
import type { BudgetPlan, BudgetPlanEntry, Category } from '../../../shared/types/api';
import type { PlanLineFormData } from '../../../shared/validation/planLineSchema';

const emptyForm = (defaultCategoryId: number): PlanLineFormData => ({
  categoryId: defaultCategoryId,
  bucket: 'Core',
  cadence: 'Monthly',
  amount: 0,
  isStressFactor: false,
  notes: '',
});

export const useBudgetPlanForm = (
  budgetPlans: BudgetPlan[],
  expenseCategories: Category[],
  setStatusMessage: (msg: string | null) => void,
  setStatusError: (msg: string | null) => void,
) => {
  const queryClient = useQueryClient();
  const defaultCategoryId = expenseCategories[0]?.id ?? 0;

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'add' | 'edit'>('add');
  const [activePlanId, setActivePlanId] = useState<number | null>(null);
  const [editingLineId, setEditingLineId] = useState<number | null>(null);
  const [initialValues, setInitialValues] = useState<PlanLineFormData>(emptyForm(defaultCategoryId));

  const openForAdd = useCallback((planId: number) => {
    setInitialValues(emptyForm(defaultCategoryId));
    setActivePlanId(planId);
    setEditingLineId(null);
    setDialogMode('add');
    setDialogOpen(true);
  }, [defaultCategoryId]);

  const openForEdit = useCallback((planId: number, line: BudgetPlanEntry) => {
    setInitialValues({
      categoryId: line.categoryId ?? 0,
      bucket: line.bucket,
      cadence: line.cadence,
      amount: line.amount,
      isStressFactor: line.isStressFactor,
      notes: line.notes || '',
    });
    setActivePlanId(planId);
    setEditingLineId(line.id);
    setDialogMode('edit');
    setDialogOpen(true);
  }, []);

  const closeDialog = useCallback(() => {
    setDialogOpen(false);
  }, []);

  const updateMutation = useMutation({
    mutationFn: async ({ id, payload }: { id: number; payload: BudgetPlan }) =>
      budgetPlanService.update(id, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['budgetPlans'] });
      setStatusError(null);
      setStatusMessage('Budget plan updated.');
      closeDialog();
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to update budget plan: ${error.message}`);
    },
  });

  const save = useCallback((formData: PlanLineFormData) => {
    if (activePlanId === null) return;

    const plan = budgetPlans.find((p) => p.id === activePlanId);
    if (!plan) return;

    if (dialogMode === 'add') {
      const maxSortOrder = plan.entries.length > 0
        ? Math.max(...plan.entries.map((entry) => entry.sortOrder))
        : 0;

      const newLine: BudgetPlanEntry = {
        id: 0,
        budgetPlanId: plan.id,
        categoryId: formData.categoryId,
        lineType: 'Expense',
        bucket: formData.bucket,
        cadence: formData.cadence,
        amount: formData.amount,
        monthlyEquivalent: formData.cadence === 'Annual'
          ? Number((formData.amount / 12).toFixed(2))
          : formData.amount,
        isStressFactor: formData.isStressFactor,
        notes: formData.notes || null,
        sortOrder: maxSortOrder + 10,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };

      updateMutation.mutate({
        id: plan.id,
        payload: { ...plan, entries: [...plan.entries, newLine] },
      });
    } else if (editingLineId !== null) {
      const updatedEntries = plan.entries.map((entry) => {
        if (entry.id !== editingLineId) return entry;
        return {
          ...entry,
          categoryId: formData.categoryId,
          bucket: formData.bucket,
          cadence: formData.cadence,
          amount: formData.amount,
          monthlyEquivalent: formData.cadence === 'Annual'
            ? Number((formData.amount / 12).toFixed(2))
            : formData.amount,
          isStressFactor: formData.isStressFactor,
          notes: formData.notes || null,
          updatedAt: new Date().toISOString(),
        };
      });

      updateMutation.mutate({
        id: plan.id,
        payload: { ...plan, entries: updatedEntries },
      });
    }
  }, [dialogMode, activePlanId, editingLineId, budgetPlans, updateMutation]);

  const deleteLine = useCallback(() => {
    if (activePlanId === null || editingLineId === null) return;

    const plan = budgetPlans.find((p) => p.id === activePlanId);
    if (!plan) return;

    updateMutation.mutate({
      id: plan.id,
      payload: { ...plan, entries: plan.entries.filter((entry) => entry.id !== editingLineId) },
    });
  }, [activePlanId, editingLineId, budgetPlans, updateMutation]);

  const isSaving = updateMutation.isPending;

  return {
    dialogOpen,
    dialogMode,
    initialValues,
    isSaving,
    openForAdd,
    openForEdit,
    closeDialog,
    save,
    deleteLine,
  };
};
