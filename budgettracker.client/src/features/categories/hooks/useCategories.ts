import { useQuery } from '@tanstack/react-query';
import { categoryService } from '../../../shared/services/category.service';
import type { Category } from '../../../shared/types/api';

export const useCategories = () => {
  return useQuery<Category[], Error>({
    queryKey: ['categories'],
    queryFn: () => categoryService.getCurrentUserCategories(),
    retry: 1,
  });
};
