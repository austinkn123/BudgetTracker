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
  const [locked, setLocked] = useState(false);
  const [editingTransaction, setEditingTransaction] = useState<Transaction | null>(null);
  const [initialValues, setInitialValues] = useState<TransactionFormData>(
    emptyForm(defaultAccountId, defaultCategoryId),
  );

  const openForAdd = useCallback((occurredAt?: string) => {
    setInitialValues(emptyForm(defaultAccountId, defaultCategoryId, occurredAt ?? todayInputValue));
    setEditingId(null);
    setLocked(false);
    setEditingTransaction(null);
    setDialogMode('add');
    setDialogOpen(true);
  }, [defaultAccountId, defaultCategoryId]);

  const openForEdit = useCallback((transaction: Transaction) => {
    // [BUD-18] Guard against silent sign loss on non-Expense edit. Imported
    // (Plaid) rows are exempt: their immutable fields are locked in the dialog,
    // so a non-Expense imported row can open safely. Only MANUAL non-Expense
    // edits remain blocked until BUD-19 ships the TransactionType picker.
    if (!transaction.isImported && transaction.transactionType !== 'Expense') {
      setStatusMessage(null);
      setStatusError(
        'Editing Income, Transfer, and Adjustment transactions is not yet supported (see BUD-19).',
      );
      return;
    }

    setLocked(Boolean(transaction.isImported));
    setEditingTransaction(transaction);
    setInitialValues({
      accountId: transaction.accountId,
      categoryId: transaction.categoryId,
      // BUD-18: the wire amount is signed (negative for Expense/Transfer); the
      // dialog input is always-positive UX, so present the magnitude here and
      // re-apply the sign in the save handler based on transactionType.
      amount: Math.abs(transaction.amount),
      occurredAt: format(new Date(transaction.occurredAt), 'yyyy-MM-dd'),
      payee: transaction.payee || '',
      notes: transaction.notes || '',
    });
    setEditingId(transaction.id);
    setDialogMode('edit');
    setDialogOpen(true);
  }, [setStatusError, setStatusMessage]);

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

  // BUD-18: apply sign at submit time based on transaction type. The form
  // input is always-positive UX; the wire format is signed (Expense negative,
  // Income positive, Transfer negative, Adjustment user-driven).
  const applySign = (amount: number, transactionType: string) => {
    const magnitude = Math.abs(amount);
    switch (transactionType) {
      case 'Income':
        return magnitude;
      case 'Expense':
      case 'Transfer':
        return -magnitude;
      default:
        // Adjustment (and any unknown type) preserves the user-entered sign.
        return amount;
    }
  };

  const save = useCallback((formData: TransactionFormData) => {
    if (dialogMode === 'add') {
      const transactionType = 'Expense';
      const payload: Omit<Transaction, 'id'> = {
        accountId: formData.accountId,
        categoryId: formData.categoryId,
        transactionType,
        amount: applySign(formData.amount, transactionType),
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

      // Imported (Plaid) rows are immutable except for category & notes. When
      // locked, take ONLY those two from the form and preserve every other
      // field (amount/sign, occurredAt, payee, accountId) from `...existing`.
      const payload: Transaction = locked
        ? {
            ...existing,
            categoryId: formData.categoryId,
            notes: formData.notes || undefined,
          }
        : {
            ...existing,
            accountId: formData.accountId,
            categoryId: formData.categoryId,
            amount: applySign(formData.amount, existing.transactionType),
            occurredAt: new Date(`${formData.occurredAt}T00:00:00`).toISOString(),
            payee: formData.payee || undefined,
            notes: formData.notes || undefined,
          };
      updateMutation.mutate({ id: editingId, payload });
    }
  }, [dialogMode, editingId, locked, transactions, createMutation, updateMutation]);

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
    locked,
    editingTransaction,
    isSaving,
    openForAdd,
    openForEdit,
    closeDialog,
    save,
    deleteTransaction,
  };
};
