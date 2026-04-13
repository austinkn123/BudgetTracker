import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { budgetPlanService } from '../../../shared/services/budgetPlan.service';
import type { BudgetPlan, BudgetPlanLine, Category } from '../../../shared/types/api';

type PlanExpenseFormState = {
  categoryId: number;
  bucket: 'Core' | 'Buffer';
  cadence: 'Monthly' | 'Annual';
  amount: number;
  isStressFactor: boolean;
  notes: string;
};

export function useBudgetPlanForm(
  budgetPlans: BudgetPlan[],
  expenseCategories: Category[],
  categoryNameById: Map<number, string>,
  setStatusMessage: (msg: string | null) => void,
  setStatusError: (msg: string | null) => void,
) {
  const queryClient = useQueryClient();

  const getExpenseLineOptionLabel = (line: BudgetPlanLine) => {
    const categoryName = line.categoryId
      ? (categoryNameById.get(line.categoryId) ?? `Unknown (${line.categoryId})`)
      : 'No Category';
    return `${categoryName} | ${line.bucket} ${line.cadence} | $${line.amount.toFixed(2)} ($${line.monthlyEquivalent.toFixed(2)}/mo)`;
  };

  const expensePlanLines = useMemo(
    () => budgetPlans.flatMap((plan) =>
      plan.lines
        .filter((line) => line.lineType === 'Expense')
        .map((line) => ({ planId: plan.id, line })),
    ),
    [budgetPlans],
  );

  const [newPlanExpensePlanId, setNewPlanExpensePlanId] = useState<number | ''>('');
  const [newPlanExpense, setNewPlanExpense] = useState<PlanExpenseFormState>({
    categoryId: 0,
    bucket: 'Core',
    cadence: 'Monthly',
    amount: 0,
    isStressFactor: false,
    notes: '',
  });

  const [editPlanExpensePlanId, setEditPlanExpensePlanId] = useState<number | ''>('');
  const [editPlanExpenseLineId, setEditPlanExpenseLineId] = useState<number | ''>('');
  const [editPlanExpense, setEditPlanExpense] = useState<PlanExpenseFormState>({
    categoryId: 0,
    bucket: 'Core',
    cadence: 'Monthly',
    amount: 0,
    isStressFactor: false,
    notes: '',
  });

  useEffect(() => {
    if (expenseCategories.length === 0) return;
    if (newPlanExpense.categoryId <= 0) {
      setNewPlanExpense((prev) => ({ ...prev, categoryId: expenseCategories[0].id }));
    }
    if (editPlanExpense.categoryId <= 0) {
      setEditPlanExpense((prev) => ({ ...prev, categoryId: expenseCategories[0].id }));
    }
  }, [editPlanExpense.categoryId, expenseCategories, newPlanExpense.categoryId]);

  const updateBudgetPlanMutation = useMutation({
    mutationFn: async ({ id, payload }: { id: number; payload: BudgetPlan }) => budgetPlanService.update(id, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['budgetPlans'] });
      setStatusError(null);
      setStatusMessage('Budget plan expense updated.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to update budget plan expense: ${error.message}`);
    },
  });

  const addBudgetPlanExpenseLine = () => {
    if (newPlanExpensePlanId === '') {
      setStatusMessage(null);
      setStatusError('Select a budget plan before adding a planned expense line.');
      return;
    }

    if (newPlanExpense.categoryId <= 0 || newPlanExpense.amount <= 0) {
      setStatusMessage(null);
      setStatusError('Category and amount are required for a planned expense line.');
      return;
    }

    const plan = budgetPlans.find((item) => item.id === newPlanExpensePlanId);
    if (!plan) {
      setStatusMessage(null);
      setStatusError('Selected budget plan was not found.');
      return;
    }

    const maxSortOrder = plan.lines.length > 0
      ? Math.max(...plan.lines.map((line) => line.sortOrder))
      : 0;

    const newLine: BudgetPlanLine = {
      id: 0,
      budgetPlanId: plan.id,
      categoryId: newPlanExpense.categoryId,
      lineType: 'Expense',
      bucket: newPlanExpense.bucket,
      cadence: newPlanExpense.cadence,
      amount: newPlanExpense.amount,
      monthlyEquivalent: newPlanExpense.cadence === 'Annual'
        ? Number((newPlanExpense.amount / 12).toFixed(2))
        : newPlanExpense.amount,
      isStressFactor: newPlanExpense.isStressFactor,
      notes: newPlanExpense.notes || null,
      sortOrder: maxSortOrder + 10,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    const payload: BudgetPlan = {
      ...plan,
      lines: [...plan.lines, newLine],
    };

    updateBudgetPlanMutation.mutate({ id: plan.id, payload });
  };

  const onEditPlanSelection = (planId: number | '') => {
    setEditPlanExpensePlanId(planId);
    setEditPlanExpenseLineId('');

    if (planId === '') return;

    const firstLine = budgetPlans
      .find((plan) => plan.id === planId)
      ?.lines
      .find((line) => line.lineType === 'Expense');

    if (!firstLine) return;

    setEditPlanExpenseLineId(firstLine.id);
    setEditPlanExpense({
      categoryId: firstLine.categoryId ?? 0,
      bucket: firstLine.bucket,
      cadence: firstLine.cadence,
      amount: firstLine.amount,
      isStressFactor: firstLine.isStressFactor,
      notes: firstLine.notes || '',
    });
  };

  const onEditPlanLineSelection = (lineId: number | '') => {
    setEditPlanExpenseLineId(lineId);
    if (lineId === '' || editPlanExpensePlanId === '') return;

    const line = budgetPlans
      .find((plan) => plan.id === editPlanExpensePlanId)
      ?.lines
      .find((item) => item.id === lineId && item.lineType === 'Expense');

    if (!line) return;

    setEditPlanExpense({
      categoryId: line.categoryId ?? 0,
      bucket: line.bucket,
      cadence: line.cadence,
      amount: line.amount,
      isStressFactor: line.isStressFactor,
      notes: line.notes || '',
    });
  };

  const saveEditedBudgetPlanExpenseLine = () => {
    if (editPlanExpensePlanId === '' || editPlanExpenseLineId === '') {
      setStatusMessage(null);
      setStatusError('Select a budget plan and expense line to update.');
      return;
    }

    if (editPlanExpense.categoryId <= 0 || editPlanExpense.amount <= 0) {
      setStatusMessage(null);
      setStatusError('Category and amount are required to update a planned expense line.');
      return;
    }

    const plan = budgetPlans.find((item) => item.id === editPlanExpensePlanId);
    if (!plan) {
      setStatusMessage(null);
      setStatusError('Selected budget plan was not found.');
      return;
    }

    const lineExists = plan.lines.some((line) => line.id === editPlanExpenseLineId && line.lineType === 'Expense');
    if (!lineExists) {
      setStatusMessage(null);
      setStatusError('Selected budget expense line was not found.');
      return;
    }

    const payload: BudgetPlan = {
      ...plan,
      lines: plan.lines.map((line) => {
        if (line.id !== editPlanExpenseLineId) return line;
        return {
          ...line,
          categoryId: editPlanExpense.categoryId,
          bucket: editPlanExpense.bucket,
          cadence: editPlanExpense.cadence,
          amount: editPlanExpense.amount,
          monthlyEquivalent: editPlanExpense.cadence === 'Annual'
            ? Number((editPlanExpense.amount / 12).toFixed(2))
            : editPlanExpense.amount,
          isStressFactor: editPlanExpense.isStressFactor,
          notes: editPlanExpense.notes || null,
          updatedAt: new Date().toISOString(),
        };
      }),
    };

    updateBudgetPlanMutation.mutate({ id: plan.id, payload });
  };

  const isSaving = updateBudgetPlanMutation.isPending;

  return {
    expensePlanLines,
    getExpenseLineOptionLabel,
    newPlanExpensePlanId,
    setNewPlanExpensePlanId,
    newPlanExpense,
    setNewPlanExpense,
    editPlanExpensePlanId,
    editPlanExpenseLineId,
    editPlanExpense,
    setEditPlanExpense,
    addBudgetPlanExpenseLine,
    onEditPlanSelection,
    onEditPlanLineSelection,
    saveEditedBudgetPlanExpenseLine,
    isSaving,
  };
}
