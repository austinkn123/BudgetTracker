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
  const invalidateCategoryQueries = () =>
    Promise.all(
      ['categories', 'money-map'].map((key) => queryClient.invalidateQueries({ queryKey: [key] })),
    );

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
    mutationFn: async (category: Category) => categoryService.update(category.id, category),
    onSuccess: async () => {
      await invalidateCategoryQueries();
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
      await invalidateCategoryQueries();
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
