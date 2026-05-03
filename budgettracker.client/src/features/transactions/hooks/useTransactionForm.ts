import { useCallback, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import { transactionService } from '../../../shared/services/transaction.service';
import type { Category, Transaction } from '../../../shared/types/api';
import type { TransactionFormData } from '../../../shared/validation/transactionSchema';

const todayInputValue = format(new Date(), 'yyyy-MM-dd');

const emptyForm = (
  defaultAccountId: number,
  defaultCategoryId: number,
  occurredAt = todayInputValue,
): TransactionFormData => ({
  accountId: defaultAccountId,
  categoryId: defaultCategoryId,
  amount: 0,
  occurredAt,
  payee: '',
  notes: '',
});

export const useTransactionForm = (
  transactions: Transaction[],
  categories: Category[],
  setStatusMessage: (msg: string | null) => void,
  setStatusError: (msg: string | null) => void,
) => {
  const queryClient = useQueryClient();

  const defaultAccountId = transactions[0]?.accountId ?? 1;
  const defaultCategoryId = categories[0]?.id ?? 0;

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'add' | 'edit'>('add');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [initialValues, setInitialValues] = useState<TransactionFormData>(
    emptyForm(defaultAccountId, defaultCategoryId),
  );

  const openForAdd = useCallback((occurredAt?: string) => {
    setInitialValues(emptyForm(defaultAccountId, defaultCategoryId, occurredAt ?? todayInputValue));
    setEditingId(null);
    setDialogMode('add');
    setDialogOpen(true);
  }, [defaultAccountId, defaultCategoryId]);

  const openForEdit = useCallback((transaction: Transaction) => {
    setInitialValues({
      accountId: transaction.accountId,
      categoryId: transaction.categoryId,
      amount: transaction.amount,
      occurredAt: format(new Date(transaction.occurredAt), 'yyyy-MM-dd'),
      payee: transaction.payee || '',
      notes: transaction.notes || '',
    });
    setEditingId(transaction.id);
    setDialogMode('edit');
    setDialogOpen(true);
  }, []);

  const closeDialog = useCallback(() => {
    setDialogOpen(false);
  }, []);

  const createMutation = useMutation({
    mutationFn: async (payload: Omit<Transaction, 'id'>) => transactionService.create(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['transactions'] });
      setStatusError(null);
      setStatusMessage('Transaction added.');
      closeDialog();
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to add transaction: ${error.message}`);
    },
  });

  const updateMutation = useMutation({
    mutationFn: async ({ id, payload }: { id: number; payload: Transaction }) =>
      transactionService.update(id, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['transactions'] });
      setStatusError(null);
      setStatusMessage('Transaction updated.');
      closeDialog();
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to update transaction: ${error.message}`);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: number) => transactionService.delete(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['transactions'] });
      setStatusError(null);
      setStatusMessage('Transaction deleted.');
      closeDialog();
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to delete transaction: ${error.message}`);
    },
  });

  const save = useCallback((formData: TransactionFormData) => {
    if (dialogMode === 'add') {
      const payload: Omit<Transaction, 'id'> = {
        accountId: formData.accountId,
        categoryId: formData.categoryId,
        transactionType: 'Expense',
        amount: formData.amount,
        occurredAt: new Date(`${formData.occurredAt}T00:00:00`).toISOString(),
        payee: formData.payee || undefined,
        notes: formData.notes || undefined,
        transferAccountId: undefined,
        createdAt: new Date().toISOString(),
      };
      createMutation.mutate(payload);
    } else if (editingId !== null) {
      const existing = transactions.find((t) => t.id === editingId);
      if (!existing) return;

      const payload: Transaction = {
        ...existing,
        accountId: formData.accountId,
        categoryId: formData.categoryId,
        amount: formData.amount,
        occurredAt: new Date(`${formData.occurredAt}T00:00:00`).toISOString(),
        payee: formData.payee || undefined,
        notes: formData.notes || undefined,
      };
      updateMutation.mutate({ id: editingId, payload });
    }
  }, [dialogMode, editingId, transactions, createMutation, updateMutation]);

  const deleteTransaction = useCallback(() => {
    if (editingId !== null) {
      deleteMutation.mutate(editingId);
    }
  }, [editingId, deleteMutation]);

  const isSaving = createMutation.isPending || updateMutation.isPending || deleteMutation.isPending;

  return {
    dialogOpen,
    dialogMode,
    initialValues,
    isSaving,
    openForAdd,
    openForEdit,
    closeDialog,
    save,
    deleteTransaction,
  };
};
