import { useCallback, useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { transactionService } from '../../../shared/services/transaction.service';
import type { Transaction } from '../../../shared/types/api';

/**
 * Selection + categorisation for the review list. Categorising through the edit dialog one row
 * at a time is the bottleneck this exists to remove: inline changes and bulk assignment both
 * funnel through the same PATCH endpoint.
 */
export const useTransactionReview = (
  visibleTransactions: Transaction[],
  setStatusMessage: (msg: string | null) => void,
  setStatusError: (msg: string | null) => void,
) => {
  const queryClient = useQueryClient();
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());

  const invalidate = useCallback(() => {
    // Every filtered slice is a separate cache entry, so invalidate the whole family.
    queryClient.invalidateQueries({ queryKey: ['transactions'] });
    queryClient.invalidateQueries({ queryKey: ['budget-analysis'] });
  }, [queryClient]);

  const categorizeMutation = useMutation({
    mutationFn: ({ ids, categoryId }: { ids: number[]; categoryId: number | null }) =>
      transactionService.setCategoryBulk(ids, categoryId),
    onSuccess: (updated, variables) => {
      setStatusError(null);
      setStatusMessage(
        variables.ids.length === 1
          ? 'Category updated.'
          : `Category updated on ${updated} transactions.`,
      );
      setSelectedIds(new Set());
      invalidate();
    },
    onError: () => {
      setStatusMessage(null);
      setStatusError('Could not update the category. Please try again.');
    },
  });

  const notesMutation = useMutation({
    mutationFn: ({ id, notes }: { id: number; notes: string | null }) =>
      transactionService.setNotes(id, notes),
    onSuccess: () => {
      setStatusError(null);
      setStatusMessage('Note saved.');
      invalidate();
    },
    onError: () => {
      setStatusMessage(null);
      setStatusError('Could not save the note. Please try again.');
    },
  });

  const toggleSelected = useCallback((id: number, selected: boolean) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (selected) next.add(id);
      else next.delete(id);
      return next;
    });
  }, []);

  const toggleAll = useCallback(
    (selected: boolean) => {
      setSelectedIds(selected ? new Set(visibleTransactions.map((t) => t.id)) : new Set());
    },
    [visibleTransactions],
  );

  const clearSelection = useCallback(() => setSelectedIds(new Set()), []);

  const setCategoryFor = useCallback(
    (id: number, categoryId: number | null) => {
      categorizeMutation.mutate({ ids: [id], categoryId });
    },
    [categorizeMutation],
  );

  const setCategoryForSelected = useCallback(
    (categoryId: number | null) => {
      if (selectedIds.size === 0) return;
      categorizeMutation.mutate({ ids: [...selectedIds], categoryId });
    },
    [categorizeMutation, selectedIds],
  );

  const setNotesFor = useCallback(
    (id: number, notes: string | null) => notesMutation.mutate({ id, notes }),
    [notesMutation],
  );

  const isBusy = categorizeMutation.isPending || notesMutation.isPending;

  return useMemo(
    () => ({
      selectedIds,
      toggleSelected,
      toggleAll,
      clearSelection,
      setCategoryFor,
      setCategoryForSelected,
      setNotesFor,
      isBusy,
    }),
    [
      selectedIds,
      toggleSelected,
      toggleAll,
      clearSelection,
      setCategoryFor,
      setCategoryForSelected,
      setNotesFor,
      isBusy,
    ],
  );
};
