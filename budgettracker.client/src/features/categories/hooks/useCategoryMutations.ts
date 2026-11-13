import { useMutation, useQueryClient } from '@tanstack/react-query';
import { categoryService } from '../../../shared/services/category.service';
import type { Category } from '../../../shared/types/api';
import { errorMessage } from '../../../shared/utils/errorMessage';

type StatusCallback = (message: string | null) => void;

type UseCategoryMutationsArgs = {
  setStatusMessage: StatusCallback;
  setStatusError: StatusCallback;
};

export const useCategoryMutations = ({
  setStatusMessage,
  setStatusError,
}: UseCategoryMutationsArgs) => {
  const queryClient = useQueryClient();
  // The map names its lines by category and leaves transfers out, so a rename or retype moves it too.
  const invalidate = (keys: string[]) =>
    Promise.all(keys.map((key) => queryClient.invalidateQueries({ queryKey: [key] })));
  const invalidateCategoryQueries = () => invalidate(['categories', 'money-map']);
  // Reads derived from how rows are categorised: the summary's uncategorized count and category
  // usage live under 'transactions'; analysis and cash flow sum rows by type.
  const invalidateLedgerQueries = () => invalidate(['transactions', 'budget-analysis', 'cash-flow']);

  const createCategory = useMutation({
    mutationFn: async (category: Omit<Category, 'id'>) => categoryService.create(category),
    onSuccess: async () => {
      await invalidateCategoryQueries();
      setStatusError(null);
      setStatusMessage('Category created.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(errorMessage(error, "Couldn't create the category. Please try again."));
    },
  });

  const updateCategory = useMutation({
    mutationFn: async (category: Category) => {
      // Read before the save: once the categories refetch, the old type is gone.
      const previous = queryClient
        .getQueryData<Category[]>(['categories'])
        ?.find((c) => c.id === category.id);
      await categoryService.update(category.id, category);
      return { previous };
    },
    onSuccess: async ({ previous }, category) => {
      // A retype makes the server reclassify the category's rows (Income/Expense follow it), so
      // the ledger moves too. A rename moves no row. With no cached copy, assume it might have.
      const retyped = !previous || previous.categoryType !== category.categoryType;
      await Promise.all([invalidateCategoryQueries(), retyped && invalidateLedgerQueries()]);
      setStatusError(null);
      setStatusMessage('Category updated.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(errorMessage(error, "Couldn't update the category. Please try again."));
    },
  });

  const deleteCategory = useMutation({
    mutationFn: async (id: number) => categoryService.delete(id),
    onSuccess: async () => {
      // The category drops out of category usage and any ledger read that named it.
      await Promise.all([invalidateCategoryQueries(), invalidate(['transactions'])]);
      setStatusError(null);
      setStatusMessage('Category deleted.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(errorMessage(error, "Couldn't delete the category. Please try again."));
    },
  });

  return {
    createCategory,
    updateCategory,
    deleteCategory,
  };
};
