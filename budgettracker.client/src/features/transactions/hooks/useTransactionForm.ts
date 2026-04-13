import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import { transactionService } from '../../../shared/services/transaction.service';
import type { Category, Transaction } from '../../../shared/types/api';

type ExpenseFormState = {
  accountId: number;
  categoryId: number;
  amount: number;
  occurredAt: string;
  payee: string;
  notes: string;
};

const todayInputValue = format(new Date(), 'yyyy-MM-dd');

export function useTransactionForm(
  transactions: Transaction[],
  expenseCategories: Category[],
  setStatusMessage: (msg: string | null) => void,
  setStatusError: (msg: string | null) => void,
) {
  const queryClient = useQueryClient();

  const expenseTransactions = useMemo(
    () => transactions.filter((t) => t.transactionType === 'Expense'),
    [transactions],
  );

  const defaultAccountId = transactions[0]?.accountId ?? 1;

  const [newExpense, setNewExpense] = useState<ExpenseFormState>({
    accountId: defaultAccountId,
    categoryId: 0,
    amount: 0,
    occurredAt: todayInputValue,
    payee: '',
    notes: '',
  });

  const [editExpenseId, setEditExpenseId] = useState<number | ''>('');
  const [editExpense, setEditExpense] = useState<ExpenseFormState>({
    accountId: defaultAccountId,
    categoryId: 0,
    amount: 0,
    occurredAt: todayInputValue,
    payee: '',
    notes: '',
  });

  useEffect(() => {
    if (newExpense.accountId <= 0) {
      setNewExpense((prev) => ({ ...prev, accountId: defaultAccountId }));
    }
    if (editExpense.accountId <= 0) {
      setEditExpense((prev) => ({ ...prev, accountId: defaultAccountId }));
    }
  }, [defaultAccountId, editExpense.accountId, newExpense.accountId]);

  useEffect(() => {
    if (expenseCategories.length === 0) return;
    if (newExpense.categoryId <= 0) {
      setNewExpense((prev) => ({ ...prev, categoryId: expenseCategories[0].id }));
    }
    if (editExpense.categoryId <= 0) {
      setEditExpense((prev) => ({ ...prev, categoryId: expenseCategories[0].id }));
    }
  }, [editExpense.categoryId, expenseCategories, newExpense.categoryId]);

  const createExpenseMutation = useMutation({
    mutationFn: async (payload: Omit<Transaction, 'id'>) => transactionService.create(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['transactions'] });
      setStatusError(null);
      setStatusMessage('Expense transaction added.');
      setNewExpense((prev) => ({ ...prev, amount: 0, payee: '', notes: '' }));
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to add expense transaction: ${error.message}`);
    },
  });

  const updateExpenseMutation = useMutation({
    mutationFn: async ({ id, payload }: { id: number; payload: Transaction }) => transactionService.update(id, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['transactions'] });
      setStatusError(null);
      setStatusMessage('Expense transaction updated.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to update expense transaction: ${error.message}`);
    },
  });

  const saveNewExpense = () => {
    if (newExpense.accountId <= 0 || newExpense.categoryId <= 0 || newExpense.amount <= 0) {
      setStatusMessage(null);
      setStatusError('Account, category, and amount are required to add an expense transaction.');
      return;
    }

    const payload: Omit<Transaction, 'id'> = {
      accountId: newExpense.accountId,
      categoryId: newExpense.categoryId,
      transactionType: 'Expense',
      amount: newExpense.amount,
      occurredAt: new Date(`${newExpense.occurredAt}T00:00:00`).toISOString(),
      payee: newExpense.payee || undefined,
      notes: newExpense.notes || undefined,
      transferAccountId: undefined,
      createdAt: new Date().toISOString(),
    };

    createExpenseMutation.mutate(payload);
  };

  const loadExpenseForEdit = (id: number | '') => {
    setEditExpenseId(id);
    if (id === '') return;

    const transaction = expenseTransactions.find((item) => item.id === id);
    if (!transaction) return;

    setEditExpense({
      accountId: transaction.accountId,
      categoryId: transaction.categoryId,
      amount: transaction.amount,
      occurredAt: format(new Date(transaction.occurredAt), 'yyyy-MM-dd'),
      payee: transaction.payee || '',
      notes: transaction.notes || '',
    });
  };

  const saveEditedExpense = () => {
    if (editExpenseId === '') {
      setStatusMessage(null);
      setStatusError('Select an expense transaction to update.');
      return;
    }

    if (editExpense.accountId <= 0 || editExpense.categoryId <= 0 || editExpense.amount <= 0) {
      setStatusMessage(null);
      setStatusError('Account, category, and amount are required to update an expense transaction.');
      return;
    }

    const existing = expenseTransactions.find((item) => item.id === editExpenseId);
    if (!existing) {
      setStatusMessage(null);
      setStatusError('Selected expense transaction was not found.');
      return;
    }

    const payload: Transaction = {
      ...existing,
      accountId: editExpense.accountId,
      categoryId: editExpense.categoryId,
      amount: editExpense.amount,
      occurredAt: new Date(`${editExpense.occurredAt}T00:00:00`).toISOString(),
      payee: editExpense.payee || undefined,
      notes: editExpense.notes || undefined,
      transactionType: 'Expense',
      transferAccountId: undefined,
    };

    updateExpenseMutation.mutate({ id: editExpenseId, payload });
  };

  const isSaving = createExpenseMutation.isPending || updateExpenseMutation.isPending;

  return {
    expenseTransactions,
    newExpense,
    setNewExpense,
    editExpenseId,
    editExpense,
    setEditExpense,
    saveNewExpense,
    loadExpenseForEdit,
    saveEditedExpense,
    isSaving,
    isCreating: createExpenseMutation.isPending,
    isUpdating: updateExpenseMutation.isPending,
  };
}
