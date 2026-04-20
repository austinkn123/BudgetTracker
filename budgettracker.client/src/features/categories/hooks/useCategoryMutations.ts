import { useMutation, useQueryClient } from '@tanstack/react-query';
import { categoryService } from '../../../shared/services/category.service';
import type { Category } from '../../../shared/types/api';

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

  const createCategory = useMutation({
    mutationFn: async (category: Omit<Category, 'id'>) => categoryService.create(category),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['categories'] });
      setStatusError(null);
      setStatusMessage('Category created.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to create category: ${error.message}`);
    },
  });

  const updateCategory = useMutation({
    mutationFn: async (category: Category) => categoryService.update(category.id, category),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['categories'] });
      setStatusError(null);
      setStatusMessage('Category updated.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to update category: ${error.message}`);
    },
  });

  const deleteCategory = useMutation({
    mutationFn: async (id: number) => categoryService.delete(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['categories'] });
      setStatusError(null);
      setStatusMessage('Category deleted.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to delete category: ${error.message}`);
    },
  });

  return {
    createCategory,
    updateCategory,
    deleteCategory,
  };
};
